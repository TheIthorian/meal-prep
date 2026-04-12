using Api.Configuration;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
///     Summary of records removed by a retention cleanup pass.
/// </summary>
public sealed record RetentionCleanupResult(int WorkspacesDeleted);

/// <summary>
///     Deletes empty workspaces according to retention settings.
/// </summary>
public class RetentionCleanupService(
    ApiDbContext db,
    IOptions<RetentionCleanupOptions> options,
    ILogger<RetentionCleanupService> logger
)
{
    public async Task<RetentionCleanupResult> RunAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        var retentionDays = options.Value.RetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var numberOfExpiredWorkspaces = await RemoveExpiredWorkspacesAsync(cutoff, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var result = new RetentionCleanupResult(numberOfExpiredWorkspaces);

        logger.LogInformation(
            "Nightly retention cleanup removed {WorkspaceCount} workspaces older than {Cutoff}",
            result.WorkspacesDeleted,
            cutoff
        );

        return result;
    }

    private async Task<int> RemoveExpiredWorkspacesAsync(DateTime olderThanDate, CancellationToken cancellationToken) {
        // Get the expired workspaces
        var expiredWorkspaces = await db.Workspaces
            .Where(workspace => workspace.UpdatedAt < olderThanDate)
            .Include(workspace => workspace.Members)
            .Where(workspace => workspace.Members.Count == 0)
            .ToArrayAsync(cancellationToken);

        var expiredWorkspaceIds = expiredWorkspaces
            .Select(workspace => workspace.Id)
            .ToArray();

        db.Workspaces.RemoveRange(expiredWorkspaces);

        return expiredWorkspaceIds.Length;
    }
}
