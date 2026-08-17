using Api.Links;
using Api.Models;
using Api.Services.MealPrep;

namespace Api.Endpoints.Responses.MealPrep;

public record RecipeListItemResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Servings,
    bool IsArchived,
    string[] Tags,
    string? SourceUrl,
    int IngredientCount,
    int StepCount,
    bool HasImage,
    bool IsFavorite,
    string? WebUrl = null,
    string? ResourceUrl = null
);

public record RecipeIngredientResponse(
    Guid Id,
    int SortOrder,
    string Name,
    string? NormalizedIngredientName,
    decimal? Amount,
    string? Unit,
    string? PreparationNote,
    string? Section,
    string DisplayText
);

public record RecipeStepResponse(Guid Id, int SortOrder, string Instruction, int? TimerSeconds);

public record RecipeNutrientResponse(Guid Id, string NutrientType, decimal Amount);

public record RecipeNutritionResponse(decimal? ServingBasis, RecipeNutrientResponse[] Nutrients);

public record RecipeResponse(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string? Description,
    decimal Servings,
    string? SourceUrl,
    string? Notes,
    int? PrepMinutes,
    int? CookMinutes,
    bool IsArchived,
    string[] Tags,
    bool HasImage,
    bool IsFavorite,
    RecipeCollectionMembershipResponse[] Collections,
    RecipeIngredientResponse[] Ingredients,
    RecipeStepResponse[] Steps,
    RecipeNutritionResponse? Nutrition,
    string? WebUrl = null,
    string? ResourceUrl = null
);

public record RecipeImportPreviewResponse(
    string Title,
    string? Description,
    decimal Servings,
    string SourceUrl,
    int? PrepMinutes,
    int? CookMinutes,
    string[] Tags,
    RecipeIngredientResponse[] Ingredients,
    RecipeStepResponse[] Steps,
    RecipeNutritionResponse? Nutrition,
    string? ImageUrl
);

public record RecipeTagListResponse(string[] Tags);

public record SuggestRecipeTagsResponse(string[] Tags);

public record RecipeTagUsageItemResponse(string Tag, int RecipeCount);

public record RecipeTagUsageListResponse(RecipeTagUsageItemResponse[] Items);

public record BulkRemoveRecipeTagsResponse(int RecipesUpdated, string[] TagsProcessed);

/// <summary>
///     Attaches the web-app page link and the API self link to recipe responses. Callers only ever see ids
///     otherwise, and an id is not something a client can navigate to.
/// </summary>
public static class RecipeResponseLinks
{
    extension(RecipeResponse response)
    {
        public RecipeResponse WithLinks(WebAppLinkGenerator webApp, ResourceLinkGenerator resources) {
            return response with {
                WebUrl = webApp.Recipe(response.WorkspaceId, response.Id),
                ResourceUrl = resources.Recipe(response.WorkspaceId, response.Id)
            };
        }
    }

    extension(RecipeListItemResponse response)
    {
        public RecipeListItemResponse WithLinks(
            Guid workspaceId,
            WebAppLinkGenerator webApp,
            ResourceLinkGenerator resources
        ) {
            return response with {
                WebUrl = webApp.Recipe(workspaceId, response.Id),
                ResourceUrl = resources.Recipe(workspaceId, response.Id)
            };
        }
    }
}

/// <summary>
///     Maps recipe domain models and import previews to API responses.
/// </summary>
public static class RecipeResponseTransforms
{
    extension(Recipe recipe)
    {
        public RecipeListItemResponse ToRecipeListItemResponse(bool isFavorite) {
            return new RecipeListItemResponse(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.IsArchived,
                RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags),
                recipe.SourceUrl,
                recipe.Ingredients.Count,
                recipe.Steps.Count,
                !string.IsNullOrEmpty(recipe.ImageObjectKey),
                isFavorite
            );
        }

        public RecipeResponse ToRecipeResponse(
            bool isFavorite,
            RecipeCollectionMembershipResponse[]? collections = null
        ) {
            return new RecipeResponse(
                recipe.Id,
                recipe.WorkspaceId,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.SourceUrl,
                recipe.Notes,
                recipe.PrepMinutes,
                recipe.CookMinutes,
                recipe.IsArchived,
                RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags),
                !string.IsNullOrEmpty(recipe.ImageObjectKey),
                isFavorite,
                collections ?? [],
                recipe.Ingredients
                    .OrderBy(ingredient => ingredient.SortOrder)
                    .Select(ingredient => ingredient.ToResponse())
                    .ToArray(),
                recipe.Steps.OrderBy(step => step.SortOrder).Select(step => step.ToResponse()).ToArray(),
                recipe.ToNutritionResponse()
            );
        }

        public SharedRecipeSummaryResponse ToSharedRecipeSummaryResponse() {
            return new SharedRecipeSummaryResponse(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.PrepMinutes,
                recipe.CookMinutes,
                RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags),
                !string.IsNullOrEmpty(recipe.ImageObjectKey)
            );
        }

        public SharedRecipeDetailResponse ToSharedRecipeDetailResponse() {
            return new SharedRecipeDetailResponse(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Servings,
                recipe.SourceUrl,
                recipe.Notes,
                recipe.PrepMinutes,
                recipe.CookMinutes,
                RecipeTagWhitelist.NormalizeToWhitelist(recipe.Tags),
                !string.IsNullOrEmpty(recipe.ImageObjectKey),
                recipe.Ingredients
                    .OrderBy(ingredient => ingredient.SortOrder)
                    .Select(ingredient => ingredient.ToResponse())
                    .ToArray(),
                recipe.Steps.OrderBy(step => step.SortOrder).Select(step => step.ToResponse()).ToArray(),
                recipe.ToNutritionResponse()
            );
        }

        private RecipeNutritionResponse? ToNutritionResponse() {
            if (recipe.NutritionServingBasis is null && recipe.Nutrition.Count == 0) return null;

            return new RecipeNutritionResponse(
                recipe.NutritionServingBasis,
                recipe.Nutrition
                    .OrderBy(nutrient => RecipeNutrientOrdering.GetSortIndex(nutrient.NutrientType))
                    .ThenBy(nutrient => nutrient.NutrientType)
                    .Select(nutrient => nutrient.ToResponse())
                    .ToArray()
            );
        }
    }

    extension(RecipeIngredient ingredient)
    {
        public RecipeIngredientResponse ToResponse() {
            return new RecipeIngredientResponse(
                ingredient.Id,
                ingredient.SortOrder,
                ingredient.Name,
                ingredient.NormalizedIngredientName,
                ingredient.Amount,
                ingredient.Unit,
                ingredient.PreparationNote,
                ingredient.Section,
                ingredient.DisplayText
            );
        }
    }

    extension(RecipeStep step)
    {
        public RecipeStepResponse ToResponse() {
            return new RecipeStepResponse(step.Id, step.SortOrder, step.Instruction, step.TimerSeconds);
        }
    }

    extension(RecipeNutrition nutrition)
    {
        public RecipeNutrientResponse ToResponse() {
            return new RecipeNutrientResponse(nutrition.Id, nutrition.NutrientType, nutrition.Amount);
        }
    }

    extension(RecipeImportPreview preview)
    {
        public RecipeImportPreviewResponse ToResponse() {
            return new RecipeImportPreviewResponse(
                preview.Title,
                preview.Description,
                preview.Servings,
                preview.SourceUrl,
                preview.PrepMinutes,
                preview.CookMinutes,
                RecipeTagWhitelist.NormalizeToWhitelist(preview.Tags),
                preview.Ingredients
                    .Select(ingredient => new RecipeIngredientResponse(
                            Guid.Empty,
                            ingredient.SortOrder,
                            ingredient.Name,
                            ingredient.NormalizedIngredientName,
                            ingredient.Amount,
                            ingredient.Unit,
                            ingredient.PreparationNote,
                            ingredient.Section,
                            ingredient.DisplayText
                        )
                    )
                    .ToArray(),
                preview.Steps
                    .Select(step => new RecipeStepResponse(
                            Guid.Empty,
                            step.SortOrder,
                            step.Instruction,
                            step.TimerSeconds
                        )
                    )
                    .ToArray(),
                preview.Nutrition is null
                    ? null
                    : new RecipeNutritionResponse(
                        preview.Nutrition.ServingBasis,
                        preview.Nutrition
                            .Nutrients
                            .OrderBy(nutrient => RecipeNutrientOrdering.GetSortIndex(nutrient.NutrientType))
                            .ThenBy(nutrient => nutrient.NutrientType)
                            .Select(nutrient => new RecipeNutrientResponse(
                                    Guid.Empty,
                                    nutrient.NutrientType,
                                    nutrient.Amount
                                )
                            )
                            .ToArray()
                    ),
                preview.ImageUrl
            );
        }
    }
}

internal static class RecipeNutrientOrdering
{
    public static int GetSortIndex(string nutrientType) {
        var index = Array.IndexOf(RecipeNutrientTypes.DefaultOrder, nutrientType);
        return index >= 0 ? index : int.MaxValue;
    }
}
