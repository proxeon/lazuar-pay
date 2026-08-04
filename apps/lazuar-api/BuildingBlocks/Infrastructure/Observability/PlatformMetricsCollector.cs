using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// On-demand SQL metrics across module private schemas (outbox lag, dead letters, LHDN stuck).
/// </summary>
public sealed class PlatformMetricsCollector : IPlatformMetricsCollector
{
    /// <summary>Schemas that own OutboxMessages / InboxMessages (see docs/007 runbook).</summary>
    public static readonly string[] ModuleSchemas =
    [
        "one", "messaging", "payments", "crm", "ops", "billing", "lhdn", "commerce", "communications"
    ];

    private readonly string _connectionString;
    private readonly ObservabilityOptions _options;
    private readonly ILogger<PlatformMetricsCollector> _logger;

    public PlatformMetricsCollector(
        string connectionString,
        IOptions<ObservabilityOptions> options,
        ILogger<PlatformMetricsCollector> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database readiness check failed.");
            return false;
        }
    }

    public async Task<PlatformMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var collectedAt = DateTime.UtcNow;
        var schemaMetrics = new List<SchemaOutboxMetrics>(ModuleSchemas.Length);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            double maxLag = 0;
            long pendingTotal = 0;
            long deadTotal = 0;

            foreach (var schema in ModuleSchemas)
            {
                var (pending, outboxDead, lag) = await QueryOutboxAsync(conn, schema, cancellationToken);
                var inboxDead = await QueryInboxDeadAsync(conn, schema, cancellationToken);

                pendingTotal += pending;
                deadTotal += outboxDead + inboxDead;
                if (lag > maxLag) maxLag = lag;

                schemaMetrics.Add(new SchemaOutboxMetrics
                {
                    Schema = schema,
                    OutboxPending = pending,
                    OutboxDead = outboxDead,
                    InboxDead = inboxDead,
                    OutboxLagSeconds = lag
                });
            }

            var stuckThreshold = _options.LhdnStuckThreshold;
            if (stuckThreshold <= TimeSpan.Zero)
            {
                stuckThreshold = TimeSpan.FromHours(1);
            }

            var lhdnStuck = await QueryLhdnStuckAsync(conn, stuckThreshold, cancellationToken);

            var snapshot = new PlatformMetricsSnapshot
            {
                CollectedAtUtc = collectedAt,
                OutboxLagSeconds = maxLag,
                OutboxPendingCount = pendingTotal,
                DeadLetterCount = deadTotal,
                LhdnStuckCount = lhdnStuck,
                Schemas = schemaMetrics,
                DeadLettersSinceStart = LazuarMetrics.DeadLettersTotal,
                WebhookFailedSinceStart = LazuarMetrics.WebhookFailedTotal,
                DunningCancelsSinceStart = LazuarMetrics.DunningCancelsTotal,
                DatabaseReachable = true
            };

            LazuarMetricsGauges.PublishSnapshot(snapshot);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect platform metrics.");
            var failed = new PlatformMetricsSnapshot
            {
                CollectedAtUtc = collectedAt,
                Schemas = schemaMetrics,
                DeadLettersSinceStart = LazuarMetrics.DeadLettersTotal,
                WebhookFailedSinceStart = LazuarMetrics.WebhookFailedTotal,
                DunningCancelsSinceStart = LazuarMetrics.DunningCancelsTotal,
                DatabaseReachable = false,
                Error = ex.Message
            };
            return failed;
        }
    }

    private static async Task<(long Pending, long Dead, double LagSeconds)> QueryOutboxAsync(
        NpgsqlConnection conn,
        string schema,
        CancellationToken ct)
    {
        // Quote schema identifier from our fixed allow-list only.
        var sql = $"""
            SELECT
                COUNT(*) FILTER (WHERE "ProcessedAt" IS NULL AND "Status" IS DISTINCT FROM 'Dead') AS pending,
                COUNT(*) FILTER (WHERE "Status" = 'Dead') AS dead,
                COALESCE(
                    MAX(EXTRACT(EPOCH FROM (NOW() AT TIME ZONE 'UTC' - "OccurredOn")))
                        FILTER (WHERE "ProcessedAt" IS NULL AND "Status" IS DISTINCT FROM 'Dead'),
                    0) AS lag_seconds
            FROM "{schema}"."OutboxMessages"
            """;

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (0, 0, 0);
            }

            var pending = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
            var dead = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
            var lag = reader.IsDBNull(2) ? 0d : reader.GetDouble(2);
            return (pending, dead, Math.Max(0, lag));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Schema/table not migrated yet — treat as empty.
            return (0, 0, 0);
        }
    }

    private static async Task<long> QueryInboxDeadAsync(
        NpgsqlConnection conn,
        string schema,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT COUNT(*)
            FROM "{schema}"."InboxMessages"
            WHERE "Status" = 'Dead'
            """;

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is long l ? l : Convert.ToInt64(result ?? 0L);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return 0;
        }
    }

    private static async Task<long> QueryLhdnStuckAsync(
        NpgsqlConnection conn,
        TimeSpan olderThan,
        CancellationToken ct)
    {
        // PENDING/SUBMITTED and UpdatedAt older than threshold (or CreatedAt if UpdatedAt null — column is required).
        const string sql = """
            SELECT COUNT(*)
            FROM lhdn."TaxDocuments"
            WHERE "ValidationStatus" IN ('PENDING', 'SUBMITTED')
              AND "UpdatedAt" < (NOW() AT TIME ZONE 'UTC') - @threshold
            """;

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("threshold", olderThan);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is long l ? l : Convert.ToInt64(result ?? 0L);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.InvalidSchemaName)
        {
            return 0;
        }
    }
}
