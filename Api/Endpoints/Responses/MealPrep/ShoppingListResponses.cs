using Api.Models;

namespace Api.Endpoints.Responses.MealPrep;

public record ShoppingListListItemResponse(
    Guid Id,
    string Name,
    string? Notes,
    DateTime? GeneratedAt,
    int TotalItemCount,
    int CheckedItemCount
);

public record ShoppingListItemResponse(
    Guid Id,
    int SortOrder,
    string Name,
    string? NormalizedIngredientName,
    decimal? Amount,
    string? Unit,
    bool IsApproximate,
    bool IsChecked,
    bool IsManual,
    string? Category,
    string? Note,
    string DisplayText,
    string[] SourceNames
);

public record ShoppingListSourceResponse(Guid Id, Guid? RecipeId, Guid? NextMealId, string SourceName);

public record ShoppingListResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Notes,
    DateTime? GeneratedAt,
    ShoppingListItemResponse[] Items,
    ShoppingListSourceResponse[] Sources
);

/// <summary>
///     Maps shopping-list domain models to API responses.
/// </summary>
public static class ShoppingListResponseTransforms
{
    extension(ShoppingList shoppingList)
    {
        public ShoppingListListItemResponse ToShoppingListListItemResponse() {
            return new ShoppingListListItemResponse(
                shoppingList.Id,
                shoppingList.Name,
                shoppingList.Notes,
                shoppingList.GeneratedAt,
                shoppingList.Items.Count,
                shoppingList.Items.Count(item => item.IsChecked)
            );
        }

        public ShoppingListResponse ToShoppingListResponse() {
            return new ShoppingListResponse(
                shoppingList.Id,
                shoppingList.WorkspaceId,
                shoppingList.Name,
                shoppingList.Notes,
                shoppingList.GeneratedAt,
                shoppingList.Items
                    .OrderBy(item => item.IsChecked)
                    .ThenBy(item => item.SortOrder)
                    .Select(item => item.ToResponse())
                    .ToArray(),
                shoppingList.Sources.Select(source => source.ToResponse()).ToArray()
            );
        }
    }

    extension(ShoppingListItem item)
    {
        public ShoppingListItemResponse ToResponse() {
            return new ShoppingListItemResponse(
                item.Id,
                item.SortOrder,
                item.Name,
                item.NormalizedIngredientName,
                item.Amount,
                item.Unit,
                item.IsApproximate,
                item.IsChecked,
                item.IsManual,
                item.Category,
                item.Note,
                item.DisplayText,
                item.SourceNames
            );
        }
    }

    extension(ShoppingListSource source)
    {
        public ShoppingListSourceResponse ToResponse() {
            return new ShoppingListSourceResponse(
                source.Id,
                source.RecipeId,
                source.MealPlanEntryId,
                source.SourceName
            );
        }
    }
}
