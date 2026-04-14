using Api.Models;

namespace Api.Endpoints.Responses.MealPrep;

public record MealPlanEntryResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid RecipeId,
    string RecipeTitle,
    string? RecipeDescription,
    DateOnly PlannedDate,
    string MealType,
    decimal? TargetServings,
    string? Notes,
    string Status,
    DateTime? CompletedAtUtc
);

/// <summary>
///     Maps meal-plan entries to API responses.
/// </summary>
public static class MealPlanResponseTransforms
{
    extension(MealPlanEntry entry)
    {
        public MealPlanEntryResponse ToMealPlanEntryResponse() {
            return new MealPlanEntryResponse(
                entry.Id,
                entry.WorkspaceId,
                entry.RecipeId,
                entry.Recipe.Title,
                entry.Recipe.Description,
                entry.PlannedDate,
                entry.MealType,
                entry.TargetServings,
                entry.Notes,
                entry.Status,
                entry.CompletedAtUtc
            );
        }
    }
}
