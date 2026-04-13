using Api.Data;
using Api.Domain;
using Api.Endpoints.Requests;
using Api.Endpoints.Responses;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegisterRequest = Api.Endpoints.Requests.RegisterRequest;

namespace Api.Endpoints;

internal static class AuthHandlers
{
    [AllowAnonymous]
    [HttpPost]
    public static async Task<IResult> PostRegister(
        UserManager<AppUser> userManager,
        ApiDbContext db,
        [FromBody] RegisterRequest body
    ) {
        var displayName = string.IsNullOrWhiteSpace(body.DisplayName) ? body.Email : body.DisplayName;
        var user = new AppUser { UserName = body.Email, Email = body.Email, DisplayName = displayName };
        var result = await userManager.CreateAsync(user, body.Password);

        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        // Create default workspace
        var workspaceName = $"{displayName}'s Workspace";
        var workspace = Workspace.CreateNew(workspaceName);
        workspace.Members.Add(WorkspaceUser.CreateNew(user, workspace, WorkspaceUser.Roles.Owner));

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "User registered successfully" });
    }

    [Authorize]
    [HttpPost]
    public static async Task<IResult> PostLogout(UserManager<AppUser> userManager, HttpContext ctx) {
        var user = await userManager.GetUserAsync(ctx.User);
        if (user is null) return Results.Unauthorized();

        // Delete all user tokens
        await userManager.RemoveAuthenticationTokenAsync(user, "IdentityApi", "access_token");
        await userManager.RemoveAuthenticationTokenAsync(user, "IdentityApi", "refresh_token");
        ctx.Response.Cookies.Delete(".AspNetCore.Identity.Application");
        return Results.Json(new { message = "Token invalidated" });
    }

    [Authorize]
    [HttpGet]
    public static async Task<JsonHttpResult<UserResponse>> GetMe(
        CurrentUserService currentUserService,
        ApiDbContext db
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        var user = await db.Users
            .Include(u => u.Workspaces)
            .ThenInclude(wu => wu.Workspace)
            .Where(u => u.Id == userId)
            .FirstAsync();

        return TypedResults.Json(user.ToUserResponse());
    }

    [Authorize]
    [HttpPatch]
    public static async Task<JsonHttpResult<UserResponse>> PatchMe(
        CurrentUserService currentUserService,
        ApiDbContext db,
        [FromBody] PatchUserRequest body
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        var user = await db.Users
            .Include(u => u.Workspaces)
            .ThenInclude(wu => wu.Workspace)
            .Where(u => u.Id == userId)
            .FirstAsync();

        user.DisplayName = body.DisplayName;
        await db.SaveChangesAsync();

        return TypedResults.Json(user.ToUserResponse());
    }

    [Authorize]
    [HttpDelete]
    public static async Task<IResult> DeleteMe(
        CurrentUserService currentUserService,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApiDbContext db
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        var user = await db.Users
            .Include(u => u.Workspaces)
            .ThenInclude(wu => wu.Workspace)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return Results.NotFound();

        // Check if user is owner of any workspace
        var ownedWorkspaces = user.Workspaces.Where(wu => wu.Role == WorkspaceUser.Roles.Owner).ToList();
        if (ownedWorkspaces.Any())
            throw new ForbiddenActionException(
                "Cannot delete account",
                "You cannot delete your account while you are the owner of one or more workspaces. Please transfer ownership or delete the workspaces first."
            );

        // Remove user from all workspaces (WorkspaceUser records)
        db.WorkspaceUsers.RemoveRange(user.Workspaces);
        await db.SaveChangesAsync();

        // Delete the user
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return Results.BadRequest(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        await signInManager.SignOutAsync();
        return Results.Ok(new { message = "Account deleted successfully" });
    }
}
