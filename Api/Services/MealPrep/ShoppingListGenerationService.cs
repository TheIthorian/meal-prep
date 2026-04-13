using Api.Models;

namespace Api.Services.MealPrep;

/// <summary>
///     Builds shopping lists from recipes or meal-plan entries while consolidating compatible ingredients.
/// </summary>
public class ShoppingListGenerationService(MeasurementService measurementService, IIngredientCategoryResolver ingredientCategoryResolver)
{
    public async Task<ShoppingList> BuildFromSourcesAsync(
        Workspace workspace,
        string name,
        string? notes,
        IReadOnlyCollection<ShoppingListGenerationSource> sources,
        CancellationToken cancellationToken = default
    )
    {
        var shoppingList = ShoppingList.CreateNew(workspace, name);
        shoppingList.UpdateDetails(name, notes);
        shoppingList.MarkGenerated(DateTime.UtcNow);

        var groupedIngredients = new Dictionary<string, AggregatedIngredient>();
        var rawIngredients = new List<AggregatedIngredient>();

        foreach (var source in sources)
        foreach (var ingredient in source.Ingredients)
        {
            var scaledAmount = measurementService.ScaleAmount(
                ingredient.Amount,
                source.BaseServings <= 0m ? 1m : source.BaseServings,
                source.TargetServings <= 0m ? source.BaseServings : source.TargetServings
            );

            var normalizedName = ingredient.NormalizedIngredientName
                                 ?? measurementService.NormalizeIngredientName(ingredient.Name);
            var normalizedUnit = measurementService.Normalize(ingredient.Unit);

            if (scaledAmount is null || normalizedUnit.FactorToCanonical is null || normalizedUnit.Kind is null)
            {
                rawIngredients.Add(
                    new AggregatedIngredient(
                        ingredient.Name,
                        normalizedName,
                        scaledAmount,
                        ingredient.Unit,
                        ingredient.Section,
                        false,
                        ingredient.PreparationNote
                    )
                );
                continue;
            }

            var canonicalAmount = scaledAmount.Value * normalizedUnit.FactorToCanonical.Value;
            var key = $"{normalizedName}|{normalizedUnit.Kind}|{normalizedUnit.CanonicalUnit}";

            if (!groupedIngredients.TryGetValue(key, out var existing))
            {
                groupedIngredients[key] = new AggregatedIngredient(
                    ingredient.Name,
                    normalizedName,
                    canonicalAmount,
                    normalizedUnit.CanonicalUnit,
                    ingredient.Section,
                    normalizedUnit.IsApproximate,
                    ingredient.PreparationNote
                );
                continue;
            }

            groupedIngredients[key] = existing with {
                Amount = (existing.Amount ?? 0m) + canonicalAmount,
                IsApproximate = existing.IsApproximate || normalizedUnit.IsApproximate
            };
        }

        var groupedList = groupedIngredients.Values.ToList();
        var categoryKeys = groupedList.Concat(rawIngredients)
            .Select(item => item.NormalizedName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var categoryMap = await ingredientCategoryResolver.ResolveAsync(categoryKeys, cancellationToken).ConfigureAwait(false);

        string? ResolveCategory(AggregatedIngredient item)
        {
            if (categoryMap.TryGetValue(item.NormalizedName, out var c) && !string.IsNullOrWhiteSpace(c))
                return c;

            return item.Section;
        }

        var combined = groupedList
            .Concat(rawIngredients)
            .OrderBy(item => ResolveCategory(item))
            .ThenBy(item => item.Name)
            .Select((item, index) => CreateGeneratedItem(index, item, ResolveCategory(item)))
            .ToArray();

        shoppingList.ReplaceItems(combined);
        shoppingList.ReplaceSources(
            sources.Select(source =>
                ShoppingListSource.CreateNew(source.RecipeId, source.MealPlanEntryId, source.SourceName)
            )
        );

        return shoppingList;
    }

    private ShoppingListItem CreateGeneratedItem(int sortOrder, AggregatedIngredient item, string? category)
    {
        decimal? amount = item.Amount;
        string? unit = item.Unit;
        var isApproximate = item.IsApproximate;

        if (amount is not null && !string.IsNullOrWhiteSpace(unit))
        {
            var displayAmount = measurementService.ConvertForDisplay(amount.Value, unit, isApproximate);
            amount = displayAmount.Amount;
            unit = displayAmount.Unit;
            isApproximate = displayAmount.IsApproximate;
        }

        var displayText = measurementService.BuildDisplayText(amount, unit, item.Name, item.PreparationNote);
        return ShoppingListItem.CreateNew(
            sortOrder,
            item.Name,
            displayText,
            amount,
            unit,
            item.NormalizedName,
            isApproximate,
            false,
            category,
            item.PreparationNote
        );
    }
}

public sealed record ShoppingListGenerationSource(
    Guid? RecipeId,
    Guid? MealPlanEntryId,
    string SourceName,
    decimal BaseServings,
    decimal TargetServings,
    IReadOnlyCollection<ShoppingListGenerationIngredient> Ingredients
);

public sealed record ShoppingListGenerationIngredient(
    string Name,
    string? NormalizedIngredientName,
    decimal? Amount,
    string? Unit,
    string? PreparationNote,
    string? Section
);

internal sealed record AggregatedIngredient(
    string Name,
    string NormalizedName,
    decimal? Amount,
    string? Unit,
    string? Section,
    bool IsApproximate,
    string? PreparationNote
);
