using System;
using System.Collections.Generic;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>Point-in-time observability snapshot for gauges and <c>/health/metrics</c>.</summary>
public sealed class PlatformMetricsSnapshot
{
    public static PlatformMetricsSnapshot Empty { get; } = new()
    {
        CollectedAtUtc = DateTime.UnixEpoch,
        Schemas = Array.Empty<SchemaOutboxMetrics>()
    };

    public required DateTime CollectedAtUtc { get; init; }

    /// <summary>Max unprocessed outbox age in whole seconds (0 if none pending).</summary>
    public double OutboxLagSeconds { get; init; }

    /// <summary>Unprocessed outbox rows (Status != Dead, ProcessedAt null) across schemas.</summary>
    public long OutboxPendingCount { get; init; }

    /// <summary>Dead outbox + inbox rows across schemas.</summary>
    public long DeadLetterCount { get; init; }

    /// <summary>TaxDocuments in PENDING/SUBMITTED older than configured threshold.</summary>
    public long LhdnStuckCount { get; init; }

    /// <summary>Per-schema outbox breakdown (for ops JSON).</summary>
    public required IReadOnlyList<SchemaOutboxMetrics> Schemas { get; init; }

    /// <summary>Process-lifetime counters (not reset on collect).</summary>
    public long DeadLettersSinceStart { get; init; }
    public long WebhookFailedSinceStart { get; init; }
    public long DunningCancelsSinceStart { get; init; }

    public bool DatabaseReachable { get; init; } = true;
    public string? Error { get; init; }
}

public sealed class SchemaOutboxMetrics
{
    public required string Schema { get; init; }
    public long OutboxPending { get; init; }
    public long OutboxDead { get; init; }
    public long InboxDead { get; init; }
    public double OutboxLagSeconds { get; init; }
}
