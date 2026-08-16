using System.Security.Claims;
using System.Text.Json;
using Api.Authentication;
using Api.Data;
using Api.Endpoints.Requests.MealPrep;
using Api.Endpoints.Responses.MealPrep;
using Api.Mcp;
using Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Endpoints;

/// <summary>
///     MCP update tools must merge with stored state so callers can send partial payloads without losing data.
/// </summary>
public sealed class McpToolsPartialUpdateTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiWebApplicationFactory factory;

    public McpToolsPartialUpdateTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task UpdateRecipe_WithOnlyTitle_KeepsExistingRecipeData() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Partial Update Recipes");

        var created = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.CreateRecipe(
                "Original title",
                "Original description",
                4,
                "https://example.com/recipe",
                "Original notes",
                10,
                20,
                false,
                ["dinner"],
                [new SaveRecipeIngredientRequest("Onion", "onion", 1, "unit", "diced", "Base", "1 onion, diced")],
                [new SaveRecipeStepRequest("Chop the onion", 60)],
                new SaveRecipeNutritionRequest(1, [new SaveRecipeNutrientRequest("calories", 250)]),
                null,
                CancellationToken.None
            )
        );

        var updated = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateRecipe(created.Id, "Renamed title", cancellationToken: CancellationToken.None)
        );

        Assert.Equal("Renamed title", updated.Title);
        Assert.Equal("Original description", updated.Description);
        Assert.Equal(4, updated.Servings);
        Assert.Equal("https://example.com/recipe", updated.SourceUrl);
        Assert.Equal("Original notes", updated.Notes);
        Assert.Equal(10, updated.PrepMinutes);
        Assert.Equal(20, updated.CookMinutes);
        Assert.Equal(["dinner"], updated.Tags);

        var ingredient = Assert.Single(updated.Ingredients);
        Assert.Equal("Onion", ingredient.Name);
        Assert.Equal("onion", ingredient.NormalizedIngredientName);
        Assert.Equal("diced", ingredient.PreparationNote);
        Assert.Equal("Base", ingredient.Section);

        var step = Assert.Single(updated.Steps);
        Assert.Equal("Chop the onion", step.Instruction);
        Assert.Equal(60, step.TimerSeconds);

        Assert.NotNull(updated.Nutrition);
        Assert.Equal(250, Assert.Single(updated.Nutrition!.Nutrients).Amount);
    }

    [Fact]
    public async Task UpdateRecipe_WithOnlyTitle_KeepsChildRowIds() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Stable Child Ids");

        var created = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.CreateRecipe(
                "Original title",
                null,
                2,
                null,
                null,
                null,
                null,
                false,
                [],
                [
                    new SaveRecipeIngredientRequest("Onion", "onion", 1, "unit", null, null, "1 onion"),
                    new SaveRecipeIngredientRequest("Garlic", "garlic", 2, "clove", null, null, "2 cloves")
                ],
                [new SaveRecipeStepRequest("Chop", null), new SaveRecipeStepRequest("Cook", null)],
                null,
                null,
                CancellationToken.None
            )
        );

        var updated = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateRecipe(created.Id, "Renamed title", cancellationToken: CancellationToken.None)
        );

        Assert.Equal(
            created.Ingredients.Select(ingredient => ingredient.Id),
            updated.Ingredients.Select(ingredient => ingredient.Id)
        );
        Assert.Equal(created.Steps.Select(step => step.Id), updated.Steps.Select(step => step.Id));
    }

    [Fact]
    public async Task UpdateRecipe_WithOnlyTitle_KeepsCollectionMembership() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Collections Preserved");

        var created = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => CreateSimpleRecipeAsync(tools)
        );

        var collectionName = await AddRecipeToNewCollectionAsync(workspaceId, created.Id, "All (Imported)");

        var updated = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateRecipe(created.Id, "Renamed title", cancellationToken: CancellationToken.None)
        );

        Assert.Equal(collectionName, Assert.Single(updated.Collections).CollectionName);
    }

    [Fact]
    public async Task UpdateRecipe_WithExplicitValues_StillReplacesThem() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Full Update Recipes");

        var created = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.CreateRecipe(
                "Original title",
                "Original description",
                4,
                null,
                "Original notes",
                null,
                null,
                false,
                ["dinner"],
                [new SaveRecipeIngredientRequest("Onion", "onion", 1, "unit", null, null, "1 onion")],
                [new SaveRecipeStepRequest("Chop the onion", null)],
                null,
                null,
                CancellationToken.None
            )
        );

        var updated = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateRecipe(
                created.Id,
                servings: 8,
                isArchived: true,
                ingredients: [new SaveRecipeIngredientRequest("Garlic", "garlic", 2, "clove", null, null, "2 cloves")],
                cancellationToken: CancellationToken.None
            )
        );

        Assert.Equal("Original title", updated.Title);
        Assert.Equal(8, updated.Servings);
        Assert.True(updated.IsArchived);
        Assert.Equal("Garlic", Assert.Single(updated.Ingredients).Name);
        Assert.Equal("Chop the onion", Assert.Single(updated.Steps).Instruction);
    }

    [Fact]
    public async Task UpdateRecipe_WithEmptyStringDescription_ClearsIt() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Clear Optional Text");

        var created = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.CreateRecipe(
                "Original title",
                "Original description",
                2,
                null,
                null,
                null,
                null,
                false,
                [],
                [new SaveRecipeIngredientRequest("Onion", "onion", 1, "unit", null, null, "1 onion")],
                [new SaveRecipeStepRequest("Cook", null)],
                null,
                null,
                CancellationToken.None
            )
        );

        var updated = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateRecipe(created.Id, description: "", cancellationToken: CancellationToken.None)
        );

        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task RenameRecipe_KeepsEverythingElse() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Rename Tool");

        var created = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);

        var renamed = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.RenameRecipe(created.Id, "Better soup", CancellationToken.None)
        );

        Assert.Equal("Better soup", renamed.Title);
        Assert.Equal(created.Ingredients.Select(ingredient => ingredient.Id), renamed.Ingredients.Select(i => i.Id));
        Assert.Equal(created.Steps.Select(step => step.Id), renamed.Steps.Select(step => step.Id));
        Assert.Equal(created.Servings, renamed.Servings);
    }

    [Fact]
    public async Task ArchiveRecipe_TogglesOnlyTheArchivedFlag() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Archive Tool");

        var created = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);

        var archived = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.ArchiveRecipe(created.Id, true, CancellationToken.None)
        );

        Assert.True(archived.IsArchived);
        Assert.Equal(created.Title, archived.Title);
        Assert.Equal(created.Ingredients.Select(ingredient => ingredient.Id), archived.Ingredients.Select(i => i.Id));
    }

    [Fact]
    public async Task SetRecipeTags_ReplacesTagsOnly() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Tags Tool");

        var created = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);

        var tagged = await CallAsync<RecipeResponse>(
            userId,
            workspaceId,
            tools => tools.SetRecipeTags(created.Id, ["dinner"], CancellationToken.None)
        );

        Assert.Equal(["dinner"], tagged.Tags);
        Assert.Equal(created.Title, tagged.Title);
        Assert.Equal(created.Ingredients.Select(ingredient => ingredient.Id), tagged.Ingredients.Select(i => i.Id));
    }

    [Fact]
    public async Task UpdateShoppingList_WithOnlyName_KeepsNotes() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Partial Update Lists");

        var recipe = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);
        var created = await CallAsync<ShoppingListResponse>(
            userId,
            workspaceId,
            tools => tools.GenerateShoppingList("Week one", "Original notes", [recipe.Id], [], CancellationToken.None)
        );

        var updated = await CallAsync<ShoppingListResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateShoppingList(created.Id, "Week two", cancellationToken: CancellationToken.None)
        );

        Assert.Equal("Week two", updated.Name);
        Assert.Equal("Original notes", updated.Notes);
    }

    [Fact]
    public async Task UpdateShoppingListItem_WithOnlyIsChecked_KeepsItemFields() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Partial Update Items");

        var recipe = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);
        var list = await CallAsync<ShoppingListResponse>(
            userId,
            workspaceId,
            tools => tools.GenerateShoppingList("Week one", null, [recipe.Id], [], CancellationToken.None)
        );
        var item = await CallAsync<ShoppingListItemResponse>(
            userId,
            workspaceId,
            tools => tools.CreateShoppingListItem(
                list.Id,
                "Onion",
                "onion",
                2,
                "unit",
                false,
                false,
                true,
                "Produce",
                "Original note",
                "2 onions",
                ["Soup"],
                CancellationToken.None
            )
        );

        var updated = await CallAsync<ShoppingListItemResponse>(
            userId,
            workspaceId,
            tools => tools.UpdateShoppingListItem(
                list.Id,
                item.Id,
                isChecked: true,
                cancellationToken: CancellationToken.None
            )
        );

        Assert.True(updated.IsChecked);
        Assert.Equal("Onion", updated.Name);
        Assert.Equal(2, updated.Amount);
        Assert.Equal("unit", updated.Unit);
        Assert.Equal("Produce", updated.Category);
        Assert.Equal("Original note", updated.Note);
        Assert.Equal(item.DisplayText, updated.DisplayText);
        Assert.Equal(["Soup"], updated.SourceNames);
    }

    [Fact]
    public async Task PutNextMeal_WithOnlyStatus_KeepsPlannedDateAndNotes() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Mcp Partial Update Next Meals");

        var recipe = await CallAsync<RecipeResponse>(userId, workspaceId, CreateSimpleRecipeAsync);
        var created = await CallAsync<MealPlanEntryResponse>(
            userId,
            workspaceId,
            tools => tools.PutNextMeal(
                null,
                recipe.Id,
                "2026-08-20",
                "dinner",
                3,
                "Original notes",
                "planned",
                null,
                CancellationToken.None
            )
        );

        var updated = await CallAsync<MealPlanEntryResponse>(
            userId,
            workspaceId,
            tools => tools.PutNextMeal(created.Id, status: "completed", cancellationToken: CancellationToken.None)
        );

        Assert.Equal("completed", updated.Status);
        Assert.Equal(new DateOnly(2026, 8, 20), updated.PlannedDate);
        Assert.Equal("dinner", updated.MealType);
        Assert.Equal(3, updated.TargetServings);
        Assert.Equal("Original notes", updated.Notes);
        Assert.Equal(recipe.Id, updated.RecipeId);
    }

    private static Task<string> CreateSimpleRecipeAsync(MealPrepMcpTools tools) {
        return tools.CreateRecipe(
            "Soup",
            null,
            2,
            null,
            null,
            null,
            null,
            false,
            [],
            [
                new SaveRecipeIngredientRequest("Onion", "onion", 1, "unit", null, null, "1 onion"),
                new SaveRecipeIngredientRequest("Garlic", "garlic", 2, "clove", null, null, "2 cloves")
            ],
            [new SaveRecipeStepRequest("Cook", null)],
            null,
            null,
            CancellationToken.None
        );
    }

    private async Task<string> AddRecipeToNewCollectionAsync(Guid workspaceId, Guid recipeId, string collectionName) {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var workspace = await db.Workspaces.FirstAsync(value => value.Id == workspaceId);
        var collection = RecipeCollection.CreateNew(workspace, collectionName, null);
        db.RecipeCollections.Add(collection);
        await db.SaveChangesAsync();

        db.RecipeCollectionRecipes.Add(RecipeCollectionRecipe.CreateNew(collection.Id, recipeId, 0));
        await db.SaveChangesAsync();

        return collectionName;
    }

    private async Task<T> CallAsync<T>(Guid userId, Guid workspaceId, Func<MealPrepMcpTools, Task<string>> call) {
        using var scope = factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(McpPatClaims.WorkspaceId, workspaceId.ToString())
            ],
            McpPatAuthenticationDefaults.AuthenticationScheme
        );
        accessor.HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(identity), RequestServices = scope.ServiceProvider
        };

        var tools = scope.ServiceProvider.GetRequiredService<MealPrepMcpTools>();
        var json = await call(tools);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        Assert.NotNull(value);
        return value!;
    }
}
