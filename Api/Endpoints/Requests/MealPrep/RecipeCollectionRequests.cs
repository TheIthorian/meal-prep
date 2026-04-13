using FluentValidation;

namespace Api.Endpoints.Requests.MealPrep;

public record CreateRecipeCollectionRequest(string Name, string? Description);

public record PatchRecipeCollectionRequest(string Name, string? Description);

public record AddRecipeToCollectionRequest(Guid RecipeId);

public record ShareRecipeCollectionRequest(Guid TargetWorkspaceId);

public class CreateRecipeCollectionRequestValidator : AbstractValidator<CreateRecipeCollectionRequest>
{
    public CreateRecipeCollectionRequestValidator() {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Description).MaximumLength(2000);
    }
}

public class PatchRecipeCollectionRequestValidator : AbstractValidator<PatchRecipeCollectionRequest>
{
    public PatchRecipeCollectionRequestValidator() {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Description).MaximumLength(2000);
    }
}

public class AddRecipeToCollectionRequestValidator : AbstractValidator<AddRecipeToCollectionRequest>
{
    public AddRecipeToCollectionRequestValidator() {
        RuleFor(request => request.RecipeId).NotEmpty();
    }
}

public class ShareRecipeCollectionRequestValidator : AbstractValidator<ShareRecipeCollectionRequest>
{
    public ShareRecipeCollectionRequestValidator() {
        RuleFor(request => request.TargetWorkspaceId).NotEmpty();
    }
}
