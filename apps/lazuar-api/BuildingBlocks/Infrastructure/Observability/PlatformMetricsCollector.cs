using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Thin platform metrics aggregator: scrapes registered outbox/inbox schemas and merges
/// <see cref="IPlatformMetricsContributor"/> product bags (no module business-table SQL here).
/// </summary>
/// <remarks>
/// Approved exception: may query <c>{schema}.OutboxMessages</c> / <c>InboxMessages</c> for schemas
/// registered via <see cref="IOutboxSchemaRegistration"/>. Must not query module business tables
/// (e.g. TaxDocuments). See docs/009-building-blocks-ownership.md.
/// </remarks>
public sealed class PlatformMetricsCollector : IPlatformMetricsCollector
{
    /// <summary>Bag key set by Lhdn contributor; mapped to legacy snapshot / HTTP field.</summary>
    public const string LhdnStuckCountKey = "lhdn.stuck_count";

    private readonly string _connectionString;
    private readonly ObservabilityOptions _options;
    private readonly IReadOnlyList<IOutboxSchemaRegistration> _schemas;
    private readonly IReadOnlyList<IPlatformMetricsContributor> _contributors;
    private readonly ILogger<PlatformMetricsCollector> _logger;

    public PlatformMetricsCollector(
        string connectionString,
        IOptions<ObservabilityOptions> options,
        IEnumerable<IOutboxSchemaRegistration> schemas,
        IEnumerable<IPlatformMetricsContributor> contributors,
        ILogger<PlatformMetricsCollector> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _schemas = (schemas ?? throw new ArgumentNullException(nameof(schemas)))
            .OrderBy(s => s.Schema, StringComparer.Ordinal)
            .ToList();
        _contributors = (contributors ?? throw new ArgumentNullException(nameof(contributors))).ToList();
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
        var schemaMetrics = new List<SchemaOutboxMetrics>(_schemas.Count);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            double maxLag = 0;
            long pendingTotal = 0;
            long deadTotal = 0;

            foreach (var registration in _schemas)
            {
                var schema = registration.Schema;
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

            var context = new PlatformMetricsCollectContext
            {
                Connection = conn,
                CollectedAtUtc = collectedAt
            };

            foreach (var contributor in _contributors)
            {
                try
                {
                    await contributor.ContributeAsync(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Fail-soft: one product contributor must not blank lag/readiness gauges.
                    _logger.LogWarning(
                        ex,
                        "Platform metrics contributor {ContributorName} failed; continuing with partial bag.",
                        contributor.Name);
                }
            }

            var lhdnStuck = context.Bag.TryGetLong(LhdnStuckCountKey, out var stuck) ? stuck : 0L;

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
        // Schema identifiers come only from DI-validated IOutboxSchemaRegistration allow-list.
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
}
