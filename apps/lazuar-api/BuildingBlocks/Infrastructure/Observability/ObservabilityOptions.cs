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
    /// Legacy: TaxDocuments stuck threshold. Prefer <c>Lhdn:StuckThreshold</c>
    /// (<c>LhdnObservabilityOptions</c>). Kept for dual-bind during cutover.
    /// </summary>
    [Obsolete("Use Lhdn:StuckThreshold (LhdnObservabilityOptions). Dual-bound by AddLhdnModule.")]
    public TimeSpan LhdnStuckThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// When set, <c>/health/ready</c> fails if max outbox lag exceeds this value.
    /// Null (default) disables the lag gate (DB connectivity only).
    /// </summary>
    public TimeSpan? OutboxLagReadyThreshold { get; set; }

    /// <summary>
    /// When true (and a snapshot is collected), <c>/health/ready</c> fails if DeadLetterCount &gt; 0.
    /// </summary>
    public bool FailReadyOnDeadLetters { get; set; }

    /// <summary>
    /// How often <see cref="PlatformMetricsRefreshJob"/> refreshes gauge snapshots.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan MetricsRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
}
