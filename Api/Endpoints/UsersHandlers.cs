using Api.Data;
using Api.Domain;
using Api.Endpoints.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

internal static class UsersHandlers
{
    [Authorize]
    [HttpGet]
    public static async Task<JsonHttpResult<UserResponse>> GetUser(ApiDbContext db, Guid userId)
    {
        var user = await db.Users
            .Include(u => u.Workspaces)
            .ThenInclude(uw => uw.Workspace)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user == null
            ? throw new EntityNotFoundException("User not found", null)
            : TypedResults.Json(user.ToUserResponse());
    }
}
