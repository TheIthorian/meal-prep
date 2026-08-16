using System.Net.Http.Json;
using Api.Endpoints.Responses.MealPrep;
using Xunit;

namespace Api.Tests.Endpoints;

public sealed class ShoppingListItemEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public ShoppingListItemEndpointsTests(ApiWebApplicationFactory factory) {
        this.factory = factory;
    }

    [Fact]
    public async Task PostShoppingListItem_AddsItemToGeneratedList() {
        var (userId, workspaceId) = await factory.SeedUserWithWorkspaceAsync("Shopping List Items");
        using var client = factory.CreateAuthenticatedClient(userId);

        var recipeResponse = await client.PostAsJsonAsync(
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
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = (await recipeResponse.Content.ReadFromJsonAsync<RecipeResponse>())!;

        var generateResponse = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/shopping-lists/generate",
            new { name = "Week one", recipeIds = new[] { recipe.Id }, nextMealIds = Array.Empty<Guid>() }
        );
        generateResponse.EnsureSuccessStatusCode();
        var list = (await generateResponse.Content.ReadFromJsonAsync<ShoppingListResponse>())!;

        var itemResponse = await client.PostAsJsonAsync(
            $"/api/v1/workspaces/{workspaceId}/shopping-lists/{list.Id}/items",
            new {
                name = "Bread",
                isApproximate = false,
                isChecked = false,
                isManual = true,
                displayText = "1 loaf"
            }
        );

        itemResponse.EnsureSuccessStatusCode();
        var item = (await itemResponse.Content.ReadFromJsonAsync<ShoppingListItemResponse>())!;
        Assert.Equal("Bread", item.Name);

        var reloadResponse = await client.GetAsync($"/api/v1/workspaces/{workspaceId}/shopping-lists/{list.Id}");
        reloadResponse.EnsureSuccessStatusCode();
        var reloaded = (await reloadResponse.Content.ReadFromJsonAsync<ShoppingListResponse>())!;
        Assert.Contains(reloaded.Items, value => value.Id == item.Id && value.Name == "Bread");
    }
}
