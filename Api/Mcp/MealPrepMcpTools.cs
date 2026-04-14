using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Api.Authentication;
using Api.Configuration;
using Api.Data;
using Api.Domain;
using Api.Endpoints;
using Api.Endpoints.Requests.MealPrep;
using Api.Endpoints.Responses.MealPrep;
using Api.Models;
using Api.Models.Filter;
using Api.Services;
using Api.Services.MealPrep;
using FluentValidation;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Api.Mcp;

/// <summary>
///     MCP tools that delegate to the same minimal-API handlers as the REST surface. Personal access tokens are scoped to one workspace; the workspace is taken from the token, not from tool arguments.
/// </summary>
public sealed class MealPrepMcpTools(
    CurrentUserService currentUserService,
    ApiDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IFilterConfigurationProvider filterConfigurationProvider,
    RecipeImportService recipeImportService,
    ShoppingListGenerationService shoppingListGenerationService,
    MeasurementService measurementService,
    RecipeImageProcessingService recipeImageProcessingService,
    IS3StorageService s3StorageService,
    ILogger<MealPrepMcpTools> logger,
    ILoggerFactory loggerFactory
)
{
    private static string Serialize<T>(JsonHttpResult<T> result) {
        return JsonSerializer.Serialize(result.Value, McpJson.SerializerOptions);
    }

    private DefaultHttpContext BuildQueryHttpContext(IEnumerable<KeyValuePair<string, string?>> queryParams) {
        var inner = httpContextAccessor.HttpContext!;
        var ctx = new DefaultHttpContext { User = inner.User, RequestServices = inner.RequestServices };
        var query = new Dictionary<string, StringValues>();
        foreach (var (key, value) in queryParams) {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            query[key] = value;
        }

        var parsed = new QueryCollection(query);
        ctx.Request.Query = parsed;
        return ctx;
    }

    private Guid RequireMcpWorkspaceId() {
        var raw = httpContextAccessor.HttpContext?.User.FindFirstValue(McpPatClaims.WorkspaceId);
        if (raw is not null && Guid.TryParse(raw, out var id))
            return id;
        throw new UnauthorizedException();
    }

    private async Task ValidateRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken) {
        var services = httpContextAccessor.HttpContext?.RequestServices
                       ?? throw new InvalidOperationException("MCP request has no service provider.");
        var validator = services.GetRequiredService<IValidator<TRequest>>();
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
            return;
        throw new RequestValidationException(result.ToDictionary());
    }

    private static string BuildValidationErrorResponse(string field, string message) {
        var errors = new Dictionary<string, string[]> { [field] = [message] };
        var body = new ExtendedProblemDetail(
            new ProblemDetails {
                Title = "Request validation failed",
                Type = $"https://localhost:5000/errors/{nameof(RequestValidationException)}",
                Status = StatusCodes.Status400BadRequest,
                Detail = "One or more validation errors occurred."
            },
            errors
        );
        return JsonSerializer.Serialize(body, McpJson.SerializerOptions);
    }

    private static string SerializeAppException(AppException exception) {
        var details = exception.Details;
        // Serialize using the runtime type so ExtendedProblemDetail.Errors is included (Details is typed as ProblemDetails).
        return JsonSerializer.Serialize(details, details.GetType(), McpJson.SerializerOptions);
    }

    private static string BuildUnhandledErrorResponse(string toolName, string? errorDetail = null) {
        var body = new ProblemDetails {
            Title = "MCP tool execution failed",
            Type = "https://localhost:5000/errors/McpToolExecutionException",
            Status = StatusCodes.Status500InternalServerError,
            Detail = errorDetail ?? $"The MCP tool '{toolName}' failed. See server logs for details."
        };
        return JsonSerializer.Serialize(body, McpJson.SerializerOptions);
    }

    private async Task<string> ExecuteToolWithErrorLoggingAsync(string toolName, Func<Task<string>> action) {
        try {
            return await action();
        } catch (OperationCanceledException) {
            logger.LogWarning("MCP tool {ToolName} was canceled.", toolName);
            throw;
        } catch (AppException appException) {
            logger.LogWarning(appException, "MCP tool {ToolName} failed with an application error.", toolName);
            return SerializeAppException(appException);
        } catch (Exception exception) {
            logger.LogError(exception, "MCP tool {ToolName} failed.", toolName);
            return BuildUnhandledErrorResponse(toolName, $"{toolName} failed: {exception.Message}");
        }
    }

    [McpServerTool]
    [Description("Returns the authenticated user profile and workspace memberships (read-only).")]
    public async Task<string> GetCurrentUser(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var result = await AuthHandlers.GetMe(currentUserService, db);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Returns the workspace this MCP token is scoped to (read-only).")]
    public async Task<string> ListWorkspaces(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var single = await WorkspacesHandlers.GetWorkspace(currentUserService, db, workspaceId);
        return JsonSerializer.Serialize(new[] { single.Value }, McpJson.SerializerOptions);
    }

    [McpServerTool]
    [Description(
        "Lists recipes in the token's workspace with optional paging and sorting."
    )]
    public async Task<string> ListRecipes(
        [Description("1-based page index. Omit for default paging.")]
        int? page = null,
        [Description("Number of results per page.")]
        int? pageSize = null,
        [Description("Sort field, e.g. createdAt, updatedAt, title.")]
        string? orderBy = null,
        [Description("Sort direction: asc or desc.")]
        string? direction = null,
        [Description("Include archived recipes when true.")]
        bool? includeArchived = null,
        CancellationToken cancellationToken = default
    ) {
        var normalizedOrderBy = string.IsNullOrWhiteSpace(orderBy) ? null : orderBy.Trim();
        var normalizedDirection = string.IsNullOrWhiteSpace(direction) ? null : direction.Trim().ToLowerInvariant();
        if (normalizedDirection is not null && normalizedDirection is not ("asc" or "desc"))
            return BuildValidationErrorResponse(nameof(direction), "Must be 'asc' or 'desc'.");

        var workspaceId = RequireMcpWorkspaceId();
        var queryParams = new List<KeyValuePair<string, string?>> {
            new("page", page?.ToString()),
            new("pageSize", pageSize?.ToString()),
            new("orderBy", normalizedOrderBy),
            new("direction", normalizedDirection),
            new("includeArchived", includeArchived?.ToString().ToLowerInvariant()),
        };
        var httpContext = BuildQueryHttpContext(queryParams);
        try {
            var result = await RecipesHandlers.GetRecipes(
                currentUserService,
                db,
                filterConfigurationProvider,
                httpContext,
                workspaceId,
                cancellationToken
            );
            return Serialize(result);
        } catch (AppException appException) {
            return SerializeAppException(appException);
        }
    }

    [McpServerTool]
    [Description("Gets a recipe by id.")]
    public async Task<string> GetRecipe(Guid recipeId, CancellationToken cancellationToken) {
        var workspaceId = RequireMcpWorkspaceId();
        var result = await RecipesHandlers.GetRecipe(currentUserService, db, workspaceId, recipeId, cancellationToken);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Creates a recipe.")]
    public async Task<string> CreateRecipe(
        [Description("Recipe title.")] string title,
        [Description("Optional recipe description.")]
        string? description,
        [Description("Number of servings this recipe makes.")]
        decimal servings,
        [Description("Optional source URL for the recipe.")]
        string? sourceUrl,
        [Description("Optional free-form notes.")]
        string? notes,
        [Description("Optional prep time in minutes.")]
        int? prepMinutes,
        [Description("Optional cook time in minutes.")]
        int? cookMinutes,
        [Description("Whether the recipe should be archived.")]
        bool isArchived,
        [Description(
            "Recipe tags from the app whitelist only (kebab-case), e.g. dinner, breakfast, eggs, spicy, dessert, quick, vegetarian."
        )]
        string[] tags,
        [Description("Recipe ingredients.")] SaveRecipeIngredientRequest[] ingredients,
        [Description("Recipe instructions/steps.")]
        SaveRecipeStepRequest[] steps,
        [Description("Optional nutrition block.")]
        SaveRecipeNutritionRequest? nutrition,
        [Description("Optional imported image URL (must be http/https).")]
        string? importImageUrl,
        CancellationToken cancellationToken
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(CreateRecipe),
            async () => {
                var workspaceId = RequireMcpWorkspaceId();
                var recipe = new SaveRecipeRequest(
                    title,
                    description,
                    servings,
                    sourceUrl,
                    notes,
                    prepMinutes,
                    cookMinutes,
                    isArchived,
                    tags,
                    ingredients,
                    steps,
                    nutrition,
                    importImageUrl
                );
                await ValidateRequestAsync(recipe, cancellationToken);
                var result = await RecipesHandlers.PostRecipe(
                    currentUserService,
                    db,
                    recipeImportService,
                    recipeImageProcessingService,
                    s3StorageService,
                    workspaceId,
                    recipe,
                    cancellationToken
                );
                return Serialize(result);
            }
        );
    }

    [McpServerTool]
    [Description("Updates a recipe by id.")]
    public async Task<string> UpdateRecipe(
        [Description("Recipe id to update.")] Guid recipeId,
        [Description("Recipe title.")] string title,
        [Description("Optional recipe description.")]
        string? description,
        [Description("Number of servings this recipe makes.")]
        decimal servings,
        [Description("Optional source URL for the recipe.")]
        string? sourceUrl,
        [Description("Optional free-form notes.")]
        string? notes,
        [Description("Optional prep time in minutes.")]
        int? prepMinutes,
        [Description("Optional cook time in minutes.")]
        int? cookMinutes,
        [Description("Whether the recipe should be archived.")]
        bool isArchived,
        [Description(
            "Recipe tags from the app whitelist only (kebab-case), e.g. dinner, breakfast, eggs, spicy, dessert, quick, vegetarian."
        )]
        string[] tags,
        [Description("Recipe ingredients.")] SaveRecipeIngredientRequest[] ingredients,
        [Description("Recipe instructions/steps.")]
        SaveRecipeStepRequest[] steps,
        [Description("Optional nutrition block.")]
        SaveRecipeNutritionRequest? nutrition,
        [Description("Optional imported image URL (must be http/https).")]
        string? importImageUrl,
        CancellationToken cancellationToken
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(UpdateRecipe),
            async () => {
                var workspaceId = RequireMcpWorkspaceId();
                var recipe = new SaveRecipeRequest(
                    title,
                    description,
                    servings,
                    sourceUrl,
                    notes,
                    prepMinutes,
                    cookMinutes,
                    isArchived,
                    tags,
                    ingredients,
                    steps,
                    nutrition,
                    importImageUrl
                );
                await ValidateRequestAsync(recipe, cancellationToken);

                var result = await RecipesHandlers.PatchRecipe(
                    currentUserService,
                    db,
                    recipeImportService,
                    recipeImageProcessingService,
                    s3StorageService,
                    loggerFactory,
                    workspaceId,
                    recipeId,
                    recipe,
                    cancellationToken
                );
                return Serialize(result);
            }
        );
    }

    [McpServerTool]
    [Description("Sets a recipe image from a public image URL.")]
    public async Task<string> SetRecipeImageFromUrl(
        [Description("Recipe id to update.")] Guid recipeId,
        [Description("Public image URL to import for this recipe.")]
        string imageUrl,
        CancellationToken cancellationToken
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(SetRecipeImageFromUrl),
            async () => {
                var workspaceId = RequireMcpWorkspaceId();
                var workspaceUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
                if (workspaceUser is null)
                    throw new EntityNotFoundException("workspace not found", null);

                var recipe = await db.Recipes
                    .ForCurrentUser(workspaceUser.UserId)
                    .WhereIsNotDeleted()
                    .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (recipe is null)
                    throw new EntityNotFoundException("Recipe not found", null);

                var payload = await recipeImportService.TryDownloadImportImageAsync(imageUrl, cancellationToken);
                if (payload is null)
                    throw new InvalidFormatException(
                        "Image import failed",
                        "Could not download a valid image from the provided URL."
                    );

                if (!string.IsNullOrEmpty(recipe.ImageObjectKey))
                    await s3StorageService.DeleteFileAsync(recipe.ImageObjectKey);

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
                recipe.SetImageObjectKey(key);
                await db.SaveChangesAsync(cancellationToken);

                var updatedRecipe = await db.Recipes
                    .Include(value => value.Ingredients)
                    .Include(value => value.Steps)
                    .Include(value => value.Nutrition)
                    .AsNoTracking()
                    .ForCurrentUser(workspaceUser.UserId)
                    .WhereIsNotDeleted()
                    .Where(value => value.WorkspaceId == workspaceId && value.Id == recipeId)
                    .FirstAsync(cancellationToken);

                var isFavorite = await db.RecipeFavorites.AsNoTracking()
                    .AnyAsync(
                        favorite => favorite.UserId == workspaceUser.UserId && favorite.RecipeId == recipeId,
                        cancellationToken
                    );

                return JsonSerializer.Serialize(
                    updatedRecipe.ToRecipeResponse(isFavorite),
                    McpJson.SerializerOptions
                );
            }
        );
    }

    [McpServerTool]
    [Description("Soft-deletes a recipe.")]
    public async Task<string> DeleteRecipe(Guid recipeId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await RecipesHandlers.DeleteRecipe(currentUserService, db, s3StorageService, workspaceId, recipeId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Imports a recipe from a source URL in one step.")]
    public async Task<string> ImportRecipe(
        [Description("Public recipe URL to import.")]
        string sourceUrl,
        CancellationToken cancellationToken
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(ImportRecipe),
            async () => {
                var workspaceId = RequireMcpWorkspaceId();
                var body = new ImportRecipeRequest(sourceUrl);
                await ValidateRequestAsync(body, cancellationToken);
                var result = await RecipesHandlers.PostImportRecipe(
                    currentUserService,
                    db,
                    recipeImportService,
                    recipeImageProcessingService,
                    s3StorageService,
                    workspaceId,
                    body,
                    cancellationToken
                );
                return Serialize(result);
            }
        );
    }

    [McpServerTool]
    [Description(
        "Lists next meals. Optionally pass optionalPlannedFrom and optionalPlannedTo as yyyy-MM-dd to filter by target date."
    )]
    public async Task<string> ListNextMeals(
        [Description("Optional start date (yyyy-MM-dd).")]
        string? optionalPlannedFrom,
        [Description("Optional end date (yyyy-MM-dd).")]
        string? optionalPlannedTo,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        DateOnly? from = null;
        DateOnly? to = null;
        if (!string.IsNullOrWhiteSpace(optionalPlannedFrom) && DateOnly.TryParse(optionalPlannedFrom, out var f))
            from = f;
        if (!string.IsNullOrWhiteSpace(optionalPlannedTo) && DateOnly.TryParse(optionalPlannedTo, out var t))
            to = t;

        var result = await MealPlanEntriesHandlers.GetMealPlanEntries(currentUserService, db, workspaceId, from, to);
        return Serialize(result);
    }

    [McpServerTool]
    [Description(
        "Creates or updates a next meal. Pass nextMealId to update; otherwise pass null to create."
    )]
    public async Task<string> PutNextMeal(
        [Description("Next-meal id to update. Omit/null to create a new entry.")]
        Guid? nextMealId,
        [Description("Recipe id for the next meal.")]
        Guid recipeId,
        [Description("Target date in yyyy-MM-dd format.")]
        string plannedDate,
        [Description("Meal type value (must match allowed meal types).")]
        string mealType,
        [Description("Optional target servings.")]
        decimal? targetServings,
        [Description("Optional notes.")] string? notes,
        [Description("Status value (must match allowed meal-plan statuses).")]
        string status,
        [Description("Optional completion timestamp in UTC (ISO-8601). If omitted and status is completed, server sets current UTC time.")]
        string? completedAtUtc,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        if (!DateOnly.TryParse(plannedDate, out var parsedPlannedDate))
            throw new InvalidFormatException("plannedDate must be a valid date in yyyy-MM-dd format.", null);
        DateTime? parsedCompletedAtUtc = null;
        if (!string.IsNullOrWhiteSpace(completedAtUtc)) {
            if (!DateTime.TryParse(completedAtUtc, out var parsed))
                throw new InvalidFormatException("completedAtUtc must be a valid ISO-8601 UTC datetime.", null);
            parsedCompletedAtUtc = parsed.ToUniversalTime();
        }
        var entry = new SaveMealPlanEntryRequest(
            recipeId,
            parsedPlannedDate,
            mealType,
            targetServings,
            notes,
            status,
            parsedCompletedAtUtc
        );
        await ValidateRequestAsync(entry, cancellationToken);

        if (nextMealId is null) {
            var createResult = await MealPlanEntriesHandlers.PostMealPlanEntry(
                currentUserService,
                db,
                workspaceId,
                entry
            );
            return Serialize(createResult);
        }

        var result = await MealPlanEntriesHandlers.PatchMealPlanEntry(
            currentUserService,
            db,
            workspaceId,
            nextMealId.Value,
            entry
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes a next meal entry.")]
    public async Task<string> DeleteNextMeal(Guid nextMealId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await MealPlanEntriesHandlers.DeleteMealPlanEntry(currentUserService, db, workspaceId, nextMealId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Lists shopping lists in the token's workspace.")]
    public async Task<string> ListShoppingLists(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var result = await ShoppingListsHandlers.GetShoppingLists(currentUserService, db, workspaceId);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Gets a shopping list with items and sources.")]
    public async Task<string> GetShoppingList(
        [Description("Shopping list id (UUID).")]
        string shoppingListId,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        if (!Guid.TryParse(shoppingListId, out var parsedShoppingListId))
            return BuildValidationErrorResponse(nameof(shoppingListId), "Must be a valid UUID.");

        var workspaceId = RequireMcpWorkspaceId();
        var result = await ShoppingListsHandlers.GetShoppingList(
            currentUserService,
            db,
            workspaceId,
            parsedShoppingListId
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Generates a shopping list from selected recipes and/or next meals.")]
    public async Task<string> GenerateShoppingList(
        [Description("Shopping list name.")] string name,
        [Description("Optional shopping list notes.")]
        string? notes,
        [Description("Recipe ids to include.")]
        Guid[] recipeIds,
        [Description("Next-meal ids to include.")]
        Guid[] nextMealIds,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var request = new GenerateShoppingListRequest(name, notes, recipeIds, nextMealIds);
        await ValidateRequestAsync(request, cancellationToken);
        var result = await ShoppingListsHandlers.PostGenerateShoppingList(
            currentUserService,
            db,
            shoppingListGenerationService,
            workspaceId,
            request,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates shopping list metadata by id.")]
    public async Task<string> UpdateShoppingList(
        [Description("Shopping list id to update.")]
        Guid shoppingListId,
        [Description("Shopping list name.")] string name,
        [Description("Optional shopping list notes.")]
        string? notes,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var request = new SaveShoppingListRequest(name, notes);
        await ValidateRequestAsync(request, cancellationToken);
        var result = await ShoppingListsHandlers.PatchShoppingList(
            currentUserService,
            db,
            workspaceId,
            shoppingListId,
            request
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes a shopping list.")]
    public async Task<string> DeleteShoppingList(Guid shoppingListId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await ShoppingListsHandlers.DeleteShoppingList(currentUserService, db, workspaceId, shoppingListId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Adds an item to a shopping list.")]
    public async Task<string> CreateShoppingListItem(
        [Description("Shopping list id that will contain this item.")]
        Guid shoppingListId,
        [Description("Display name for the item.")]
        string name,
        [Description("Optional normalized ingredient name.")]
        string? normalizedIngredientName,
        [Description("Optional numeric amount.")]
        decimal? amount,
        [Description("Optional unit, e.g. g, oz, cup.")]
        string? unit,
        [Description("Whether the amount is approximate.")]
        bool isApproximate,
        [Description("Whether the item is checked/completed.")]
        bool isChecked,
        [Description("Whether this item was manually added.")]
        bool isManual,
        [Description("Optional category, e.g. Produce.")]
        string? category,
        [Description("Optional note.")] string? note,
        [Description("Primary display text for the item.")]
        string displayText,
        [Description("Optional source names that contributed to this item.")]
        string[]? sourceNames,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var item = new SaveShoppingListItemRequest(
            name,
            normalizedIngredientName,
            amount,
            unit,
            isApproximate,
            isChecked,
            isManual,
            category,
            note,
            displayText,
            sourceNames
        );
        await ValidateRequestAsync(item, cancellationToken);
        var result = await ShoppingListsHandlers.PostShoppingListItem(
            currentUserService,
            db,
            measurementService,
            workspaceId,
            shoppingListId,
            item
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates a shopping list item by id.")]
    public async Task<string> UpdateShoppingListItem(
        [Description("Shopping list id containing the item.")]
        Guid shoppingListId,
        [Description("Shopping list item id to update.")]
        Guid itemId,
        [Description("Display name for the item.")]
        string name,
        [Description("Optional normalized ingredient name.")]
        string? normalizedIngredientName,
        [Description("Optional numeric amount.")]
        decimal? amount,
        [Description("Optional unit, e.g. g, oz, cup.")]
        string? unit,
        [Description("Whether the amount is approximate.")]
        bool isApproximate,
        [Description("Whether the item is checked/completed.")]
        bool isChecked,
        [Description("Whether this item was manually added.")]
        bool isManual,
        [Description("Optional category, e.g. Produce.")]
        string? category,
        [Description("Optional note.")] string? note,
        [Description("Primary display text for the item.")]
        string displayText,
        [Description("Optional source names that contributed to this item.")]
        string[]? sourceNames,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var item = new SaveShoppingListItemRequest(
            name,
            normalizedIngredientName,
            amount,
            unit,
            isApproximate,
            isChecked,
            isManual,
            category,
            note,
            displayText,
            sourceNames
        );
        await ValidateRequestAsync(item, cancellationToken);
        var result = await ShoppingListsHandlers.PatchShoppingListItem(
            currentUserService,
            db,
            measurementService,
            workspaceId,
            shoppingListId,
            itemId,
            item
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes an item from a shopping list.")]
    public async Task<string> DeleteShoppingListItem(
        Guid shoppingListId,
        Guid itemId,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await ShoppingListsHandlers.DeleteShoppingListItem(
            currentUserService,
            db,
            workspaceId,
            shoppingListId,
            itemId
        );
        return """{"ok":true}""";
    }
}
