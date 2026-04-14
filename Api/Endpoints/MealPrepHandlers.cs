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
using Microsoft.Extensions.Logging;

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
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var paginationOptions = PaginationOptions.FromQueryParams(httpContext.Request.Query);
        var includeArchived = paginationOptions.FilterOptions.Any(pair => pair.Key == "includeArchived"
                                                                          && bool.TryParse(
                                                                              pair.Value.ToString(),
                                                                              out var parsedValue
                                                                          )
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
        var userId = currentUserId.Value;

        var orderedQuery = query.OrderByDescending(recipe =>
            db.RecipeFavorites.Any(favorite => favorite.UserId == userId && favorite.RecipeId == recipe.Id));

        orderedQuery = paginationOptions.Direction == PaginationOptions.OrderDirections.Asc
            ? orderedQuery.ThenBy(recipe => recipe.Title)
            : orderedQuery.ThenByDescending(recipe => recipe.Title);

        var skip = (paginationOptions.Page - 1) * paginationOptions.PageSize;
        var recipes = await orderedQuery
            .Skip(skip)
            .Take(paginationOptions.PageSize)
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
                    recipe.ImageObjectKey != null && recipe.ImageObjectKey != "",
                    db.RecipeFavorites.Any(favorite => favorite.UserId == userId && favorite.RecipeId == recipe.Id)
                )
            )
            .ToArrayAsync(cancellationToken);

        return TypedResults.Json(
            PaginatedResponse<RecipeListItemResponse>.ToPaginatedResponse(recipes, paginationOptions, totalCount)
        );
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeTagListResponse>> GetRecipeTagWhitelist(
        CurrentUserService currentUserService,
        Guid workspaceId
    ) {
        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        return TypedResults.Json(new RecipeTagListResponse(RecipeTagWhitelist.AllSorted));
    }

    [Authorize]
    public static async Task<JsonHttpResult<SuggestRecipeTagsResponse>> PostSuggestRecipeTags(
        CurrentUserService currentUserService,
        Guid workspaceId,
        RecipeTagSuggestionService tagSuggestionService,
        [FromBody] SuggestRecipeTagsRequest body,
        CancellationToken cancellationToken
    ) {
        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        if (!tagSuggestionService.IsConfigured)
            throw new InvalidFormatException("Recipe tag suggestions are unavailable", "OpenAI API is not configured.");

        var suggested = await tagSuggestionService.TrySuggestTagsAsync(
            body.Title,
            body.Description,
            body.IngredientNames ?? [],
            body.StepInstructions ?? [],
            cancellationToken
        );

        if (suggested is null)
            throw new InvalidFormatException(
                "Recipe tag suggestions failed",
                "The model did not return usable tags. Try again."
            );

        return TypedResults.Json(new SuggestRecipeTagsResponse(suggested));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeTagUsageListResponse>> GetRecipeTagUsage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var items = await LoadRecipeTagUsageAsync(db, currentUserId.Value, workspaceId, cancellationToken);

        return TypedResults.Json(new RecipeTagUsageListResponse(items));
    }

    [Authorize]
    public static async Task<JsonHttpResult<BulkRemoveRecipeTagsResponse>> PostBulkRemoveRecipeTags(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        [FromBody] BulkRemoveRecipeTagsRequest body,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var distinct = body.Tags.Distinct(StringComparer.Ordinal).ToArray();
        var result = await BulkRemoveTagsFromWorkspaceRecipesAsync(
            db,
            currentUserId.Value,
            workspaceId,
            distinct,
            cancellationToken
        );

        return TypedResults.Json(result);
    }

    [Authorize]
    public static async Task<JsonHttpResult<BulkRemoveRecipeTagsResponse>> PostRemoveSingletonRecipeTags(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var usage = await LoadRecipeTagUsageAsync(db, currentUserId.Value, workspaceId, cancellationToken);
        var singletons = usage.Where(item => item.RecipeCount == 1).Select(item => item.Tag).ToArray();

        if (singletons.Length == 0)
            return TypedResults.Json(new BulkRemoveRecipeTagsResponse(0, Array.Empty<string>()));

        var result = await BulkRemoveTagsFromWorkspaceRecipesAsync(
            db,
            currentUserId.Value,
            workspaceId,
            singletons,
            cancellationToken
        );

        return TypedResults.Json(result);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> GetRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .Include(value => value.CollectionLinks)
            .ThenInclude(link => link.RecipeCollection)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        var isFavorite = await RecipeIsFavoriteAsync(db, currentUserId.Value, recipeId, cancellationToken);
        var collections = recipe.CollectionLinks
            .Where(link => !link.RecipeCollection.IsDeleted)
            .OrderBy(link => link.RecipeCollection.Name)
            .Select(link => new RecipeCollectionMembershipResponse(
                    link.RecipeCollectionId,
                    link.RecipeCollection.Name,
                    link.RecipeCollection.WorkspaceId,
                    link.RecipeCollection.WorkspaceId == workspaceId
                )
            )
            .ToArray();

        return TypedResults.Json(recipe.ToRecipeResponse(isFavorite, collections));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostAutotagRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeTagSuggestionService tagSuggestionService,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        if (!tagSuggestionService.IsConfigured)
            throw new InvalidFormatException(
                "Recipe auto-tagging is unavailable",
                "OpenAI API is not configured."
            );

        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId.Value)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

        var currentCanonical = RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags);
        var ingredientNames = recipe.Ingredients
            .OrderBy(ingredient => ingredient.SortOrder)
            .Select(ingredient => ingredient.Name)
            .ToList();
        var stepInstructions = recipe.Steps
            .OrderBy(step => step.SortOrder)
            .Select(step => step.Instruction)
            .ToList();

        var suggested = await tagSuggestionService.TrySuggestTagsAsync(
            recipe.Title,
            recipe.Description,
            ingredientNames,
            stepInstructions,
            cancellationToken,
            currentCanonical
        );

        if (suggested is null)
            throw new InvalidFormatException(
                "Auto-tag failed",
                "The model did not return usable tags. Try again."
            );

        recipe.UpdateDetails(
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.SourceUrl,
            recipe.Notes,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            recipe.IsArchived,
            suggested
        );

        await db.SaveChangesAsync(cancellationToken);

        var isFavorite = await RecipeIsFavoriteAsync(db, currentUserId.Value, recipeId, cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse(isFavorite));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImportService recipeImportService,
        RecipeImageProcessingService recipeImageProcessingService,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        [FromBody] SaveRecipeRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipe = Recipe.CreateNew(workspaceUser.Workspace, body.Title, body.Servings);
        ApplyRecipe(recipe, body);
        await TryApplyImportedImageAsync(
            body,
            recipe,
            recipeImportService,
            recipeImageProcessingService,
            s3StorageService,
            cancellationToken
        );

        await db.Recipes.AddAsync(recipe);
        await db.SaveChangesAsync(cancellationToken);

        recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .FirstAsync(value => value.Id == recipe.Id, cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse(isFavorite: false));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PatchRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImportService recipeImportService,
        RecipeImageProcessingService recipeImageProcessingService,
        IS3StorageService s3StorageService,
        ILoggerFactory loggerFactory,
        Guid workspaceId,
        Guid recipeId,
        [FromBody] SaveRecipeRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var currentUserId = workspaceUser.UserId;
        var recipeQuery = db.Recipes
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId);

        var normalizedTags = RecipeTagWhitelist.NormalizeToWhitelist(body.Tags);
        var updatedCount = await recipeQuery.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(value => value.Title, body.Title)
                .SetProperty(value => value.Description, body.Description)
                .SetProperty(value => value.Servings, body.Servings)
                .SetProperty(value => value.SourceUrl, body.SourceUrl)
                .SetProperty(value => value.Notes, body.Notes)
                .SetProperty(value => value.PrepMinutes, body.PrepMinutes)
                .SetProperty(value => value.CookMinutes, body.CookMinutes)
                .SetProperty(value => value.IsArchived, body.IsArchived)
                .SetProperty(value => value.Tags, normalizedTags)
                .SetProperty(value => value.UpdatedAt, DateTime.UtcNow),
            cancellationToken
        );

        if (updatedCount == 0) throw new EntityNotFoundException("Recipe not found", null);

        await db.RecipeIngredients
            .Where(value => value.RecipeId == recipeId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.RecipeSteps
            .Where(value => value.RecipeId == recipeId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.RecipeNutrition
            .Where(value => value.RecipeId == recipeId)
            .ExecuteDeleteAsync(cancellationToken);

        var newIngredients = body.Ingredients
            .Select((ingredient, index) => RecipeIngredient.CreateNew(
                    index,
                    ingredient.Name,
                    ingredient.DisplayText,
                    ingredient.Amount,
                    ingredient.Unit,
                    ingredient.NormalizedIngredientName,
                    ingredient.PreparationNote,
                    ingredient.Section
                )
            )
            .ToArray();

        var newSteps = body.Steps
            .Select((step, index) => RecipeStep.CreateNew(index, step.Instruction, step.TimerSeconds))
            .ToArray();

        var newNutrition = body.Nutrition?.Nutrients is { Length: > 0 } nutrients
            ? nutrients
                .GroupBy(nutrient => nutrient.NutrientType.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => RecipeNutrition.CreateNew(group.Key, group.Last().Amount))
                .ToArray()
            : Array.Empty<RecipeNutrition>();

        foreach (var ingredient in newIngredients)
            db.Entry(ingredient).Property(nameof(RecipeIngredient.RecipeId)).CurrentValue = recipeId;
        foreach (var step in newSteps)
            db.Entry(step).Property(nameof(RecipeStep.RecipeId)).CurrentValue = recipeId;
        foreach (var nutrient in newNutrition)
            db.Entry(nutrient).Property(nameof(RecipeNutrition.RecipeId)).CurrentValue = recipeId;

        await db.RecipeIngredients.AddRangeAsync(newIngredients, cancellationToken);
        await db.RecipeSteps.AddRangeAsync(newSteps, cancellationToken);
        await db.RecipeNutrition.AddRangeAsync(newNutrition, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(body.ImportImageUrl)) {
            var payload = await recipeImportService.TryDownloadImportImageAsync(
                body.ImportImageUrl,
                cancellationToken
            );
            if (payload is not null) {
                var existingImageKey = await recipeQuery.Select(value => value.ImageObjectKey)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrEmpty(existingImageKey))
                    await s3StorageService.DeleteFileAsync(existingImageKey);

                await using var stream = new MemoryStream(payload.Data);
                var optimized = await recipeImageProcessingService.OptimizeForWebAsync(
                    stream,
                    payload.FileName,
                    cancellationToken
                );
                await using var optimizedStream = new MemoryStream(optimized.Data);
                var key = await s3StorageService.UploadFileAsync(
                    optimizedStream,
                    optimized.FileName,
                    optimized.ContentType
                );
                await recipeQuery.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(value => value.ImageObjectKey, key)
                        .SetProperty(value => value.UpdatedAt, DateTime.UtcNow),
                    cancellationToken
                );
            }
        }

        var updatedRecipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstAsync(cancellationToken);

        var isFavorite = await RecipeIsFavoriteAsync(db, currentUserId, recipeId, cancellationToken);

        return TypedResults.Json(updatedRecipe.ToRecipeResponse(isFavorite));
    }

    [Authorize]
    public static async Task<Ok> DeleteRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId
    ) {
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
    ) {
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
        var contentType = RecipeImageUploadConstants.ContentTypeFromObjectKey(recipe.ImageObjectKey)
                          ?? "application/octet-stream";

        return TypedResults.File(stream, contentType);
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostRecipeImage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImageProcessingService recipeImageProcessingService,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        IFormFile? file,
        CancellationToken cancellationToken
    ) {
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
            throw new InvalidFormatException(
                "Image file is too large",
                $"Maximum size is {RecipeImageUploadConstants.MaxBytes} bytes."
            );
        }

        if (!RecipeImageUploadConstants.IsAllowedContentType(file.ContentType)) {
            throw new InvalidFormatException("Unsupported image type", "Use JPEG, PNG, WebP, or GIF.");
        }

        await using var readStream = file.OpenReadStream();
        var optimized = await recipeImageProcessingService.OptimizeForWebAsync(
            readStream,
            file.FileName,
            cancellationToken
        );
        await using var optimizedStream = new MemoryStream(optimized.Data);
        var newKey = await s3StorageService.UploadFileAsync(
            optimizedStream,
            optimized.FileName,
            optimized.ContentType
        );

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) {
            await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);
        }

        recipe.SetImageObjectKey(newKey);
        await db.SaveChangesAsync(cancellationToken);

        var isFavorite = await RecipeIsFavoriteAsync(db, currentUserId, recipeId, cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse(isFavorite));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> DeleteRecipeImage(
        CurrentUserService currentUserService,
        ApiDbContext db,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
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

        var isFavorite = await RecipeIsFavoriteAsync(db, currentUserId, recipeId, cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse(isFavorite));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PatchRecipeFavorite(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid recipeId,
        [FromBody] SetRecipeFavoriteRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var userId = workspaceUser.UserId;
        var recipeExists = await db.Recipes
            .AsNoTracking()
            .ForCurrentUser(userId)
            .WhereIsNotDeleted()
            .AnyAsync(recipe => recipe.WorkspaceId == workspaceId && recipe.Id == recipeId, cancellationToken);

        if (!recipeExists) throw new EntityNotFoundException("Recipe not found", null);

        if (body.IsFavorite) {
            var already = await db.RecipeFavorites.AnyAsync(
                favorite => favorite.UserId == userId && favorite.RecipeId == recipeId,
                cancellationToken
            );

            if (!already) {
                db.RecipeFavorites.Add(new RecipeFavorite { UserId = userId, RecipeId = recipeId });
                await db.SaveChangesAsync(cancellationToken);
            }
        } else {
            await db.RecipeFavorites
                .Where(favorite => favorite.UserId == userId && favorite.RecipeId == recipeId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var recipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .AsNoTracking()
            .ForCurrentUser(userId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
            .FirstAsync(cancellationToken);

        return TypedResults.Json(recipe.ToRecipeResponse(body.IsFavorite));
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeImportPreviewResponse>> PostImportPreview(
        CurrentUserService currentUserService,
        Guid workspaceId,
        RecipeImportService recipeImportService,
        [FromBody] ImportRecipePreviewRequest body,
        CancellationToken cancellationToken
    ) {
        if (await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId) is null)
            throw new EntityNotFoundException("workspace not found", null);

        var preview = await recipeImportService.PreviewAsync(
            body.Url,
            workspaceId,
            currentUserService.UserId,
            cancellationToken
        );
        return TypedResults.Json(preview.ToResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<RecipeResponse>> PostImportRecipe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        RecipeImportService recipeImportService,
        RecipeImageProcessingService recipeImageProcessingService,
        IS3StorageService s3StorageService,
        Guid workspaceId,
        [FromBody] ImportRecipeRequest body,
        CancellationToken cancellationToken
    ) {
        var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (workspaceUser is null) throw new EntityNotFoundException("workspace not found", null);

        var preview = await recipeImportService.PreviewAsync(
            body.Url,
            workspaceId,
            workspaceUser.UserId,
            cancellationToken
        );
        var saveRequest = ToSaveRecipeRequest(preview);

        var recipe = Recipe.CreateNew(workspaceUser.Workspace, saveRequest.Title, saveRequest.Servings);
        ApplyRecipe(recipe, saveRequest);
        await TryApplyImportedImageAsync(
            saveRequest,
            recipe,
            recipeImportService,
            recipeImageProcessingService,
            s3StorageService,
            cancellationToken
        );

        await db.Recipes.AddAsync(recipe, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var importedRecipe = await db.Recipes
            .Include(value => value.Ingredients)
            .Include(value => value.Steps)
            .Include(value => value.Nutrition)
            .ForCurrentUser(workspaceUser.UserId)
            .WhereIsNotDeleted()
            .FirstAsync(value => value.Id == recipe.Id, cancellationToken);

        return TypedResults.Json(importedRecipe.ToRecipeResponse(isFavorite: false));
    }

    private static async Task TryApplyImportedImageAsync(
        SaveRecipeRequest body,
        Recipe recipe,
        RecipeImportService recipeImportService,
        RecipeImageProcessingService recipeImageProcessingService,
        IS3StorageService s3StorageService,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(body.ImportImageUrl)) return;

        var payload = await recipeImportService.TryDownloadImportImageAsync(
            body.ImportImageUrl,
            cancellationToken
        );

        if (payload is null) return;

        if (!string.IsNullOrEmpty(recipe.ImageObjectKey)) await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);

        await using var stream = new MemoryStream(payload.Data);
        var optimized = await recipeImageProcessingService.OptimizeForWebAsync(
            stream,
            payload.FileName,
            cancellationToken
        );
        await using var optimizedStream = new MemoryStream(optimized.Data);
        var key = await s3StorageService.UploadFileAsync(optimizedStream, optimized.FileName, optimized.ContentType);
        recipe.SetImageObjectKey(key);
    }

    private static SaveRecipeRequest ToSaveRecipeRequest(RecipeImportPreview preview) {
        return new SaveRecipeRequest(
            preview.Title,
            preview.Description,
            preview.Servings,
            preview.SourceUrl,
            null,
            preview.PrepMinutes,
            preview.CookMinutes,
            false,
            preview.Tags.ToArray(),
            preview.Ingredients
                .OrderBy(ingredient => ingredient.SortOrder)
                .Select(ingredient => new SaveRecipeIngredientRequest(
                        ingredient.Name,
                        ingredient.NormalizedIngredientName,
                        ingredient.Amount,
                        ingredient.Unit,
                        ingredient.PreparationNote,
                        ingredient.Section,
                        ingredient.DisplayText
                    )
                )
                .ToArray(),
            preview.Steps
                .OrderBy(step => step.SortOrder)
                .Select(step => new SaveRecipeStepRequest(step.Instruction, step.TimerSeconds))
                .ToArray(),
            preview.Nutrition is null
                ? null
                : new SaveRecipeNutritionRequest(
                    preview.Nutrition.ServingBasis,
                    preview.Nutrition
                        .Nutrients
                        .Select(nutrient => new SaveRecipeNutrientRequest(nutrient.NutrientType, nutrient.Amount))
                        .ToArray()
                ),
            preview.ImageUrl
        );
    }

    private static async Task<RecipeTagUsageItemResponse[]> LoadRecipeTagUsageAsync(
        ApiDbContext db,
        Guid currentUserId,
        Guid workspaceId,
        CancellationToken cancellationToken
    )
    {
        var tagArrays = await db.Recipes
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(recipe => recipe.WorkspaceId == workspaceId)
            .Select(recipe => recipe.Tags)
            .ToArrayAsync(cancellationToken);

        return tagArrays
            .SelectMany(tags => tags)
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Select(group => new RecipeTagUsageItemResponse(group.Key, group.Count()))
            .OrderBy(item => item.RecipeCount)
            .ThenBy(item => item.Tag, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<BulkRemoveRecipeTagsResponse> BulkRemoveTagsFromWorkspaceRecipesAsync(
        ApiDbContext db,
        Guid currentUserId,
        Guid workspaceId,
        IReadOnlyList<string> exactTagsToRemove,
        CancellationToken cancellationToken
    )
    {
        var remove = exactTagsToRemove.ToHashSet(StringComparer.Ordinal);

        if (remove.Count == 0)
            return new BulkRemoveRecipeTagsResponse(0, Array.Empty<string>());

        var tagList = remove.ToList();

        var recipes = await db.Recipes
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(recipe => recipe.WorkspaceId == workspaceId)
            .Where(recipe => tagList.Any(tag => recipe.Tags.Contains(tag)))
            .ToListAsync(cancellationToken);

        var recipesUpdated = 0;

        foreach (var recipe in recipes)
        {
            var before = recipe.Tags.Length;
            recipe.RemoveTags(remove);

            if (recipe.Tags.Length != before)
                recipesUpdated++;
        }

        if (recipesUpdated > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new BulkRemoveRecipeTagsResponse(recipesUpdated, tagList.ToArray());
    }

    private static Task<bool> RecipeIsFavoriteAsync(
        ApiDbContext db,
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken
    ) {
        return db.RecipeFavorites.AsNoTracking()
            .AnyAsync(favorite => favorite.UserId == userId && favorite.RecipeId == recipeId, cancellationToken);
    }

    private static void ApplyRecipe(Recipe recipe, SaveRecipeRequest body) {
        recipe.UpdateDetails(
            body.Title,
            body.Description,
            body.Servings,
            body.SourceUrl,
            body.Notes,
            body.PrepMinutes,
            body.CookMinutes,
            body.IsArchived,
            RecipeTagWhitelist.NormalizeToWhitelist(body.Tags)
        );

        recipe.ReplaceIngredients(
            body.Ingredients.Select((ingredient, index) => RecipeIngredient.CreateNew(
                    index,
                    ingredient.Name,
                    ingredient.DisplayText,
                    ingredient.Amount,
                    ingredient.Unit,
                    ingredient.NormalizedIngredientName,
                    ingredient.PreparationNote,
                    ingredient.Section
                )
            )
        );

        recipe.ReplaceSteps(
            body.Steps.Select((step, index) => RecipeStep.CreateNew(index, step.Instruction, step.TimerSeconds))
        );

        recipe.SetNutrition(
            body.Nutrition?.ServingBasis,
            body.Nutrition?.Nutrients is { Length: > 0 } nutrients
                ? nutrients
                    .GroupBy(nutrient => nutrient.NutrientType.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => RecipeNutrition.CreateNew(group.Key, group.Last().Amount))
                    .ToArray()
                : Array.Empty<RecipeNutrition>()
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
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var entries = await db.MealPlanEntries
            .Include(entry => entry.Recipe)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(entry => entry.WorkspaceId == workspaceId)
            .Where(entry => from == null || entry.PlannedDate >= from.Value)
            .Where(entry => to == null || entry.PlannedDate <= to.Value)
            .OrderBy(entry => entry.Status == MealPlanEntryStatuses.Completed ? 1 : 0)
            .ThenBy(entry => entry.PlannedDate)
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
    ) {
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
        entry.Update(body.PlannedDate, body.MealType, body.TargetServings, body.Notes, body.Status, body.CompletedAtUtc);

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
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var entry = await db.MealPlanEntries
            .Include(value => value.Recipe)
            .ForCurrentUser(currentUserId)
            .WhereIsNotDeleted()
            .Where(value => value.WorkspaceId == workspaceId && value.Id == mealPlanEntryId)
            .FirstOrDefaultAsync();

        if (entry is null) throw new EntityNotFoundException("Meal-plan entry not found", null);

        if (entry.RecipeId != body.RecipeId) {
            var recipe = await db.Recipes
                .ForCurrentUser(currentUserId)
                .WhereIsNotDeleted()
                .Where(value => value.WorkspaceId == workspaceId && value.Id == body.RecipeId)
                .FirstOrDefaultAsync();

            if (recipe is null) throw new EntityNotFoundException("Recipe not found", null);

            entry.ChangeRecipe(recipe);
        }

        entry.Update(body.PlannedDate, body.MealType, body.TargetServings, body.Notes, body.Status, body.CompletedAtUtc);
        await db.SaveChangesAsync();

        return TypedResults.Json(entry.ToMealPlanEntryResponse());
    }

    [Authorize]
    public static async Task<Ok> DeleteMealPlanEntry(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid mealPlanEntryId
    ) {
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
    ) {
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
    ) {
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
    ) {
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
            .Where(entry => entry.WorkspaceId == workspaceId && body.NextMealIds.Contains(entry.Id))
            .ToArrayAsync();

        var sources = recipes.Select(recipe => new ShoppingListGenerationSource(
                    recipe.Id,
                    null,
                    recipe.Title,
                    recipe.Servings,
                    recipe.Servings,
                    recipe.Ingredients
                        .Select(ingredient => new ShoppingListGenerationIngredient(
                                ingredient.Name,
                                ingredient.NormalizedIngredientName,
                                ingredient.Amount,
                                ingredient.Unit,
                                ingredient.PreparationNote,
                                ingredient.Section
                            )
                        )
                        .ToArray()
                )
            )
            .Concat(
                entries.Select(entry => new ShoppingListGenerationSource(
                        entry.RecipeId,
                        entry.Id,
                        $"{entry.Recipe.Title} ({entry.PlannedDate:yyyy-MM-dd})",
                        entry.Recipe.Servings,
                        entry.TargetServings ?? entry.Recipe.Servings,
                        entry.Recipe
                            .Ingredients
                            .Select(ingredient => new ShoppingListGenerationIngredient(
                                    ingredient.Name,
                                    ingredient.NormalizedIngredientName,
                                    ingredient.Amount,
                                    ingredient.Unit,
                                    ingredient.PreparationNote,
                                    ingredient.Section
                                )
                            )
                            .ToArray()
                    )
                )
            )
            .ToArray();

        if (sources.Length == 0)
            throw new InvalidFormatException(
                "Shopping list generation failed",
                "No valid recipes or plan entries were selected."
            );

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
    ) {
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
    ) {
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
    ) {
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
            body.SourceNames ?? Array.Empty<string>()
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
            body.SourceNames ?? Array.Empty<string>()
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
    ) {
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
    ) {
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
