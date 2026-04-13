using Api.Endpoints.Requests.MealPrep;
using Api.Models;
using Api.Services.MealPrep;

namespace Api.Services.MealPrep;

/// <summary>
///     Maps stored recipes to save payloads (e.g. collection export / backup).
/// </summary>
public static class RecipeExportMapper
{
    public static SaveRecipeRequest ToSaveRecipeRequest(Recipe recipe) {
        var ingredients = recipe.Ingredients
            .OrderBy(ingredient => ingredient.SortOrder)
            .Select(ingredient => new SaveRecipeIngredientRequest(
                    ingredient.Name,
                    ingredient.NormalizedIngredientName,
                    ingredient.Amount,
                    ingredient.Unit,
                    ingredient.PreparationNote,
                    ingredient.Section,
                    ingredient.DisplayText
                )
            )
            .ToArray();

        var steps = recipe.Steps
            .OrderBy(step => step.SortOrder)
            .Select(step => new SaveRecipeStepRequest(step.Instruction, step.TimerSeconds))
            .ToArray();

        SaveRecipeNutritionRequest? nutrition = null;
        if (recipe.NutritionServingBasis is not null || recipe.Nutrition.Count > 0) {
            nutrition = new SaveRecipeNutritionRequest(
                recipe.NutritionServingBasis,
                recipe.Nutrition
                    .OrderBy(n => NutrientSortIndex(n.NutrientType))
                    .ThenBy(n => n.NutrientType)
                    .Select(n => new SaveRecipeNutrientRequest(n.NutrientType, n.Amount))
                    .ToArray()
            );
        }

        return new SaveRecipeRequest(
            recipe.Title,
            recipe.Description,
            recipe.Servings,
            recipe.SourceUrl,
            recipe.Notes,
            recipe.PrepMinutes,
            recipe.CookMinutes,
            recipe.IsArchived,
            RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags),
            ingredients,
            steps,
            nutrition,
            ImportImageUrl: null
        );
    }

    private static int NutrientSortIndex(string nutrientType) {
        var index = Array.IndexOf(RecipeNutrientTypes.DefaultOrder, nutrientType);
        return index >= 0 ? index : int.MaxValue;
    }
}
