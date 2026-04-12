using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

internal static class TestDatabaseInitializer
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool migrationsApplied;

    public static async Task EnsureMigratedAsync()
    {
        if (migrationsApplied) return;

        await MigrationLock.WaitAsync();
        try
        {
            if (migrationsApplied) return;

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseNpgsql(TestEnvironment.GetDatabaseConnectionString())
                .Options;

            await using var context = new ApiDbContext(options);
            await DatabaseMigrationCoordinator.MigrateWithLockAsync(context);
            migrationsApplied = true;
        }
        finally
        {
            MigrationLock.Release();
        }
    }
}
