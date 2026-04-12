using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
///     Resolves information about the authenticated user.
/// </summary>
public class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    UserManager<AppUser> userManager,
    ApiDbContext db
)
{
    public Guid? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is string userId
            ? Guid.Parse(userId)
            : null;

    public async Task<AppUser?> GetCurrentUserAsync()
    {
        using var methodTiming = System.Diagnostics.Activity.Current.BeginAppMethodEvent();

        var principal = httpContextAccessor.HttpContext?.User;
        return principal == null ? null : await userManager.GetUserAsync(principal);
    }

    /**
     * Prefer to use over GetCurrentUserAsync when you need the workspace entity
     */
    public async Task<WorkspaceUser?> GetCurrentWorkspaceUserAsync(Guid workspaceId)
    {
        using var methodTiming = System.Diagnostics.Activity.Current.BeginAppMethodEvent();

        return await db.WorkspaceUsers
            .Where(wu => wu.UserId == UserId)
            .Where(wu => wu.WorkspaceId == workspaceId)
            .Include(wu => wu.Workspace)
            .FirstOrDefaultAsync();
    }
}
