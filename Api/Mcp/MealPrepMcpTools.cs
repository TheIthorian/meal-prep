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
    RecipeImageStore recipeImageStore,
    McpWebLinks webLinks,
    ILogger<MealPrepMcpTools> logger,
    ILoggerFactory loggerFactory
)
{
    private static string Serialize<T>(JsonHttpResult<T> result) {
        return JsonSerializer.Serialize(result.Value, McpJson.SerializerOptions);
    }

    /// <summary>
    ///     Serializes a recipe (or a page of recipes) with a <c>webUrl</c> pointing at the web app,
    ///     so the caller can link the user straight to what it is talking about.
    /// </summary>
    private string SerializeWithRecipeLinks<T>(JsonHttpResult<T> result, Guid workspaceId) {
        return webLinks.WithRecipeLinks(Serialize(result), workspaceId);
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

    /// <summary>
    ///     Partial-update semantics for optional text: null/omitted keeps the stored value, an empty string clears it.
    /// </summary>
    private static string? MergeOptionalText(string? provided, string? existing) {
        if (provided is null)
            return existing;
        return string.IsNullOrWhiteSpace(provided) ? null : provided;
    }

    private static SaveRecipeIngredientRequest[] ToSaveRequests(RecipeIngredientResponse[] ingredients) {
        return ingredients
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
            .ToArray();
    }

    private static SaveRecipeStepRequest[] ToSaveRequests(RecipeStepResponse[] steps) {
        return steps
            .OrderBy(step => step.SortOrder)
            .Select(step => new SaveRecipeStepRequest(step.Instruction, step.TimerSeconds))
            .ToArray();
    }

    private static SaveRecipeNutritionRequest? ToSaveRequest(RecipeNutritionResponse? nutrition) {
        if (nutrition is null)
            return null;
        return new SaveRecipeNutritionRequest(
            nutrition.ServingBasis,
            nutrition.Nutrients
                .Select(nutrient => new SaveRecipeNutrientRequest(nutrient.NutrientType, nutrient.Amount))
                .ToArray()
        );
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
        "Lists recipes in the token's workspace with optional paging and sorting. Each entry includes webUrl, the page for that recipe in the web app."
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
            return SerializeWithRecipeLinks(result, workspaceId);
        } catch (AppException appException) {
            return SerializeAppException(appException);
        }
    }

    [McpServerTool]
    [Description("Gets a recipe by id. The response includes webUrl, the page for this recipe in the web app.")]
    public async Task<string> GetRecipe(Guid recipeId, CancellationToken cancellationToken) {
        var workspaceId = RequireMcpWorkspaceId();
        var result = await RecipesHandlers.GetRecipe(currentUserService, db, workspaceId, recipeId, cancellationToken);
        return SerializeWithRecipeLinks(result, workspaceId);
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
                    recipeImageStore,
                    workspaceId,
                    recipe,
                    cancellationToken
                );
                return SerializeWithRecipeLinks(result, workspaceId);
            }
        );
    }

    [McpServerTool]
    [Description(
        "Updates a recipe by id. This is a partial update: omitted arguments keep their stored value. "
        + "To clear an optional text field, pass an empty string."
    )]
    public async Task<string> UpdateRecipe(
        [Description("Recipe id to update.")] Guid recipeId,
        [Description("Recipe title. Omit to keep the stored title.")]
        string? title = null,
        [Description("Recipe description. Omit to keep the stored value, empty string to clear.")]
        string? description = null,
        [Description("Number of servings this recipe makes. Omit to keep the stored value.")]
        decimal? servings = null,
        [Description("Source URL for the recipe. Omit to keep the stored value, empty string to clear.")]
        string? sourceUrl = null,
        [Description("Free-form notes. Omit to keep the stored value, empty string to clear.")]
        string? notes = null,
        [Description("Prep time in minutes. Omit to keep the stored value.")]
        int? prepMinutes = null,
        [Description("Cook time in minutes. Omit to keep the stored value.")]
        int? cookMinutes = null,
        [Description("Whether the recipe should be archived. Omit to keep the stored value.")]
        bool? isArchived = null,
        [Description(
            "Recipe tags from the app whitelist only (kebab-case), e.g. dinner, breakfast, eggs, spicy, dessert, quick, vegetarian. "
            + "Omit to keep the stored tags; pass an empty array to remove all tags."
        )]
        string[]? tags = null,
        [Description(
            "Full replacement list of recipe ingredients. Omit to keep the stored ingredients; pass an empty array to remove them all."
        )]
        SaveRecipeIngredientRequest[]? ingredients = null,
        [Description(
            "Full replacement list of recipe instructions/steps. Omit to keep the stored steps; pass an empty array to remove them all."
        )]
        SaveRecipeStepRequest[]? steps = null,
        [Description("Nutrition block. Omit to keep the stored nutrition.")]
        SaveRecipeNutritionRequest? nutrition = null,
        [Description("Optional imported image URL (must be http/https). Omit to keep the current image.")]
        string? importImageUrl = null,
        CancellationToken cancellationToken = default
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(UpdateRecipe),
            async () => {
                var workspaceId = RequireMcpWorkspaceId();
                var existing = (await RecipesHandlers.GetRecipe(
                    currentUserService,
                    db,
                    workspaceId,
                    recipeId,
                    cancellationToken
                )).Value ?? throw new EntityNotFoundException("Recipe not found", null);

                var recipe = new SaveRecipeRequest(
                    string.IsNullOrWhiteSpace(title) ? existing.Title : title,
                    MergeOptionalText(description, existing.Description),
                    servings ?? existing.Servings,
                    MergeOptionalText(sourceUrl, existing.SourceUrl),
                    MergeOptionalText(notes, existing.Notes),
                    prepMinutes ?? existing.PrepMinutes,
                    cookMinutes ?? existing.CookMinutes,
                    isArchived ?? existing.IsArchived,
                    tags ?? existing.Tags,
                    ingredients ?? ToSaveRequests(existing.Ingredients),
                    steps ?? ToSaveRequests(existing.Steps),
                    nutrition ?? ToSaveRequest(existing.Nutrition),
                    importImageUrl
                );
                await ValidateRequestAsync(recipe, cancellationToken);

                // Only rewrite the child rows the caller actually sent; the rest keep their ids and per-row state.
                var childReplacement = RecipeChildReplacement.None;
                if (ingredients is not null)
                    childReplacement |= RecipeChildReplacement.Ingredients;
                if (steps is not null)
                    childReplacement |= RecipeChildReplacement.Steps;
                if (nutrition is not null)
                    childReplacement |= RecipeChildReplacement.Nutrition;

                var result = await RecipesHandlers.PatchRecipeInternal(
                    currentUserService,
                    db,
                    recipeImportService,
                    recipeImageStore,
                    loggerFactory,
                    workspaceId,
                    recipeId,
                    recipe,
                    childReplacement,
                    cancellationToken
                );
                return SerializeWithRecipeLinks(result, workspaceId);
            }
        );
    }

    [McpServerTool]
    [Description("Renames a recipe. Touches the title only; every other field, including ingredients and steps, is left as-is.")]
    public async Task<string> RenameRecipe(
        [Description("Recipe id to rename.")] Guid recipeId,
        [Description("New recipe title.")] string title,
        CancellationToken cancellationToken = default
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(RenameRecipe),
            () => UpdateRecipe(recipeId, title, cancellationToken: cancellationToken)
        );
    }

    [McpServerTool]
    [Description("Replaces a recipe's tags. Touches tags only; every other field is left as-is.")]
    public async Task<string> SetRecipeTags(
        [Description("Recipe id to update.")] Guid recipeId,
        [Description(
            "Full replacement tag list, from the app whitelist only (kebab-case). Pass an empty array to remove all tags."
        )]
        string[] tags,
        CancellationToken cancellationToken = default
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(SetRecipeTags),
            () => UpdateRecipe(recipeId, tags: tags, cancellationToken: cancellationToken)
        );
    }

    [McpServerTool]
    [Description("Archives or unarchives a recipe. Touches the archived flag only; every other field is left as-is.")]
    public async Task<string> ArchiveRecipe(
        [Description("Recipe id to update.")] Guid recipeId,
        [Description("True to archive, false to restore.")]
        bool isArchived,
        CancellationToken cancellationToken = default
    ) {
        return await ExecuteToolWithErrorLoggingAsync(
            nameof(ArchiveRecipe),
            () => UpdateRecipe(recipeId, isArchived: isArchived, cancellationToken: cancellationToken)
        );
    }

    [McpServerTool]
    [Description("Soft-deletes a recipe.")]
    public async Task<string> DeleteRecipe(Guid recipeId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await RecipesHandlers.DeleteRecipe(currentUserService, db, recipeImageStore, workspaceId, recipeId);
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
                    recipeImageStore,
                    workspaceId,
                    body,
                    cancellationToken
                );
                return SerializeWithRecipeLinks(result, workspaceId);
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
        "Creates or updates a next meal. Pass nextMealId to update; otherwise pass null to create. "
        + "When updating, this is a partial update: omitted arguments keep their stored value."
    )]
    public async Task<string> PutNextMeal(
        [Description("Next-meal id to update. Omit/null to create a new entry.")]
        Guid? nextMealId = null,
        [Description("Recipe id for the next meal. Required when creating; omit when updating to keep the stored recipe.")]
        Guid? recipeId = null,
        [Description("Target date in yyyy-MM-dd format. Required when creating; omit when updating to keep the stored date.")]
        string? plannedDate = null,
        [Description("Meal type value (must match allowed meal types). Required when creating; omit when updating to keep the stored value.")]
        string? mealType = null,
        [Description("Target servings. Omit when updating to keep the stored value.")]
        decimal? targetServings = null,
        [Description("Notes. Omit when updating to keep the stored value, empty string to clear.")]
        string? notes = null,
        [Description("Status value (must match allowed meal-plan statuses). Required when creating; omit when updating to keep the stored value.")]
        string? status = null,
        [Description("Optional completion timestamp in UTC (ISO-8601). If omitted and status is completed, server sets current UTC time.")]
        string? completedAtUtc = null,
        CancellationToken cancellationToken = default
    ) {
        var workspaceId = RequireMcpWorkspaceId();

        DateOnly? parsedPlannedDate = null;
        if (!string.IsNullOrWhiteSpace(plannedDate)) {
            if (!DateOnly.TryParse(plannedDate, out var parsedDate))
                throw new InvalidFormatException("plannedDate must be a valid date in yyyy-MM-dd format.", null);
            parsedPlannedDate = parsedDate;
        }

        DateTime? parsedCompletedAtUtc = null;
        if (!string.IsNullOrWhiteSpace(completedAtUtc)) {
            if (!DateTime.TryParse(completedAtUtc, out var parsed))
                throw new InvalidFormatException("completedAtUtc must be a valid ISO-8601 UTC datetime.", null);
            parsedCompletedAtUtc = parsed.ToUniversalTime();
        }

        if (nextMealId is null) {
            if (recipeId is null)
                return BuildValidationErrorResponse(nameof(recipeId), "Required when creating a next meal.");
            if (parsedPlannedDate is null)
                return BuildValidationErrorResponse(nameof(plannedDate), "Required when creating a next meal.");
            if (string.IsNullOrWhiteSpace(mealType))
                return BuildValidationErrorResponse(nameof(mealType), "Required when creating a next meal.");
            if (string.IsNullOrWhiteSpace(status))
                return BuildValidationErrorResponse(nameof(status), "Required when creating a next meal.");

            var newEntry = new SaveMealPlanEntryRequest(
                recipeId.Value,
                parsedPlannedDate.Value,
                mealType,
                targetServings,
                MergeOptionalText(notes, null),
                status,
                parsedCompletedAtUtc
            );
            await ValidateRequestAsync(newEntry, cancellationToken);

            var createResult = await MealPlanEntriesHandlers.PostMealPlanEntry(
                currentUserService,
                db,
                workspaceId,
                newEntry
            );
            return Serialize(createResult);
        }

        var entries = (await MealPlanEntriesHandlers.GetMealPlanEntries(
            currentUserService,
            db,
            workspaceId,
            null,
            null
        )).Value ?? [];

        var existing = entries.FirstOrDefault(value => value.Id == nextMealId.Value)
                       ?? throw new EntityNotFoundException("Meal-plan entry not found", null);

        var entry = new SaveMealPlanEntryRequest(
            recipeId ?? existing.RecipeId,
            parsedPlannedDate ?? existing.PlannedDate,
            string.IsNullOrWhiteSpace(mealType) ? existing.MealType : mealType,
            targetServings ?? existing.TargetServings,
            MergeOptionalText(notes, existing.Notes),
            string.IsNullOrWhiteSpace(status) ? existing.Status : status,
            parsedCompletedAtUtc ?? existing.CompletedAtUtc
        );
        await ValidateRequestAsync(entry, cancellationToken);

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
    [Description(
        "Updates shopping list metadata by id. This is a partial update: omitted arguments keep their stored value. "
        + "To clear notes, pass an empty string."
    )]
    public async Task<string> UpdateShoppingList(
        [Description("Shopping list id to update.")]
        Guid shoppingListId,
        [Description("Shopping list name. Omit to keep the stored name.")]
        string? name = null,
        [Description("Shopping list notes. Omit to keep the stored value, empty string to clear.")]
        string? notes = null,
        CancellationToken cancellationToken = default
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var existing = (await ShoppingListsHandlers.GetShoppingList(
            currentUserService,
            db,
            workspaceId,
            shoppingListId
        )).Value ?? throw new EntityNotFoundException("Shopping list not found", null);

        var request = new SaveShoppingListRequest(
            string.IsNullOrWhiteSpace(name) ? existing.Name : name,
            MergeOptionalText(notes, existing.Notes)
        );
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
    [Description(
        "Updates a shopping list item by id. This is a partial update: omitted arguments keep their stored value. "
        + "To clear an optional text field, pass an empty string."
    )]
    public async Task<string> UpdateShoppingListItem(
        [Description("Shopping list id containing the item.")]
        Guid shoppingListId,
        [Description("Shopping list item id to update.")]
        Guid itemId,
        [Description("Display name for the item. Omit to keep the stored name.")]
        string? name = null,
        [Description("Normalized ingredient name. Omit to keep the stored value, empty string to clear.")]
        string? normalizedIngredientName = null,
        [Description("Numeric amount. Omit to keep the stored value.")]
        decimal? amount = null,
        [Description("Unit, e.g. g, oz, cup. Omit to keep the stored value, empty string to clear.")]
        string? unit = null,
        [Description("Whether the amount is approximate. Omit to keep the stored value.")]
        bool? isApproximate = null,
        [Description("Whether the item is checked/completed. Omit to keep the stored value.")]
        bool? isChecked = null,
        [Description("Whether this item was manually added. Omit to keep the stored value.")]
        bool? isManual = null,
        [Description("Category, e.g. Produce. Omit to keep the stored value, empty string to clear.")]
        string? category = null,
        [Description("Note. Omit to keep the stored value, empty string to clear.")]
        string? note = null,
        [Description("Primary display text for the item. Omit to keep the stored value.")]
        string? displayText = null,
        [Description("Source names that contributed to this item. Omit to keep the stored values.")]
        string[]? sourceNames = null,
        CancellationToken cancellationToken = default
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var list = (await ShoppingListsHandlers.GetShoppingList(
            currentUserService,
            db,
            workspaceId,
            shoppingListId
        )).Value ?? throw new EntityNotFoundException("Shopping list not found", null);

        var existing = list.Items.FirstOrDefault(value => value.Id == itemId)
                       ?? throw new EntityNotFoundException("Shopping-list item not found", null);

        var item = new SaveShoppingListItemRequest(
            string.IsNullOrWhiteSpace(name) ? existing.Name : name,
            MergeOptionalText(normalizedIngredientName, existing.NormalizedIngredientName),
            amount ?? existing.Amount,
            MergeOptionalText(unit, existing.Unit),
            isApproximate ?? existing.IsApproximate,
            isChecked ?? existing.IsChecked,
            isManual ?? existing.IsManual,
            MergeOptionalText(category, existing.Category),
            MergeOptionalText(note, existing.Note),
            string.IsNullOrWhiteSpace(displayText) ? existing.DisplayText : displayText,
            sourceNames ?? existing.SourceNames
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
