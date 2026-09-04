using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Lazuar.Pay.Tests;

/// <summary>
/// One Postgres 16 container for the suite. Each <see cref="PayApiFactory"/> gets a unique
/// database cloned from a migrated template so unique indexes, CAS, and transactions are real.
/// </summary>
[SetUpFixture]
internal sealed class PayPostgres
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static PostgreSqlContainer? Container;
    static string? AdminCs;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        try
        {
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Pay tests require Docker/Testcontainers Postgres 16. Start Docker and retry.",
                ex);
        }

        Container = container;
        AdminCs = container.GetConnectionString();

        await using (var admin = new NpgsqlConnection(AdminCs))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = "CREATE DATABASE pay_template";
            await cmd.ExecuteNonQueryAsync();
        }

        var templateCs = new NpgsqlConnectionStringBuilder(AdminCs) { Database = "pay_template" }.ConnectionString;
        var options = new DbContextOptionsBuilder<PayDbContext>().UseNpgsql(templateCs).Options;
        await using (var db = new PayDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        NpgsqlConnection.ClearAllPools();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }

    public static Task<PayApiFactory> FactoryAsync() => Task.FromResult(new PayApiFactory());

    public static string CreateDatabase()
    {
        if (AdminCs is null)
        {
            throw new InvalidOperationException(
                "PayPostgres container is not started. Run tests under NUnit so [SetUpFixture] runs.");
        }

        var db = "p" + Guid.NewGuid().ToString("N");
        Gate.Wait();
        try
        {
            using var admin = new NpgsqlConnection(AdminCs);
            admin.Open();
            using var cmd = admin.CreateCommand();
            cmd.CommandText = "CREATE DATABASE " + db + " TEMPLATE pay_template";
            cmd.ExecuteNonQuery();
        }
        finally
        {
            Gate.Release();
        }

        return new NpgsqlConnectionStringBuilder(AdminCs) { Database = db }.ConnectionString;
    }

    public static void DropDatabase(string connectionString)
    {
        if (AdminCs is null)
        {
            return;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var db = builder.Database;
            if (string.IsNullOrWhiteSpace(db) || db is "postgres" or "pay_template")
            {
                return;
            }

            NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
            using var admin = new NpgsqlConnection(AdminCs);
            admin.Open();
            using var cmd = admin.CreateCommand();
            cmd.CommandText = "DROP DATABASE IF EXISTS " + db + " WITH (FORCE)";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Container teardown drops leftover databases.
        }
    }
}
