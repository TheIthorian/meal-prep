using Api.Models;

namespace Api.Endpoints.Responses;

/**
 * A member of a workspace
 */
public record MemberListItem(Guid UserId, string DisplayName, string Email, string Role);

public record UserResponse(Guid UserId, string DisplayName, string Email, WorkspaceListItem[] Workspaces);

/// <summary>
///     Maps user response models to API responses.
/// </summary>
public static class UserResponseTransforms
{
    extension(AppUser user)
    {
        public UserResponse ToUserResponse() {
            return new UserResponse(
                user.Id,
                user.DisplayName,
                user.Email ?? "",
                user.Workspaces.Select(workspace => workspace.ToWorkspaceListItem()).ToArray()
            );
        }
    }

    extension(WorkspaceUser workspaceUser)
    {
        public MemberListItem ToMemberListItem() {
            return new MemberListItem(
                workspaceUser.UserId,
                workspaceUser.User.DisplayName,
                workspaceUser.User.Email ?? "",
                workspaceUser.Role
            );
        }
    }
}
