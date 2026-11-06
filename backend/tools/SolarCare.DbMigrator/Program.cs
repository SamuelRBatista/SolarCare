using System.Security.Cryptography;
using System.Text;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SolarCare")
    ?? throw new InvalidOperationException("Set ConnectionStrings__SolarCare before running migrations.");
var migrationDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
if (!Directory.Exists(migrationDirectory))
{
    throw new DirectoryNotFoundException($"Migration directory was not found: {migrationDirectory}");
}

await using var dataSource = NpgsqlDataSource.Create(connectionString);
await using var connection = await dataSource.OpenConnectionAsync();
await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(73829104612);", connection))
{
    await lockCommand.ExecuteNonQueryAsync();
}

await using (var preflight = new NpgsqlCommand(
    """
    SELECT to_regclass('public.schema_migrations') IS NOT NULL,
           to_regclass('public.tenants') IS NOT NULL
           OR to_regclass('public.users') IS NOT NULL
           OR to_regclass('public.refresh_tokens') IS NOT NULL;
    """,
    connection))
await using (var reader = await preflight.ExecuteReaderAsync())
{
    await reader.ReadAsync();
    if (!reader.GetBoolean(0) && reader.GetBoolean(1))
    {
        throw new InvalidOperationException(
            "Authentication tables exist without migration history. Back up the database and baseline it explicitly before applying migrations.");
    }
}

await using (var createHistory = new NpgsqlCommand(
    """
    CREATE TABLE IF NOT EXISTS schema_migrations (
        version varchar(120) PRIMARY KEY,
        checksum bytea NOT NULL,
        applied_at timestamptz NOT NULL DEFAULT now()
    );
    """,
    connection))
{
    await createHistory.ExecuteNonQueryAsync();
}

var migrations = Directory.GetFiles(migrationDirectory, "*.sql")
    .OrderBy(Path.GetFileName, StringComparer.Ordinal)
    .ToArray();
foreach (var migrationPath in migrations)
{
    var version = Path.GetFileName(migrationPath);
    var sql = await File.ReadAllTextAsync(migrationPath);
    var checksum = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
    await using var transaction = await connection.BeginTransactionAsync();
    await using (var existingCommand = new NpgsqlCommand(
        "SELECT checksum FROM schema_migrations WHERE version = @version;",
        connection,
        transaction))
    {
        existingCommand.Parameters.AddWithValue("version", version);
        var existing = await existingCommand.ExecuteScalarAsync();
        if (existing is byte[] existingChecksum)
        {
            if (!CryptographicOperations.FixedTimeEquals(existingChecksum, checksum))
            {
                throw new InvalidOperationException(
                    $"Applied migration '{version}' has changed. Add a new migration instead.");
            }

            await transaction.CommitAsync();
            Console.WriteLine($"Already applied: {version}");
            continue;
        }
    }

    await using (var migrationCommand = new NpgsqlCommand(sql, connection, transaction))
    {
        await migrationCommand.ExecuteNonQueryAsync();
    }

    await using (var recordCommand = new NpgsqlCommand(
        "INSERT INTO schema_migrations (version, checksum) VALUES (@version, @checksum);",
        connection,
        transaction))
    {
        recordCommand.Parameters.AddWithValue("version", version);
        recordCommand.Parameters.AddWithValue("checksum", checksum);
        await recordCommand.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();
    Console.WriteLine($"Applied: {version}");
}
