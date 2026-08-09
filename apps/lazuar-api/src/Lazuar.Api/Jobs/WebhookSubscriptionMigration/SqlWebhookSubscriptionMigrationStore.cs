using Dapper;
using Npgsql;

namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// Dapper/Npgsql store against <c>ConnectionStrings:Default</c> (shared Neon / Postgres).
/// </summary>
public sealed class SqlWebhookSubscriptionMigrationStore : IWebhookSubscriptionMigrationStore
{
    private const string LegacyBatchSql = """
        SELECT
            "Id",
            "OrganizationId",
            "Url",
            "Secret",
            "IsActive",
            "CreatedAt"
        FROM lhdn."WebhookSubscriptions"
        WHERE "IsActive" = true
          AND (@AfterId IS NULL OR "Id" > @AfterId)
        ORDER BY "Id"
        LIMIT @BatchSize
        """;

    private const string FindByOrgAndUrlSql = """
        SELECT "Id", "OrganizationId", "Url", "IsActive"
        FROM one."TenantWebhookEndpoints"
        WHERE "OrganizationId" = @OrganizationId
          AND "Url" = @Url
        LIMIT 1
        """;

    private const string OrgExistsSql = """
        SELECT 1
        FROM one."Organizations"
        WHERE "Id" = @Id
        LIMIT 1
        """;

    // No unique index on (OrganizationId, Url); gate concurrent races with NOT EXISTS.
    private const string InsertSql = """
        INSERT INTO one."TenantWebhookEndpoints" (
            "Id",
            "OrganizationId",
            "Url",
            "SecretKey",
            "IsActive",
            "EnabledEvents",
            "CreatedAt",
            "UpdatedAt"
        )
        SELECT
            @Id,
            @OrganizationId,
            @Url,
            @SecretKey,
            @IsActive,
            CAST(@EnabledEventsJson AS jsonb),
            @CreatedAt,
            @UpdatedAt
        WHERE NOT EXISTS (
            SELECT 1
            FROM one."TenantWebhookEndpoints" e
            WHERE e."OrganizationId" = @OrganizationId
              AND e."Url" = @Url
        )
        """;

    private readonly string _connectionString;

    public SqlWebhookSubscriptionMigrationStore(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<IReadOnlyList<LegacyWebhookSubscriptionRow>> GetActiveLegacyBatchAsync(
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        var rows = await conn.QueryAsync<LegacyWebhookSubscriptionRow>(
            new CommandDefinition(
                LegacyBatchSql,
                new { AfterId = afterId, BatchSize = batchSize },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<OneWebhookEndpointProbe?> FindByOrgAndUrlAsync(
        Guid organizationId,
        string url,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OneWebhookEndpointProbe>(
            new CommandDefinition(
                FindByOrgAndUrlSql,
                new { OrganizationId = organizationId, Url = url },
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
        MigratedTenantWebhookEndpointInsert row,
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
                    row.Url,
                    row.SecretKey,
                    row.IsActive,
                    row.EnabledEventsJson,
                    row.CreatedAt,
                    row.UpdatedAt
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
