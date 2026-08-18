using System.Net.Http.Json;
using Api.Endpoints.Responses;
using Api.Endpoints.Responses.MealPrep;
using Xunit;

namespace Api.Tests.Endpoints;

/// <summary>
///     The recipe list can be sorted by title, created date, or last updated date.
/// </summary>
public sealed class RecipeListSortEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RecipeListSortEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task GetRecipes_OrderByTitleAscending_ReturnsAlphabeticalOrder() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe Sort Title");
        using var client = factory.CreateAuthenticatedClient(userId);

        await CreateRecipeAsync(client, workspaceId, "Pancakes");
        await CreateRecipeAsync(client, workspaceId, "apple pie");
        await CreateRecipeAsync(client, workspaceId, "Zucchini bake");

        var titles = await GetTitlesAsync(client, workspaceId, "orderBy=title&direction=asc");

        Assert.Equal(new[] { "apple pie", "Pancakes", "Zucchini bake" }, titles);
    }

    [Fact]
    public async Task GetRecipes_OrderByTitleDescending_ReturnsReverseAlphabeticalOrder() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe Sort Title Desc");
        using var client = factory.CreateAuthenticatedClient(userId);

        await CreateRecipeAsync(client, workspaceId, "Pancakes");
        await CreateRecipeAsync(client, workspaceId, "apple pie");

        var titles = await GetTitlesAsync(client, workspaceId, "orderBy=title&direction=desc");

        Assert.Equal(new[] { "Pancakes", "apple pie" }, titles);
    }

    [Fact]
    public async Task GetRecipes_OrderByUpdatedAt_PutsMostRecentlyEditedFirst() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe Sort Updated");
        using var client = factory.CreateAuthenticatedClient(userId);

        var first = await CreateRecipeAsync(client, workspaceId, "First");
        await CreateRecipeAsync(client, workspaceId, "Second");

        // Editing the older recipe should lift it above the newer one under an updated-at sort,
        // which is what distinguishes this sort from the created-date default.
        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes/{first.Id}",
            BuildRecipePayload("First edited")
        );
        patch.EnsureSuccessStatusCode();

        var titles = await GetTitlesAsync(client, workspaceId, "orderBy=updatedAt&direction=desc");

        Assert.Equal(new[] { "First edited", "Second" }, titles);
    }

    [Fact]
    public async Task GetRecipes_UnknownOrderBy_FallsBackToCreatedDate() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe Sort Fallback");
        using var client = factory.CreateAuthenticatedClient(userId);

        await CreateRecipeAsync(client, workspaceId, "Older");
        await CreateRecipeAsync(client, workspaceId, "Newer");

        var titles = await GetTitlesAsync(client, workspaceId, "orderBy=nonsense&direction=desc");

        Assert.Equal(new[] { "Newer", "Older" }, titles);
    }

    private static async Task<string[]> GetTitlesAsync(HttpClient client, Guid workspaceId, string queryString) {
        var response = await client.GetAsync($"/api/v1/workspaces/{workspaceId}/recipes?{queryString}");
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PaginatedResponse<RecipeListItemResponse>>())!;
        return page.Data.Select(item => item.Title).ToArray();
    }

    private static async Task<RecipeResponse> CreateRecipeAsync(HttpClient client, Guid workspaceId, string title) {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes",
            BuildRecipePayload(title)
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecipeResponse>())!;
    }

    private static object BuildRecipePayload(string title) {
        return new {
            title,
            servings = 2,
            isArchived = false,
            tags = Array.Empty<string>(),
            ingredients = new[] { new { name = "Onion", amount = 1, unit = "unit", displayText = "1 onion" } },
            steps = new[] { new { instruction = "Cook" } }
        };
    }
}
