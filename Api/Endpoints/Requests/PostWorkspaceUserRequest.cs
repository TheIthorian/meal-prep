using FluentValidation;

namespace Api.Endpoints.Requests;

public record PostWorkspaceUserRequest(string Email);

public class PostWorkspaceUserRequestValidator : AbstractValidator<PostWorkspaceUserRequest>
{
    public PostWorkspaceUserRequestValidator() {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
