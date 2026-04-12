using Api.Models;
using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class ShoppingListGenerationServiceTests
{
    private readonly ShoppingListGenerationService _service = new(new MeasurementService());

    [Fact]
    public void BuildFromSources_ShouldMergeCompatibleUnits()
    {
        var workspace = Workspace.CreateNew("Family Kitchen");
        var shoppingList = _service.BuildFromSources(
            workspace,
            "Weekly shop",
            null,
            [
                new ShoppingListGenerationSource(
                    Guid.NewGuid(),
                    null,
                    "Tomato soup",
                    4m,
                    4m,
                    [
                        new ShoppingListGenerationIngredient("Tomatoes", "tomatoes", 500m, "g", null, "produce"),
                    ]
                ),
                new ShoppingListGenerationSource(
                    Guid.NewGuid(),
                    null,
                    "Pasta sauce",
                    4m,
                    4m,
                    [
                        new ShoppingListGenerationIngredient("Tomatoes", "tomatoes", 1m, "kg", null, "produce"),
                    ]
                ),
            ]
        );

        var item = Assert.Single(shoppingList.Items);
        Assert.Equal("Tomatoes", item.Name);
        Assert.Equal(1.5m, item.Amount);
        Assert.Equal("kg", item.Unit);
        Assert.False(item.IsApproximate);
    }

    [Fact]
    public void BuildFromSources_ShouldKeepNonConvertibleItemsSeparate()
    {
        var workspace = Workspace.CreateNew("Family Kitchen");
        var shoppingList = _service.BuildFromSources(
            workspace,
            "Weekly shop",
            null,
            [
                new ShoppingListGenerationSource(
                    Guid.NewGuid(),
                    null,
                    "Tray bake",
                    4m,
                    4m,
                    [
                        new ShoppingListGenerationIngredient("Onion", "onion", 1m, null, null, "produce"),
                        new ShoppingListGenerationIngredient("Onion", "onion", 2m, "item", null, "produce"),
                    ]
                ),
            ]
        );

        Assert.Equal(2, shoppingList.Items.Count);
    }
}
