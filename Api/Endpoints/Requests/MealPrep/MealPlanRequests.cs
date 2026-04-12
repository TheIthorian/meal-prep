using Api.Models;
using FluentValidation;

namespace Api.Endpoints.Requests.MealPrep;

public record SaveMealPlanEntryRequest(
    Guid RecipeId,
    DateOnly PlannedDate,
    string MealType,
    decimal? TargetServings,
    string? Notes,
    string Status
);

public class SaveMealPlanEntryRequestValidator : AbstractValidator<SaveMealPlanEntryRequest>
{
    public SaveMealPlanEntryRequestValidator() {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.MealType).Must(MealPlanEntryMealTypes.All.Contains);
        RuleFor(x => x.Status).Must(MealPlanEntryStatuses.All.Contains);
        RuleFor(x => x.TargetServings).GreaterThan(0).When(x => x.TargetServings.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
