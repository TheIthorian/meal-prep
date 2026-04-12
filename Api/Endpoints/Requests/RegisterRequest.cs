using FluentValidation;

namespace Api.Endpoints.Requests;

public record RegisterRequest(string Email, string Password, string? DisplayName);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator() {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}
