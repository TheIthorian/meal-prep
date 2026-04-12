using FluentValidation;

namespace Api.Endpoints.Requests;

public record PostWorkspaceRequest(string Name);

public class PostWorkspaceRequestValidator : AbstractValidator<PostWorkspaceRequest>
{
    public PostWorkspaceRequestValidator() {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}
