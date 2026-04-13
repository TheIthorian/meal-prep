using Api.Services.MealPrep;
using FluentValidation;

namespace Api.Endpoints.Requests.MealPrep;

public record SaveRecipeIngredientRequest(
    string Name,
    string? NormalizedIngredientName,
    decimal? Amount,
    string? Unit,
    string? PreparationNote,
    string? Section,
    string DisplayText
);

public record SaveRecipeStepRequest(string Instruction, int? TimerSeconds);

public record SaveRecipeNutrientRequest(string NutrientType, decimal Amount);

public record SaveRecipeNutritionRequest(decimal? ServingBasis, SaveRecipeNutrientRequest[] Nutrients);

public record SaveRecipeRequest(
    string Title,
    string? Description,
    decimal Servings,
    string? SourceUrl,
    string? Notes,
    int? PrepMinutes,
    int? CookMinutes,
    bool IsArchived,
    string[] Tags,
    SaveRecipeIngredientRequest[] Ingredients,
    SaveRecipeStepRequest[] Steps,
    SaveRecipeNutritionRequest? Nutrition,
    string? ImportImageUrl
);

public record ImportRecipeRequest(string Url);

public record ImportRecipePreviewRequest(string Url);

public class SaveRecipeRequestValidator : AbstractValidator<SaveRecipeRequest>
{
    public SaveRecipeRequestValidator() {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.PrepMinutes).GreaterThanOrEqualTo(0).When(x => x.PrepMinutes.HasValue);
        RuleFor(x => x.CookMinutes).GreaterThanOrEqualTo(0).When(x => x.CookMinutes.HasValue);
        RuleForEach(x => x.Tags).MaximumLength(64);
        RuleFor(x => x.Ingredients).NotEmpty();
        RuleFor(x => x.Steps).NotEmpty();
        RuleForEach(x => x.Ingredients).SetValidator(new SaveRecipeIngredientRequestValidator());
        RuleForEach(x => x.Steps).SetValidator(new SaveRecipeStepRequestValidator());
        When(x => x.Nutrition is not null, () => { RuleFor(x => x.Nutrition!).SetValidator(new SaveRecipeNutritionRequestValidator()); });
        RuleFor(x => x.ImportImageUrl).MaximumLength(2048);
        When(
            x => !string.IsNullOrWhiteSpace(x.ImportImageUrl),
            () => {
                RuleFor(x => x.ImportImageUrl!)
                    .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var u)
                                 && (string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(u.Scheme, "http", StringComparison.OrdinalIgnoreCase)))
                    .WithMessage("Import image URL must be an absolute http(s) URL.");
                RuleFor(x => x).Must(x =>
                        !string.IsNullOrWhiteSpace(x.SourceUrl)
                        && RecipeImportImagePolicy.AreHostsCompatibleForImportedImage(x.SourceUrl!, x.ImportImageUrl!)
                    )
                    .WithMessage("Import image must use the same site as the recipe source URL.");
            }
        );
    }
}

public class SaveRecipeIngredientRequestValidator : AbstractValidator<SaveRecipeIngredientRequest>
{
    public SaveRecipeIngredientRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DisplayText).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.Unit).MaximumLength(64);
        RuleFor(x => x.NormalizedIngredientName).MaximumLength(255);
        RuleFor(x => x.PreparationNote).MaximumLength(255);
        RuleFor(x => x.Section).MaximumLength(255);
    }
}

public class SaveRecipeStepRequestValidator : AbstractValidator<SaveRecipeStepRequest>
{
    public SaveRecipeStepRequestValidator() {
        RuleFor(x => x.Instruction).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.TimerSeconds).GreaterThanOrEqualTo(0).When(x => x.TimerSeconds.HasValue);
    }
}

public class SaveRecipeNutritionRequestValidator : AbstractValidator<SaveRecipeNutritionRequest>
{
    public SaveRecipeNutritionRequestValidator() {
        RuleFor(x => x.ServingBasis).GreaterThan(0).When(x => x.ServingBasis.HasValue);
        RuleFor(x => x.Nutrients).NotEmpty();
        RuleForEach(x => x.Nutrients).SetValidator(new SaveRecipeNutrientRequestValidator());
    }
}

public class SaveRecipeNutrientRequestValidator : AbstractValidator<SaveRecipeNutrientRequest>
{
    public SaveRecipeNutrientRequestValidator() {
        RuleFor(x => x.NutrientType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public class ImportRecipePreviewRequestValidator : AbstractValidator<ImportRecipePreviewRequest>
{
    public ImportRecipePreviewRequestValidator() {
        RuleFor(x => x.Url).NotEmpty().Must(url => Uri.TryCreate(url, UriKind.Absolute, out _));
    }
}

public class ImportRecipeRequestValidator : AbstractValidator<ImportRecipeRequest>
{
    public ImportRecipeRequestValidator() {
        RuleFor(x => x.Url).NotEmpty().Must(url => Uri.TryCreate(url, UriKind.Absolute, out _));
    }
}
