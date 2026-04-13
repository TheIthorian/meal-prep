using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Cached LLM-assigned store category for a normalized ingredient name (shared across workspaces).
/// </summary>
public class IngredientCategoryCache : Entity
{
    private IngredientCategoryCache() { }

    public IngredientCategoryCache(string normalizedIngredientName, string category) {
        NormalizedIngredientName = normalizedIngredientName;
        Category = category;
    }

    [MaxLength(512)] public string NormalizedIngredientName { get; private set; } = string.Empty;

    [MaxLength(128)] public string Category { get; private set; } = string.Empty;
}
