using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Api.Models;

/// <summary>
///     A hashed personal access token used to authenticate MCP HTTP clients via a private URL segment.
/// </summary>
[Index(nameof(TokenHash), IsUnique = true)]
public sealed class McpPersonalAccessToken : Entity
{
    private McpPersonalAccessToken() { }

    public Guid UserId { get; private set; }
    public AppUser User { get; private set; } = null!;

    public Guid WorkspaceId { get; private set; }
    public Workspace Workspace { get; private set; } = null!;

    [MaxLength(32)]
    public byte[] TokenHash { get; private set; } = [];

    [MaxLength(128)]
    public string? Name { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public static McpPersonalAccessToken CreateNew(Guid userId, Guid workspaceId, byte[] tokenHash, string? name) {
        return new McpPersonalAccessToken {
            UserId = userId,
            WorkspaceId = workspaceId,
            TokenHash = tokenHash,
            Name = name,
        };
    }

    public void Revoke() {
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public void Touch() {
        LastUsedAt = DateTimeOffset.UtcNow;
    }
}
