using Npgsql;
using Testcontainers.PostgreSql;

namespace Lazuar.Pay.Tests;

/// <summary>Shared Postgres 16 for TX / unique-index proofs. InMemory is not this.</summary>
internal static class PayPostgres
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static PostgreSqlContainer? Container;
    static string? SkipReason;

    public static async Task<PayApiFactory> FactoryAsync()
    {
        var cs = await ConnectionStringAsync();
        if (cs is null)
        {
            Assert.Ignore(SkipReason ?? "Docker/Testcontainers Postgres unavailable");
        }

        var db = "p" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(cs))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = "CREATE DATABASE " + db;
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(cs) { Database = db };
        return new PayApiFactory { PostgresConnection = builder.ConnectionString };
    }

    static async Task<string?> ConnectionStringAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (Container is not null)
            {
                return Container.GetConnectionString();
            }

            if (SkipReason is not null)
            {
                return null;
            }

            try
            {
                var c = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("lazuar_pay_tx")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();
                await c.StartAsync();
                Container = c;
                return c.GetConnectionString();
            }
            catch (Exception ex)
            {
                SkipReason = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
