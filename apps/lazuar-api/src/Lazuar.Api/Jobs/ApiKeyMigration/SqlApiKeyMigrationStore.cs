using System.Data;
using Dapper;
using Npgsql;

namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// Dapper/Npgsql store against <c>ConnectionStrings:Default</c> (shared Neon / Postgres).
/// </summary>
public sealed class SqlApiKeyMigrationStore : IApiKeyMigrationStore
{
    private const string LegacyBatchSql = """
        SELECT
            "Id",
            "OrganizationId",
            "Name",
            "Prefix",
            "KeyHash",
            "KeyHint",
            "Scopes",
            "IsActive",
            "CreatedAt"
        FROM lhdn."DeveloperApiKeys"
        WHERE (@AfterId IS NULL OR "Id" > @AfterId)
        ORDER BY "Id"
        LIMIT @BatchSize
        """;

    private const string FindByKeyHashSql = """
        SELECT "Id", "KeyHash", "OrganizationId"
        FROM one."ApiCredentials"
        WHERE "KeyHash" = @KeyHash
        LIMIT 1
        """;

    private const string FindByIdSql = """
        SELECT "Id", "KeyHash", "OrganizationId"
        FROM one."ApiCredentials"
        WHERE "Id" = @Id
        LIMIT 1
        """;

    private const string OrgExistsSql = """
        SELECT 1
        FROM one."Organizations"
        WHERE "Id" = @Id
        LIMIT 1
        """;

    private const string InsertSql = """
        INSERT INTO one."ApiCredentials" (
            "Id",
            "OrganizationId",
            "Name",
            "Prefix",
            "KeyHash",
            "KeyHint",
            "Scopes",
            "IsActive",
            "CreatedAt",
            "CreatedByUserId"
        ) VALUES (
            @Id,
            @OrganizationId,
            @Name,
            @Prefix,
            @KeyHash,
            @KeyHint,
            @Scopes,
            @IsActive,
            @CreatedAt,
            NULL
        )
        ON CONFLICT ("KeyHash") DO NOTHING
        """;

    private readonly string _connectionString;

    public SqlApiKeyMigrationStore(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<IReadOnlyList<LegacyDeveloperApiKeyRow>> GetLegacyBatchAsync(
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        var rows = await conn.QueryAsync<LegacyDeveloperApiKeyRow>(
            new CommandDefinition(
                LegacyBatchSql,
                new { AfterId = afterId, BatchSize = batchSize },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<OneCredentialProbe?> FindByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OneCredentialProbe>(
            new CommandDefinition(
                FindByKeyHashSql,
                new { KeyHash = keyHash },
                cancellationToken: cancellationToken));
    }

    public async Task<OneCredentialProbe?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OneCredentialProbe>(
            new CommandDefinition(
                FindByIdSql,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> OrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        var result = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                OrgExistsSql,
                new { Id = organizationId },
                cancellationToken: cancellationToken));
        return result.HasValue;
    }

    public async Task<bool> TryInsertAsync(
        MigratedApiCredentialInsert row,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(
            new CommandDefinition(
                InsertSql,
                new
                {
                    row.Id,
                    row.OrganizationId,
                    row.Name,
                    row.Prefix,
                    row.KeyHash,
                    row.KeyHint,
                    row.Scopes,
                    row.IsActive,
                    row.CreatedAt
                },
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
