using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(ApiDbContext context)
    {
        Context = context;
    }

    public ApiDbContext Context { get; }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        try
        {
            await TestDatabaseInitializer.EnsureMigratedAsync();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseNpgsql(TestEnvironment.GetDatabaseConnectionString())
                .Options;

            var context = new ApiDbContext(options);
            return new TestDatabase(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to Postgres for tests. Start the stack with "
                + "`docker compose -f compose.yaml -f compose.test.yaml up -d` and run tests inside "
                + "the `tests` container. Check .env.test for connection settings.",
                ex
            );
        }
    }
}
