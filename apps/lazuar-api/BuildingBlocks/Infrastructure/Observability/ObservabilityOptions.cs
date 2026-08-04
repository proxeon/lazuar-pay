using System;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Observability knobs for health readiness and metrics collection.
/// Bind from configuration section <see cref="SectionName"/> ("Observability").
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// TaxDocuments in PENDING/SUBMITTED older than this are counted as stuck.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan LhdnStuckThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// When set, <c>/health/ready</c> fails if max outbox lag exceeds this value.
    /// Null (default) disables the lag gate (DB connectivity only).
    /// </summary>
    public TimeSpan? OutboxLagReadyThreshold { get; set; }

    /// <summary>
    /// How often <see cref="PlatformMetricsRefreshJob"/> refreshes gauge snapshots.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan MetricsRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}
