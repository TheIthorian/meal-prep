using Api.Configuration;
using Api.Data;
using Api.Domain;
using Api.Endpoints.Requests.MealPrep;
using Api.Endpoints.Responses;
using Api.Endpoints.Responses.MealPrep;
using Api.Models;
using Api.Models.Filter;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

internal static class RecipesHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<PaginatedResponse<RecipeListItemResponse>>> GetRecipes(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IFilterConfigurationProvider filterConfigurationProvider,
        HttpContext httpContext,
        Guid workspaceId,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var paginationOptions = PaginationOptions.FromQueryParams(httpContext.Request.Query);
        var includeArchived = paginationOptions.FilterOptions.Any(pair =>
            pair.Key == "includeArchived"
            && bool.TryParse(pair.Value.ToString(), out var parsedValue)
            && parsedValue
        );
        var query = db.Recipes
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(recipe => recipe.WorkspaceId == workspaceId);

        if (!includeArchived) query = query.Where(recipe => !recipe.IsArchived);

        query = query.ApplyFilters(
            paginationOptions.FilterOptions.Where(pair => pair.Key != "includeArchived"),
            filterConfigurationProvider.GetConfiguration<Recipe>()
        );

        var totalCount = await query.CountAsync(cancellationToken);
        var recipes = await query.ApplyPagination(paginationOptions, nameof(Recipe.Title))
            .Select(recipe => new RecipeListItemResponse(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.IsArchived,
                recipe.Tags,
                recipe.SourceUrl,
                recipe.Ingredients.Count,
                recipe.Steps.Count,
                recipe.ImageObjectKey != null && recipe.ImageObjectKey != ""
            ))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Json(PaginatedResponse<RecipeListItemResponse>.ToPaginatedResponse(recipes, paginationOptions, totalCount));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> GetRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid recipeId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync();

        return recipe is null
            ? throw new EntityNotFoundException("Recipe not found", null)
            : TypedResults.Json(recipe.ToRecipeResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImportService recipeImportService,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        [FromBody] SaveRecipeRequest body,
        CancellationToken cancellationToken
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = Recipe.CreateNew(workspaceUser.Workspace, body.Title, body.Servings);
        ApplyRecipe(recipe, body);
        await TryApplyImportedImageAsync(body, recipe, recipeImportService, s3StorageService, cancellationToken);

        await db.Recipes.AddAsync(recipe);
        await db.SaveChangesAsync(cancellationToken);

        recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .FirstAsync(value => value.Id == recipe.Id, cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PatchRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImportService recipeImportService,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        [FromBody] SaveRecipeRequest body,
        CancellationToken cancellationToken
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        ApplyRecipe(recipe, body);
        await TryApplyImportedImageAsync(body, recipe, recipeImportService, s3StorageService, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse());
    }

    [Authorize]
    public static async Task<Ok> DeleteRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = await db.Recipes
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync();

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) {
            await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);
        }

        recipe.IsDeleted = true;
        await db.SaveChangesAsync();
        return TypedResults.Ok();
    }

    [Authorize]
    public static async Task<IResult> GetRecipeImage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var recipe = await db.Recipes
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .Select(value => new { value.ImageObjectKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        if (string.IsNullOrEmpty(recipe.ImageObjectKey)) return TypedResults.NotFound();

        var stream = await s3StorageService.DownloadFileAsync(recipe.ImageObjectKey);
        var contentType = RecipeImageUploadConstants.ContentTypeFromObjectKey(recipe.ImageObjectKey) ?? "application/octet-stream";

        return TypedResults.File(stream, contentType);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostRecipeImage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        IFormFile? file,
        CancellationToken cancellationToken
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        if (file is null || file.Length == 0) throw new InvalidFormatException("Image file is required", null);

        if (file.Length > RecipeImageUploadConstants.MaxBytes) {
            throw new InvalidFormatException("Image file is too large", $"Maximum size is {RecipeImageUploadConstants.MaxBytes} bytes.");
        }

        if (!RecipeImageUploadConstants.IsAllowedContentType(file.ContentType)) {
            throw new InvalidFormatException("Unsupported image type", "Use JPEG, PNG, WebP, or GIF.");
        }

        var safeFileName = RecipeImageUploadConstants.FileNameForUpload(file.FileName, file.ContentType);
        await using var readStream = file.OpenReadStream();
        var newKey = await s3StorageService.UploadFileAsync(readStream, safeFileName, file.ContentType);

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) {
            await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);
        }

        recipe.SetImageObjectKey(newKey);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> DeleteRecipeImage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) {
            await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);
        }

        recipe.SetImageObjectKey(null);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeImportPreviewResponse>> PostImportPreview(
        CurrentUserService currentUserService,
        Guid workspaceId,
        RecipeImportService recipeImportService,
        [FromBody] ImportRecipePreviewRequest body,
        CancellationToken cancellationToken
    )
    {
        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var preview = await recipeImportService.PreviewAsync(body.Url, workspaceId, currentUserService.UserId, cancellationToken);
        return TypedResults.Json(preview.ToResponse());
    }

    private static async Task TryApplyImportedImageAsync(
        SaveRecipeRequest body,
        Recipe recipe,
        RecipeImportService recipeImportService,
        IS3StorageService s3StorageService,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(body.ImportImageUrl) || string.IsNullOrWhiteSpace(body.SourceUrl)) return;

        var payload = await recipeImportService.TryDownloadImportImageAsync(
            body.ImportImageUrl,
            body.SourceUrl,
            cancellationToken
        );

        if (payload is null) return;

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);

        await using var stream = new MemoryStream(payload.Data);
        var key = await s3StorageService.UploadFileAsync(stream, payload.FileName, payload.ContentType);
        recipe.SetImageObjectKey(key);
    }

    private static void ApplyRecipe(Recipe recipe, SaveRecipeRequest body)
    {
        recipe.UpdateDetails(
            body.Title,
            body.Description,
            body.Servings,
            body.SourceUrl,
            body.Notes,
            body.PrepMinutes,
            body.CookMinutes,
            body.IsArchived,
            body.Tags
        );

        recipe.ReplaceIngredients(body.Ingredients.Select((ingredient, index) =>
            RecipeIngredient.CreateNew(
                index,
                ingredient.Name,
                ingredient.DisplayText,
                ingredient.Amount,
                ingredient.Unit,
                ingredient.NormalizedIngredientName,
                ingredient.PreparationNote,
                ingredient.Section
            )
        ));

        recipe.ReplaceSteps(body.Steps.Select((step, index) => RecipeStep.CreateNew(index, step.Instruction, step.TimerSeconds)));

        recipe.SetNutrition(
            body.Nutrition?.ServingBasis,
            body.Nutrition?.Nutrients
                .GroupBy(nutrient => nutrient.NutrientType.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => RecipeNutrition.CreateNew(group.Key, group.Last().Amount))
                .ToArray()
            ?? []
        );
    }
}

internal static class MealPlanEntriesHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<MealPlanEntryResponse[]>> GetMealPlanEntries(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        DateOnly? from,
        DateOnly? to
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddDays(-(int)today.DayOfWeek + 1);
        var end = to ?? start.AddDays(6);

        var entries = await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(entry => entry.WorkspaceId == workspaceId && entry.PlannedDate >= start && entry.PlannedDate <= end)
            .OrderBy(entry => entry.PlannedDate)
            .ThenBy(entry => entry.MealType)
            .ToArrayAsync();

        return TypedResults.Json(entries.Select(entry => entry.ToMealPlanEntryResponse()).ToArray());
    }

    [Authorize]
    public static async Task<JsonHttpResult<MealPlanEntryResponse>> PostMealPlanEntry(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        [FromBody] SaveMealPlanEntryRequest body
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = await db.Recipes
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == body.RecipeId)
            .FirstOrDefaultAsync();

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        var entry = MealPlanEntry.CreateNew(workspaceUser.Workspace, recipe, body.PlannedDate, body.MealType);
        entry.Update(body.PlannedDate, body.MealType, body.TargetServings, body.Notes, body.Status);

        await db.MealPlanEntries.AddAsync(entry);
        await db.SaveChangesAsync();

        entry = await db.MealPlanEntries
            .Include(value => value.Recipe)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .FirstAsync(value => value.Id == entry.Id);
        return TypedResults.Json(entry.ToMealPlanEntryResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<MealPlanEntryResponse>> PatchMealPlanEntry(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid mealPlanEntryId,
        [FromBody] SaveMealPlanEntryRequest body
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var entry = await db.MealPlanEntries
            .Include(value => value.Recipe)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == mealPlanEntryId)
            .FirstOrDefaultAsync();

        if (entry is null) throw new EntityNotFoundException("Meal-plan entry not found", null);

        if (entry.RecipeId != body.RecipeId)
        {
            var recipe = await db.Recipes
                .ForCurrentUser(currentUserId)
                .WhereIsNotDeleted()
                .Where(value => value.WorkspaceId == workspaceId && value.Id == body.RecipeId)
                .FirstOrDefaultAsync();

            if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

            entry.ChangeRecipe(recipe);
        }

        entry.Update(body.PlannedDate, body.MealType, body.TargetServings, body.Notes, body.Status);
        await db.SaveChangesAsync();

        return TypedResults.Json(entry.ToMealPlanEntryResponse());
    }

    [Authorize]
    public static async Task<Ok> DeleteMealPlanEntry(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid mealPlanEntryId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var entry = await db.MealPlanEntries
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == mealPlanEntryId)
            .FirstOrDefaultAsync();

        if (entry is null) throw new EntityNotFoundException("Meal-plan entry not found", null);

        entry.IsDeleted = true;
        await db.SaveChangesAsync();
        return TypedResults.Ok();
    }
}

internal static class ShoppingListsHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListListItemResponse[]>> GetShoppingLists(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingLists = await db.ShoppingLists
            .Include(list => list.Items)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId)
            .OrderByDescending(list => list.GeneratedAt)
            .ThenBy(list => list.Name)
            .ToArrayAsync();

        return TypedResults.Json(shoppingLists.Select(list => list.ToShoppingListListItemResponse()).ToArray());
    }

    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListResponse>> GetShoppingList(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid shoppingListId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .Include(list => list.Sources)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        return shoppingList is null
            ? throw new EntityNotFoundException("Shopping list not found", null)
            : TypedResults.Json(shoppingList.ToShoppingListResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListResponse>> PostGenerateShoppingList(
        CurrentUserService currentUserService,
        ApiDbContext db,
        ShoppingListGenerationService shoppingListGenerationService,
        Guid workspaceId,
        [FromBody] GenerateShoppingListRequest body,
        CancellationToken cancellationToken
    )
    {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipes = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(recipe => recipe.WorkspaceId == workspaceId && body.RecipeIds.Contains(recipe.Id))
            .ToArrayAsync();

        var entries = await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .ThenInclude(recipe => recipe.Ingredients)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(entry => entry.WorkspaceId == workspaceId && body.MealPlanEntryIds.Contains(entry.Id))
            .ToArrayAsync();

        var sources = recipes.Select(recipe => new ShoppingListGenerationSource(
                recipe.Id,
                null,
                recipe.Title,
                recipe.Servings,
                recipe.Servings,
                recipe.Ingredients.Select(ingredient => new ShoppingListGenerationIngredient(
                    ingredient.Name,
                    ingredient.NormalizedIngredientName,
                    ingredient.Amount,
                    ingredient.Unit,
                    ingredient.PreparationNote,
                    ingredient.Section
                )).ToArray()
            ))
            .Concat(entries.Select(entry => new ShoppingListGenerationSource(
                entry.RecipeId,
                entry.Id,
                $"{entry.Recipe.Title} ({entry.PlannedDate:yyyy-MM-dd})",
                entry.Recipe.Servings,
                entry.TargetServings ?? entry.Recipe.Servings,
                entry.Recipe.Ingredients.Select(ingredient => new ShoppingListGenerationIngredient(
                    ingredient.Name,
                    ingredient.NormalizedIngredientName,
                    ingredient.Amount,
                    ingredient.Unit,
                    ingredient.PreparationNote,
                    ingredient.Section
                )).ToArray()
            )))
            .ToArray();

        if (sources.Length == 0)
            throw new InvalidFormatException("Shopping list generation failed", "No valid recipes or plan entries were selected.");

        var shoppingList = await shoppingListGenerationService.BuildFromSourcesAsync(
            workspaceUser.Workspace,
            body.Name,
            body.Notes,
            sources,
            cancellationToken
        );

        await db.ShoppingLists.AddAsync(shoppingList, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .Include(list => list.Sources)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .FirstAsync(list => list.Id == shoppingList.Id, cancellationToken);

        return TypedResults.Json(shoppingList.ToShoppingListResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListResponse>> PatchShoppingList(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid shoppingListId,
        [FromBody] SaveShoppingListRequest body
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .Include(list => list.Sources)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        if (shoppingList is null) throw new EntityNotFoundException("Shopping list not found", null);

        shoppingList.UpdateDetails(body.Name, body.Notes);
        await db.SaveChangesAsync();
        return TypedResults.Json(shoppingList.ToShoppingListResponse());
    }

    [Authorize]
    public static async Task<Ok> DeleteShoppingList(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid shoppingListId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        if (shoppingList is null) throw new EntityNotFoundException("Shopping list not found", null);

        shoppingList.IsDeleted = true;
        await db.SaveChangesAsync();
        return TypedResults.Ok();
    }

    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListItemResponse>> PostShoppingListItem(
        CurrentUserService currentUserService,
        ApiDbContext db,
        MeasurementService measurementService,
        Guid workspaceId,
        Guid shoppingListId,
        [FromBody] SaveShoppingListItemRequest body
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        if (shoppingList is null) throw new EntityNotFoundException("Shopping list not found", null);

        var displayText = measurementService.BuildDisplayText(body.Amount, body.Unit, body.Name, body.Note);

        var item = ShoppingListItem.CreateNew(
            shoppingList.Items.Count,
            body.Name,
            displayText,
            body.Amount,
            body.Unit,
            body.NormalizedIngredientName ?? measurementService.NormalizeIngredientName(body.Name),
            body.IsApproximate,
            body.IsManual,
            body.Category,
            body.Note,
            body.SourceNames ?? []
        );

        item.Update(
            body.Name,
            displayText,
            body.Amount,
            body.Unit,
            body.NormalizedIngredientName ?? measurementService.NormalizeIngredientName(body.Name),
            body.IsApproximate,
            body.IsChecked,
            body.IsManual,
            body.Category,
            body.Note,
            body.SourceNames ?? []
        );

        shoppingList.Items.Add(item);
        await db.SaveChangesAsync();

        return TypedResults.Json(item.ToResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<ShoppingListItemResponse>> PatchShoppingListItem(
        CurrentUserService currentUserService,
        ApiDbContext db,
        MeasurementService measurementService,
        Guid workspaceId,
        Guid shoppingListId,
        Guid itemId,
        [FromBody] SaveShoppingListItemRequest body
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        if (shoppingList is null) throw new EntityNotFoundException("Shopping list not found", null);

        var item = shoppingList.Items.FirstOrDefault(value => value.Id == itemId);
        if (item is null) throw new EntityNotFoundException("Shopping-list item not found", null);

        var displayText = measurementService.BuildDisplayText(body.Amount, body.Unit, body.Name, body.Note);

        item.Update(
            body.Name,
            displayText,
            body.Amount,
            body.Unit,
            body.NormalizedIngredientName ?? measurementService.NormalizeIngredientName(body.Name),
            body.IsApproximate,
            body.IsChecked,
            body.IsManual,
            body.Category,
            body.Note,
            body.SourceNames ?? item.SourceNames
        );

        await db.SaveChangesAsync();
        return TypedResults.Json(item.ToResponse());
    }

    [Authorize]
    public static async Task<Ok> DeleteShoppingListItem(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid shoppingListId,
        Guid itemId
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var shoppingList = await db.ShoppingLists
            .Include(list => list.Items)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(list => list.WorkspaceId == workspaceId && list.Id == shoppingListId)
            .FirstOrDefaultAsync();

        if (shoppingList is null) throw new EntityNotFoundException("Shopping list not found", null);

        var item = shoppingList.Items.FirstOrDefault(value => value.Id == itemId);
        if (item is null) throw new EntityNotFoundException("Shopping-list item not found", null);

        db.ShoppingListItems.Remove(item);
        await db.SaveChangesAsync();
        return TypedResults.Ok();
    }
}
