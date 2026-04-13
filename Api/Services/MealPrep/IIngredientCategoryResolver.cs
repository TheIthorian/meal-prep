namespace Api.Services.MealPrep;

/// <summary>
///     Resolves grocery-style categories for normalized ingredient names using DB cache and optional LLM fill-in.
/// </summary>
public interface IIngredientCategoryResolver
{
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IReadOnlyCollection<string> normalizedIngredientNames,
        CancellationToken cancellationToken = default
    );
}
