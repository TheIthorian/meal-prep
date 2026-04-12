using FluentValidation;

namespace Api.Endpoints.Requests;

public record PostMcpAccessTokenRequest(Guid WorkspaceId, string? Name);

public class PostMcpAccessTokenRequestValidator : AbstractValidator<PostMcpAccessTokenRequest>
{
    public PostMcpAccessTokenRequestValidator() {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(128).When(x => !string.IsNullOrEmpty(x.Name));
    }
}
