using Api.Models;

namespace Api.Endpoints.Responses;

/**
 * A workspace the User is a member of
 */
public record WorkspaceListItem(Guid WorkspaceId, string Name, string Role);

/**
 * A workspace with details
 */
public record WorkspaceResponse(Guid Id, string Name, MemberListItem[] Members);

/// <summary>
///     Maps workspace response models to API responses.
/// </summary>
public static class WorkspaceResponseTransforms
{
    extension(Workspace workspace)
    {
        public WorkspaceResponse ToWorkspaceResponse() {
            return new WorkspaceResponse(
                workspace.Id,
                workspace.Name,
                workspace.Members.Select(m => m.ToMemberListItem()).ToArray()
            );
        }
    }

    extension(WorkspaceUser workspaceUser)
    {
        public WorkspaceListItem ToWorkspaceListItem() {
            return new WorkspaceListItem(workspaceUser.WorkspaceId, workspaceUser.Workspace.Name, workspaceUser.Role);
        }
    }
}
