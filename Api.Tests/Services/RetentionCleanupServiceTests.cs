using Api.Data;
using Api.Configuration;
using Api.Models;
using Api.Services;
using Api.Tests.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Services;

public sealed class RetentionCleanupServiceTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public RetentionCleanupServiceTests(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RunAsync_RemovesExpiredUploadsAndExpiredEmptyWorkspaces()
    {
        var staleTimestamp = DateTime.UtcNow.AddDays(-(RetentionCleanupOptions.DefaultRetentionDays + 1));
        var freshTimestamp = DateTime.UtcNow.AddDays(-2);

        var seededIds = await SeedCleanupScenarioAsync(staleTimestamp, freshTimestamp);

        using (var cleanupScope = factory.Services.CreateScope())
        {
            var cleanupService = cleanupScope.ServiceProvider.GetRequiredService<RetentionCleanupService>();
            await cleanupService.RunAsync();
        }

        using var assertScope = factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApiDbContext>();

        Assert.Null(await db.Workspaces.FindAsync(seededIds.ExpiredWorkspaceId));
        Assert.NotNull(await db.Workspaces.FindAsync(seededIds.ActiveWorkspaceId));
    }

    private async Task<CleanupSeedIds> SeedCleanupScenarioAsync(DateTime staleTimestamp, DateTime freshTimestamp)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var user = CreateUser();

        var expiredWorkspace = Workspace.CreateNew($"expired-workspace-{Guid.NewGuid():N}");
        var activeWorkspace = Workspace.CreateNew($"active-workspace-{Guid.NewGuid():N}");
        var freshWorkspace = Workspace.CreateNew($"fresh-workspace-{Guid.NewGuid():N}");

        db.Users.Add(user);
        db.Workspaces.AddRange(expiredWorkspace, activeWorkspace, freshWorkspace);
        db.WorkspaceUsers.AddRange(
            WorkspaceUser.CreateNew(user, activeWorkspace, WorkspaceUser.Roles.Owner),
            WorkspaceUser.CreateNew(user, freshWorkspace, WorkspaceUser.Roles.Owner)
        );

        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Workspaces""
               SET ""CreatedAt"" = {staleTimestamp},
                   ""UpdatedAt"" = {staleTimestamp}
               WHERE ""Id"" = {expiredWorkspace.Id}"
        );

        return new CleanupSeedIds(
            expiredWorkspace.Id,
            activeWorkspace.Id
        );
    }

    private static AppUser CreateUser()
    {
        var userId = Guid.NewGuid();
        var email = $"cleanup-{userId:N}@tests.local";

        return new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
    }

    private sealed record CleanupSeedIds(
        Guid ExpiredWorkspaceId,
        Guid ActiveWorkspaceId
    );
}
