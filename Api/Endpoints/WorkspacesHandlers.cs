using Api.Data;
using Api.Domain;
using Api.Endpoints.Requests;
using Api.Endpoints.Responses;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public class WorkspacesHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<WorkspaceResponse>> PostWorkspace(
        CurrentUserService currentUserService,
        ApiDbContext db,
        [FromBody] PostWorkspaceRequest body
    ) {
        var currentUser = await currentUserService.GetCurrentUserAsync();
        if (currentUser is null) throw new UnauthorizedException();

        var newWorkspace = Workspace.CreateNew(body.Name);
        newWorkspace.Members.Add(WorkspaceUser.CreateNew(currentUser, newWorkspace, WorkspaceUser.Roles.Owner));

        await db.Workspaces.AddAsync(newWorkspace);
        await db.SaveChangesAsync();

        return TypedResults.Json(newWorkspace.ToWorkspaceResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<IEnumerable<WorkspaceResponse>>> GetWorkspaces(
        CurrentUserService currentUserService,
        ApiDbContext db
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var workspaces = await db.Workspaces
            .Include(w => w.Members)
            .ThenInclude(uw => uw.User)
            .AsNoTracking()
            .ForCurrentUser(currentUserId)
            .OrderBy(w => w.Name)
            .ToArrayAsync();

        return TypedResults.Json(workspaces.Select(w => w.ToWorkspaceResponse()));
    }

    [Authorize]
    public static async Task<JsonHttpResult<WorkspaceResponse>> GetWorkspace(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId
    ) {
        var currentUserId = currentUserService.UserId;
        if (currentUserId is null) throw new UnauthorizedException();

        var workspace = await db.Workspaces
            .Include(w => w.Members)
            .ThenInclude(uw => uw.User)
            .ForCurrentUser(currentUserId)
            .Where(w => w.Id == workspaceId)
            .FirstOrDefaultAsync();

        return workspace is null
            ? throw new UnauthorizedException()
            : TypedResults.Json(workspace.ToWorkspaceResponse());
    }

    [Authorize]
    public static async Task<JsonHttpResult<MemberListItem>> PostWorkspacesUser(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        [FromBody] PostWorkspaceUserRequest body
    ) {
        var currentUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        var workspace = await db.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Id == workspaceId)
            .FirstOrDefaultAsync();

        if (currentUser is null || workspace is null) throw new UnauthorizedException();

        if (!CanUserModifyWorkspace(currentUser))
            throw new ForbiddenActionException("Only workspace admins and owners can invite new members", null);

        var otherUser = await db.Users
            .Where(u => u.NormalizedEmail != null && u.NormalizedEmail.Equals(body.Email.ToUpper()))
            .FirstOrDefaultAsync();

        if (otherUser == null) throw new EntityNotFoundException("User not found", null);

        if (workspace.Members.Any(member => member.UserId == otherUser.Id))
            throw new UserAlreadyMemberException();

        var newWorkspaceUser = WorkspaceUser.CreateNew(otherUser, workspace, WorkspaceUser.Roles.Member);
        workspace.Members.Add(newWorkspaceUser);
        await db.SaveChangesAsync();

        return TypedResults.Json(newWorkspaceUser.ToMemberListItem());
    }

    [Authorize]
    public static async Task<JsonHttpResult<MemberListItem>> PatchWorkspaceUserRole(
        [FromServices] ILogger<WorkspacesHandlers> logger,
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid userId,
        [FromBody] PatchWorkspaceUserRoleRequest body
    ) {
        var currentUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (currentUser is null) throw new UnauthorizedException();

        // Users cannot edit their own role
        var currentUserId = currentUser.UserId;
        if (currentUserId == userId)
            throw new InvalidOperationException("Cannot edit your own role");

        if (!CanUserModifyWorkspace(currentUser)) {
            logger.LogError("Current user role is {Role}", currentUser.Role);
            throw new ForbiddenActionException(
                "You do not have permission to change this role",
                "Only workspace admins and owners can manage workspace members"
            );
        }

        // Get workspace user is a member of
        var workspace = await db.Workspaces
            .Where(w => w.Id == workspaceId)
            .Include(w => w.Members)
            .ThenInclude(uw => uw.User)
            .Where(w => w.Members.Any(m => m.UserId == userId)) // check other user is in workspace
            .FirstOrDefaultAsync();

        var member = workspace?.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) throw new EntityNotFoundException("Member not found", null);

        if (member.Role == WorkspaceUser.Roles.Owner && currentUser.Role != WorkspaceUser.Roles.Owner) {
            logger.LogError("Cannot change role of owner. Current user role is {Role}", WorkspaceUser.Roles.Owner);
            throw new ForbiddenActionException(
                "You do not have permission to change this role",
                "Only other workspace owners can manage owner roles"
            );
        }

        // `member` is a Member or Admin

        member.Role = body.Role;
        await db.SaveChangesAsync();

        return TypedResults.Json(member.ToMemberListItem());
    }

    [Authorize]
    public static async Task<Results<Ok, NotFound>> DeleteWorkspaceUser(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        Guid userId
    ) {
        var currentUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        var workspace = await db.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Id == workspaceId)
            .FirstOrDefaultAsync();

        if (currentUser is null || workspace is null) throw new UnauthorizedException();

        if (!CanUserModifyWorkspace(currentUser))
            throw new ForbiddenActionException(
                "You do not have permission to manage this workspace",
                "Only workspace admins and owners can manage workspace users"
            );

        // Users cannot delete themselves
        if (currentUser.UserId.Equals(userId))
            throw new InvalidOperationException("Cannot remove yourself from the workspace");

        var member = workspace.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) throw new EntityNotFoundException("Member not found", null);

        // Cannot remove the owner
        if (member.Role == WorkspaceUser.Roles.Owner)
            throw new InvalidOperationException("Cannot remove the workspace owner");

        workspace.Members.Remove(member);
        await db.SaveChangesAsync();

        return TypedResults.Ok();
    }

    [Authorize]
    public static async Task<JsonHttpResult<WorkspaceResponse>> PatchWorkspace(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId,
        [FromBody] PostWorkspaceRequest body
    ) {
        var currentUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (currentUser is null) throw new UnauthorizedException();

        // Only admins and owners can rename workspaces
        if (!CanUserModifyWorkspace(currentUser))
            throw new ForbiddenActionException(
                "You do not have permission to manage this workspace",
                "Only workspace admins and owners can manage workspaces"
            );

        var workspace = await db.Workspaces
            .Include(w => w.Members)
            .ThenInclude(uw => uw.User)
            .Where(w => w.Id == workspaceId)
            .FirstOrDefaultAsync();

        if (workspace is null) throw new EntityNotFoundException("Workspace not found", null);

        workspace.Rename(body.Name);
        await db.SaveChangesAsync();

        return TypedResults.Json(workspace.ToWorkspaceResponse());
    }

    [Authorize]
    public static async Task<Results<Ok, NotFound>> DeleteWorkspace(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid workspaceId
    ) {
        var currentUser = await currentUserService.GetCurrentWorkspaceUserAsync(workspaceId);
        if (currentUser is null) throw new UnauthorizedException();

        // Only owners can delete workspaces
        if (currentUser.Role != WorkspaceUser.Roles.Owner)
            throw new ForbiddenActionException(
                "You do not have permission to delete this workspace",
                "Only workspace owners can delete workspaces"
            );

        var workspace = await db.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Id == currentUser.WorkspaceId)
            .FirstOrDefaultAsync();

        if (workspace is null) throw new EntityNotFoundException("Workspace not found", null);

        // Remove all members (soft delete of workspace by removing access)
        workspace.Members.Clear();
        await db.SaveChangesAsync();

        return TypedResults.Ok();
    }

    private static bool CanUserModifyWorkspace(WorkspaceUser user) {
        return user.Role == WorkspaceUser.Roles.Admin || user.Role == WorkspaceUser.Roles.Owner;
    }
}
