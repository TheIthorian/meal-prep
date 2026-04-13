using FluentValidation;

namespace Api.Endpoints.Requests.MealPrep;

public record GenerateShoppingListRequest(string Name, string? Notes, Guid[] RecipeIds, Guid[] MealPlanEntryIds);

public record SaveShoppingListRequest(string Name, string? Notes);

public record SaveShoppingListItemRequest(
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
    string[]? SourceNames = null
);

public class GenerateShoppingListRequestValidator : AbstractValidator<GenerateShoppingListRequest>
{
    public GenerateShoppingListRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x)
            .Must(x => x.RecipeIds.Length > 0 || x.MealPlanEntryIds.Length > 0)
            .WithMessage("Select at least one recipe or meal-plan entry.");
    }
}

public class SaveShoppingListRequestValidator : AbstractValidator<SaveShoppingListRequest>
{
    public SaveShoppingListRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class SaveShoppingListItemRequestValidator : AbstractValidator<SaveShoppingListItemRequest>
{
    public SaveShoppingListItemRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.NormalizedIngredientName).MaximumLength(255);
        RuleFor(x => x.Unit).MaximumLength(64);
        RuleFor(x => x.Category).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(255);
        RuleFor(x => x.DisplayText).NotEmpty().MaximumLength(1024);
        RuleForEach(x => x.SourceNames ?? Array.Empty<string>())
            .MaximumLength(255)
            .OverridePropertyName(nameof(SaveShoppingListItemRequest.SourceNames));
        RuleFor(x => x.SourceNames)
            .Must(names => names is null || names.Length <= 64)
            .WithMessage("At most 64 source names.");
    }
}
