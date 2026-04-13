using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Api.Authentication;
using Api.Configuration;
using Api.Data;
using Api.Domain;
using Api.Endpoints;
using Api.Endpoints.Requests.MealPrep;
using Api.Models;
using Api.Services;
using Api.Services.MealPrep;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.Server;

namespace Api.Mcp;

/// <summary>
///     MCP tools that delegate to the same minimal-API handlers as the REST surface. Personal access tokens are scoped to one workspace; the workspace is taken from the token, not from tool arguments.
/// </summary>
[McpServerToolType]
public sealed class MealPrepMcpTools(
    CurrentUserService currentUserService,
    ApiDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IFilterConfigurationProvider filterConfigurationProvider,
    RecipeImportService recipeImportService,
    ShoppingListGenerationService shoppingListGenerationService,
    MeasurementService measurementService,
    IS3StorageService s3StorageService
)
{
    private static string Serialize<T>(JsonHttpResult<T> result) {
        return JsonSerializer.Serialize(result.Value, McpJson.SerializerOptions);
    }

    private DefaultHttpContext BuildQueryHttpContext(string? queryString) {
        var inner = httpContextAccessor.HttpContext!;
        var ctx = new DefaultHttpContext { User = inner.User, RequestServices = inner.RequestServices };
        var parsed = QueryHelpers.ParseQuery(queryString ?? string.Empty);
        ctx.Request.Query = new QueryCollection(parsed);
        return ctx;
    }

    private static T DeserializeBody<T>(string json) where T : class {
        var value = JsonSerializer.Deserialize<T>(json, McpJson.SerializerOptions);
        if (value is null) throw new InvalidFormatException("Request body JSON was invalid or empty.", null);
        return value;
    }

    private Guid RequireMcpWorkspaceId() {
        var raw = httpContextAccessor.HttpContext?.User.FindFirstValue(McpPatClaims.WorkspaceId);
        if (raw is not null && Guid.TryParse(raw, out var id))
            return id;
        throw new UnauthorizedException();
    }

    [McpServerTool]
    [Description("Returns the authenticated user profile and workspace memberships (read-only).")]
    public async Task<string> MealPrepGetCurrentUser(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var result = await AuthHandlers.GetMe(currentUserService, db);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Returns the workspace this MCP token is scoped to (read-only).")]
    public async Task<string> MealPrepListWorkspaces(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var single = await WorkspacesHandlers.GetWorkspace(currentUserService, db, workspaceId);
        return JsonSerializer.Serialize(new[] { single.Value }, McpJson.SerializerOptions);
    }

    [McpServerTool]
    [Description("Gets the workspace this MCP token is scoped to (read-only).")]
    public async Task<string> MealPrepGetWorkspace(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var result = await WorkspacesHandlers.GetWorkspace(currentUserService, db, workspaceId);
        return Serialize(result);
    }

    [McpServerTool]
    [Description(
        "Lists recipes in the token's workspace. Optional listQuery uses the same query parameters as the REST API (e.g. page, pageSize, orderBy, direction, includeArchived, filters)."
    )]
    public async Task<string> MealPrepListRecipes(string? listQuery, CancellationToken cancellationToken) {
        var workspaceId = RequireMcpWorkspaceId();
        var httpContext = BuildQueryHttpContext(listQuery);
        var result = await RecipesHandlers.GetRecipes(
            currentUserService,
            db,
            filterConfigurationProvider,
            httpContext,
            workspaceId,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Gets a recipe by id.")]
    public async Task<string> MealPrepGetRecipe(Guid recipeId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var result = await RecipesHandlers.GetRecipe(currentUserService, db, workspaceId, recipeId);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Creates a recipe. recipeJson must match the SaveRecipeRequest schema used by the REST API.")]
    public async Task<string> MealPrepCreateRecipe(string recipeJson, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveRecipeRequest>(recipeJson);
        var result = await RecipesHandlers.PostRecipe(
            currentUserService,
            db,
            recipeImportService,
            s3StorageService,
            workspaceId,
            body,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates a recipe. recipeJson must match the SaveRecipeRequest schema.")]
    public async Task<string> MealPrepUpdateRecipe(Guid recipeId, string recipeJson, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveRecipeRequest>(recipeJson);
        var result = await RecipesHandlers.PatchRecipe(
            currentUserService,
            db,
            recipeImportService,
            s3StorageService,
            workspaceId,
            recipeId,
            body,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Soft-deletes a recipe.")]
    public async Task<string> MealPrepDeleteRecipe(Guid recipeId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await RecipesHandlers.DeleteRecipe(currentUserService, db, s3StorageService, workspaceId, recipeId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Previews importing a recipe from a URL (same as REST import-preview).")]
    public async Task<string> MealPrepPreviewRecipeImport(string sourceUrl, CancellationToken cancellationToken) {
        var workspaceId = RequireMcpWorkspaceId();
        var body = new ImportRecipePreviewRequest(sourceUrl);
        var result = await RecipesHandlers.PostImportPreview(
            currentUserService,
            workspaceId,
            recipeImportService,
            body,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description(
        "Lists meal-plan entries for a date range. Pass optionalPlannedFrom and optionalPlannedTo as yyyy-MM-dd (UTC) or omit for the default week window."
    )]
    public async Task<string> MealPrepListMealPlanEntries(
        string? optionalPlannedFrom,
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
    [Description("Creates a meal-plan entry. entryJson matches SaveMealPlanEntryRequest.")]
    public async Task<string> MealPrepCreateMealPlanEntry(string entryJson, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveMealPlanEntryRequest>(entryJson);
        var result = await MealPlanEntriesHandlers.PostMealPlanEntry(currentUserService, db, workspaceId, body);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates a meal-plan entry. entryJson matches SaveMealPlanEntryRequest.")]
    public async Task<string> MealPrepUpdateMealPlanEntry(
        Guid mealPlanEntryId,
        string entryJson,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveMealPlanEntryRequest>(entryJson);
        var result = await MealPlanEntriesHandlers.PatchMealPlanEntry(
            currentUserService,
            db,
            workspaceId,
            mealPlanEntryId,
            body
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes a meal-plan entry.")]
    public async Task<string> MealPrepDeleteMealPlanEntry(Guid mealPlanEntryId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await MealPlanEntriesHandlers.DeleteMealPlanEntry(currentUserService, db, workspaceId, mealPlanEntryId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Lists shopping lists in the token's workspace.")]
    public async Task<string> MealPrepListShoppingLists(CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var result = await ShoppingListsHandlers.GetShoppingLists(currentUserService, db, workspaceId);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Gets a shopping list with items and sources.")]
    public async Task<string> MealPrepGetShoppingList(Guid shoppingListId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var result = await ShoppingListsHandlers.GetShoppingList(currentUserService, db, workspaceId, shoppingListId);
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Generates a shopping list from recipes and/or meal-plan entries. requestJson matches GenerateShoppingListRequest.")]
    public async Task<string> MealPrepGenerateShoppingList(string requestJson, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<GenerateShoppingListRequest>(requestJson);
        var result = await ShoppingListsHandlers.PostGenerateShoppingList(
            currentUserService,
            db,
            shoppingListGenerationService,
            workspaceId,
            body,
            cancellationToken
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates shopping list metadata. requestJson matches SaveShoppingListRequest.")]
    public async Task<string> MealPrepUpdateShoppingList(
        Guid shoppingListId,
        string requestJson,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveShoppingListRequest>(requestJson);
        var result = await ShoppingListsHandlers.PatchShoppingList(
            currentUserService,
            db,
            workspaceId,
            shoppingListId,
            body
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes a shopping list.")]
    public async Task<string> MealPrepDeleteShoppingList(Guid shoppingListId, CancellationToken cancellationToken) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        await ShoppingListsHandlers.DeleteShoppingList(currentUserService, db, workspaceId, shoppingListId);
        return """{"ok":true}""";
    }

    [McpServerTool]
    [Description("Adds an item to a shopping list. itemJson matches SaveShoppingListItemRequest.")]
    public async Task<string> MealPrepCreateShoppingListItem(
        Guid shoppingListId,
        string itemJson,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveShoppingListItemRequest>(itemJson);
        var result = await ShoppingListsHandlers.PostShoppingListItem(
            currentUserService,
            db,
            measurementService,
            workspaceId,
            shoppingListId,
            body
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Updates a shopping list item. itemJson matches SaveShoppingListItemRequest.")]
    public async Task<string> MealPrepUpdateShoppingListItem(
        Guid shoppingListId,
        Guid itemId,
        string itemJson,
        CancellationToken cancellationToken
    ) {
        _ = cancellationToken;
        var workspaceId = RequireMcpWorkspaceId();
        var body = DeserializeBody<SaveShoppingListItemRequest>(itemJson);
        var result = await ShoppingListsHandlers.PatchShoppingListItem(
            currentUserService,
            db,
            measurementService,
            workspaceId,
            shoppingListId,
            itemId,
            body
        );
        return Serialize(result);
    }

    [McpServerTool]
    [Description("Deletes an item from a shopping list.")]
    public async Task<string> MealPrepDeleteShoppingListItem(
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
