using System.ComponentModel.DataAnnotations;
using Api.Models;
using FluentValidation;

namespace Api.Endpoints.Requests;

public record PatchWorkspaceUserRoleRequest([Required] [MaxLength(50)] string Role);

public class PatchWorkspaceUserRoleRequestValidator : AbstractValidator<PatchWorkspaceUserRoleRequest>
{
    public PatchWorkspaceUserRoleRequestValidator() {
        RuleFor(x => x.Role)
            .Must(r => WorkspaceUser.GetAllRoles().Contains(r))
            .WithMessage("Invalid role. Supported roles are :  " + string.Join(",", WorkspaceUser.GetAllRoles()));
    }
}
