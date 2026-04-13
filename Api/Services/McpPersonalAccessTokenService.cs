using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
///     Generates opaque MCP personal access tokens, persists their hashes, and validates incoming tokens.
/// </summary>
public sealed class McpPersonalAccessTokenService(ApiDbContext db)
{
    public static string GenerateOpaqueToken() {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] HashToken(string plainToken) {
        return SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
    }

    public async Task<McpTokenValidationResult?> ValidateAndTouchAsync(
        string plainToken,
        CancellationToken cancellationToken
    ) {
        var hash = HashToken(plainToken);
        var row = await db.McpPersonalAccessTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (row is null || row.RevokedAt is not null) return null;

        row.Touch();
        await db.SaveChangesAsync(cancellationToken);
        return new McpTokenValidationResult(row.User, row.WorkspaceId);
    }
}
