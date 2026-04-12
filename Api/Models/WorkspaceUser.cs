using System.ComponentModel.DataAnnotations;

namespace Api.Models;

/// <summary>
///     Represents a user's membership within a workspace.
/// </summary>
public class WorkspaceUser
{
    private WorkspaceUser() { } // used by EF Core

    private WorkspaceUser(AppUser user, Workspace workspace, string role) {
        User = user;
        UserId = user.Id;
        WorkspaceId = workspace.Id;
        Workspace = workspace;
        Role = role;
    }

    public AppUser User { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Workspace Workspace { get; private set; } = null!;
    [MaxLength(255)] public string Role { get; set; } = Roles.Owner;

    public static WorkspaceUser CreateNew(AppUser user, Workspace workspace, string role) {
        return new WorkspaceUser(user, workspace, role);
    }

    public static string[] GetAllRoles() {
        return new[] { Roles.Owner, Roles.Admin, Roles.Member };
    }

    public static class Roles
    {
        public static readonly string Owner = "owner";
        public static readonly string Admin = "admin";
        public static readonly string Member = "member";
    }
}
