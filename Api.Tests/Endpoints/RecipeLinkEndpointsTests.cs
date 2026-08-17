using System.Net.Http.Json;
using Api.Endpoints.Responses;
using Api.Endpoints.Responses.MealPrep;
using Xunit;

namespace Api.Tests.Endpoints;

/// <summary>
///     Recipe payloads carry a web-app link and an API self link, so a client holding an id can navigate.
/// </summary>
public sealed class RecipeLinkEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RecipeLinkEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task GetRecipe_ReturnsWebAndResourceLinks() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe Links");
        using var client = factory.CreateAuthenticatedClient(userId);

        var created = await CreateRecipeAsync(client, workspaceId);

        var response = await client.GetAsync($"/api/v1/workspaces/{workspaceId}/recipes/{created.Id}");
        response.EnsureSuccessStatusCode();
        var recipe = (await response.Content.ReadFromJsonAsync<RecipeResponse>())!;

        Assert.Equal($"http://localhost:8080/workspaces/{workspaceId}/recipe/{created.Id}", recipe.WebUrl);
        Assert.NotNull(recipe.ResourceUrl);
        Assert.EndsWith($"/api/v1/workspaces/{workspaceId}/recipes/{created.Id}", recipe.ResourceUrl);
    }

    [Fact]
    public async Task GetRecipes_ReturnsLinksForEveryListEntry() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Recipe List Links");
        using var client = factory.CreateAuthenticatedClient(userId);

        var created = await CreateRecipeAsync(client, workspaceId);

        var response = await client.GetAsync($"/api/v1/workspaces/{workspaceId}/recipes");
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PaginatedResponse<RecipeListItemResponse>>())!;

        var item = Assert.Single(page.Data, entry => entry.Id == created.Id);
        Assert.Equal($"http://localhost:8080/workspaces/{workspaceId}/recipe/{created.Id}", item.WebUrl);
        Assert.EndsWith($"/api/v1/workspaces/{workspaceId}/recipes/{created.Id}", item.ResourceUrl);
    }

    private static async Task<RecipeResponse> CreateRecipeAsync(HttpClient client, Guid workspaceId) {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/recipes",
            new {
                title = "Soup",
                servings = 2,
                isArchived = false,
                tags = Array.Empty<string>(),
                ingredients = new[] { new { name = "Onion", amount = 1, unit = "unit", displayText = "1 onion" } },
                steps = new[] { new { instruction = "Cook" } }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecipeResponse>())!;
    }
}
