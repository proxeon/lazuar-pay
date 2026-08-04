using System;
using System.Diagnostics.Metrics;
using System.Threading;
using BuildingBlocks.Application.Observability;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Registers observable gauges on the shared <see cref="LazuarMetrics.MeterName"/> meter
/// and holds the latest DB snapshot for gauge callbacks.
/// </summary>
public static class LazuarMetricsGauges
{
    private static PlatformMetricsSnapshot _latestSnapshot = PlatformMetricsSnapshot.Empty;
    private static int _registered;

    /// <summary>Ensure gauges are registered once (called from DI registration or first collect).</summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // Reuse the same Meter instance name; System.Diagnostics.Metrics merges instruments by name.
        var meter = new Meter(LazuarMetrics.MeterName, "1.0.0");

        meter.CreateObservableGauge(
            "lazuar.outbox.lag_seconds",
            () => _latestSnapshot.OutboxLagSeconds,
            unit: "s",
            description: "Max age in seconds of unprocessed outbox messages across module schemas");

        meter.CreateObservableGauge(
            "lazuar.outbox.dead_letters_count",
            () => _latestSnapshot.DeadLetterCount,
            description: "Current count of Dead outbox+inbox rows across module schemas");

        meter.CreateObservableGauge(
            "lazuar.lhdn.stuck_count",
            () => _latestSnapshot.LhdnStuckCount,
            description: "TaxDocuments stuck in PENDING/SUBMITTED longer than threshold");

        meter.CreateObservableGauge(
            "lazuar.outbox.pending_count",
            () => _latestSnapshot.OutboxPendingCount,
            description: "Unprocessed outbox rows (not Dead) across module schemas");
    }

    public static void PublishSnapshot(PlatformMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureRegistered();
        Volatile.Write(ref _latestSnapshot, snapshot);
    }

    public static PlatformMetricsSnapshot LatestSnapshot => Volatile.Read(ref _latestSnapshot);
}
