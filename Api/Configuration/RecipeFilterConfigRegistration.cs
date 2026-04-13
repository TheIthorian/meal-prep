using Api.Models;
using Api.Models.Filter;
using Microsoft.EntityFrameworkCore;

namespace Api.Configuration;

/// <summary>
///     Registers filter rules for workspace recipe queries.
/// </summary>
public static class RecipeFilterConfigRegistration
{
    public static void RegisterRecipeFilters(FilterConfigurationProvider provider) {
        var config = new FilterConfiguration<Recipe>();

        config.Rules["q"] = new FilterRule<Recipe> {
            ValueType = typeof(string), ApplyToQuery = (query, value) => ApplyTextSearch(query, value.ToString())
        };

        provider.Add(config);
    }

    public static IQueryable<Recipe> ApplyTextSearch(IQueryable<Recipe> query, string? searchText) {
        var search = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(search))
            return query;

        var pattern = $"%{search}%";

        return query.Where(recipe => EF.Functions.ILike(recipe.Title, pattern)
                                     || (recipe.Description != null && EF.Functions.ILike(recipe.Description, pattern))
                                     || recipe.Tags.Any(tag => EF.Functions.ILike(tag, pattern))
                                     || recipe.Ingredients.Any(ingredient => EF.Functions.ILike(
                                             ingredient.Name,
                                             pattern
                                         )
                                     )
        );
    }
}
