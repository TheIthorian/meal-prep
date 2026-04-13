using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Data;

/// <summary>
///     Coordinates database migrations so they run safely.
/// </summary>
public static class DatabaseMigrationCoordinator
{
    private const long MigrationLockId = 583014270184512391;

    public static async Task MigrateWithLockAsync(ApiDbContext db, CancellationToken cancellationToken = default) {
        await EnsureDatabaseExistsAsync(db, cancellationToken);

        await db.Database.OpenConnectionAsync(cancellationToken);
        try {
            await AcquireLockAsync(db, cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        } finally {
            await ReleaseLockAsync(db, cancellationToken);
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureDatabaseExistsAsync(ApiDbContext db, CancellationToken cancellationToken) {
        var targetConnectionString = db.Database.GetConnectionString()
                                     ?? throw new InvalidOperationException(
                                         "Database connection string was not configured."
                                     );
        var targetConnectionBuilder = new NpgsqlConnectionStringBuilder(targetConnectionString);

        if (string.IsNullOrWhiteSpace(targetConnectionBuilder.Database))
            throw new InvalidOperationException("Database connection string must include a database name.");

        await using var adminConnection = CreateAdminConnection(targetConnectionBuilder);
        await adminConnection.OpenAsync(cancellationToken);

        try {
            await AcquireLockAsync(adminConnection, cancellationToken);

            if (await DatabaseExistsAsync(adminConnection, targetConnectionBuilder.Database, cancellationToken))
                return;

            await CreateDatabaseAsync(adminConnection, targetConnectionBuilder.Database, cancellationToken);
        } finally {
            await ReleaseLockAsync(adminConnection, cancellationToken);
        }
    }

    private static async Task AcquireLockAsync(ApiDbContext db, CancellationToken cancellationToken) {
        await using var command = CreateLockCommand(db, "pg_advisory_lock");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(ApiDbContext db, CancellationToken cancellationToken) {
        await using var command = CreateLockCommand(db, "pg_advisory_unlock");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken) {
        await using var command = CreateLockCommand(connection, "pg_advisory_lock");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken) {
        await using var command = CreateLockCommand(connection, "pg_advisory_unlock");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> DatabaseExistsAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken
    ) {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
        command.Parameters.AddWithValue("databaseName", databaseName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task CreateDatabaseAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken
    ) {
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)}";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlConnection CreateAdminConnection(NpgsqlConnectionStringBuilder targetConnectionBuilder) {
        var adminConnectionBuilder = new NpgsqlConnectionStringBuilder(targetConnectionBuilder.ConnectionString) {
            Database = "postgres"
        };
        return new NpgsqlConnection(adminConnectionBuilder.ConnectionString);
    }

    private static string QuoteIdentifier(string identifier) {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static NpgsqlCommand CreateLockCommand(ApiDbContext db, string functionName) {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        return CreateLockCommand(connection, functionName);
    }

    private static NpgsqlCommand CreateLockCommand(NpgsqlConnection connection, string functionName) {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT {functionName}(@lockId)";
        command.Parameters.AddWithValue("lockId", MigrationLockId);
        return command;
    }
}
