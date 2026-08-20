using Api.Models;

namespace Api.Services.MealPrep;

/// <summary>
///     Copies a recipe into another workspace, detached from the original: the copy carries the same content
///     but is owned and editable by the target workspace. Used by collection imports and single recipe shares.
/// </summary>
public static class RecipeCloner
{
    public static async Task<Recipe> CloneToWorkspaceAsync(
        Recipe sourceRecipe,
        Workspace targetWorkspace,
        RecipeImageStore recipeImageStore,
        CancellationToken cancellationToken
    ) {
        var recipe = Recipe.CreateNew(targetWorkspace, sourceRecipe.Title, sourceRecipe.Servings);
        recipe.UpdateDetails(
            sourceRecipe.Title,
            sourceRecipe.Description,
            sourceRecipe.Servings,
            sourceRecipe.SourceUrl,
            sourceRecipe.Notes,
            sourceRecipe.PrepMinutes,
            sourceRecipe.CookMinutes,
            sourceRecipe.IsArchived,
            RecipeTagWhitelist.NormalizeToWhitelist(sourceRecipe.Tags)
        );
        recipe.ReplaceIngredients(
            sourceRecipe.Ingredients
                .OrderBy(value => value.SortOrder)
                .Select((value, index) => RecipeIngredient.CreateNew(
                        index,
                        value.Name,
                        value.DisplayText,
                        value.Amount,
                        value.Unit,
                        value.NormalizedIngredientName,
                        value.PreparationNote,
                        value.Section
                    )
                )
        );
        recipe.ReplaceSteps(
            sourceRecipe.Steps
                .OrderBy(value => value.SortOrder)
                .Select((value, index) => RecipeStep.CreateNew(index, value.Instruction, value.TimerSeconds))
        );
        recipe.SetNutrition(
            sourceRecipe.NutritionServingBasis,
            sourceRecipe.Nutrition
                .Select(value => RecipeNutrition.CreateNew(value.NutrientType, value.Amount))
                .ToArray()
        );

        if (!string.IsNullOrEmpty(sourceRecipe.ImageObjectKey)) {
            // The source bytes are already optimized, so this is a copy rather than a re-upload:
            // the image and its renditions are duplicated inside the storage service, and a source
            // image that has since been deleted simply leaves the copy without one.
            var imageObjectKey = await recipeImageStore.CopyAsync(sourceRecipe.ImageObjectKey, cancellationToken);
            if (imageObjectKey is not null)
                recipe.SetImageObjectKey(imageObjectKey);
        }

        return recipe;
    }
}
