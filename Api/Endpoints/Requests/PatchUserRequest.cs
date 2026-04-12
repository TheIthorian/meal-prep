using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace Api.Endpoints.Requests;

public record PatchUserRequest([Required] [MaxLength(100)] string DisplayName);

public class PatchUserRequestValidator : AbstractValidator<PatchUserRequest>
{
    public PatchUserRequestValidator() {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MaximumLength(100)
            .WithMessage("Display name must not exceed 100 characters");
    }
}
