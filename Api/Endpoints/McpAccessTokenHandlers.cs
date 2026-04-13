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

internal static class McpAccessTokenHandlers
{
    [Authorize]
    public static async Task<JsonHttpResult<McpAccessTokenCreatedResponse>> PostMcpAccessToken(
        CurrentUserService currentUserService,
        ApiDbContext db,
        HttpContext httpContext,
        [FromBody] PostMcpAccessTokenRequest body
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        if (await currentUserService.GetCurrentWorkspaceUserAsync(body.WorkspaceId) is null)
            throw new EntityNotFoundException("Workspace not found", null);

        var plain = McpPersonalAccessTokenService.GenerateOpaqueToken();
        var hash = McpPersonalAccessTokenService.HashToken(plain);
        var entity = McpPersonalAccessToken.CreateNew(userId.Value, body.WorkspaceId, hash, body.Name);
        db.McpPersonalAccessTokens.Add(entity);
        await db.SaveChangesAsync();

        var mcpUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/mcp/{plain}";
        return TypedResults.Json(new McpAccessTokenCreatedResponse(entity.Id, body.WorkspaceId, plain, mcpUrl));
    }

    [Authorize]
    public static async Task<JsonHttpResult<McpAccessTokenListItemResponse[]>> GetMcpAccessTokens(
        CurrentUserService currentUserService,
        ApiDbContext db
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        var rows = await db.McpPersonalAccessTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new McpAccessTokenListItemResponse(
                    t.Id,
                    t.WorkspaceId,
                    t.Name,
                    t.CreatedAt,
                    t.LastUsedAt,
                    t.RevokedAt
                )
            )
            .ToArrayAsync();

        return TypedResults.Json(rows);
    }

    [Authorize]
    public static async Task<IResult> DeleteMcpAccessToken(
        CurrentUserService currentUserService,
        ApiDbContext db,
        Guid tokenId
    ) {
        var userId = currentUserService.UserId;
        if (userId is null) throw new UnauthorizedException();

        var row = await db.McpPersonalAccessTokens
            .Where(t => t.UserId == userId && t.Id == tokenId)
            .FirstOrDefaultAsync();

        if (row is null) throw new EntityNotFoundException("Token not found", null);

        row.Revoke();
        await db.SaveChangesAsync();
        return Results.Ok();
    }
}
