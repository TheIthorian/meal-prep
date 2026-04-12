using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Api.Models;

/**
 * An individual member of a workspace
 */
public class AppUser : IdentityUser<Guid>
{
    [MaxLength(243)] public string DisplayName { get; set; } = string.Empty;

    public List<WorkspaceUser> Workspaces { get; private set; } = new();
}
