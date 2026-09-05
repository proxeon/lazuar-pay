using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lazuar.Pay.Money;

/// <summary>
/// plans/031/02 step 3: pending-refund visibility. The settle worker publishes a snapshot
/// after every batch; the gauges read the last published snapshot (no DB access from the
/// callback). Alert shape: refunds_pending growing, or refunds_pending_oldest_seconds
/// beyond the reconciliation policy — those are rows owed to buyers with no automated exit.
/// </summary>
public static class RefundMetrics
{
    private static readonly Meter Meter = new("Lazuar.Pay.Refunds");
    private static long _pendingStripe;
    private static long _pendingManual;
    private static long _oldestSeconds;

    static RefundMetrics()
    {
        Meter.CreateObservableGauge("refunds_pending", () =>
            new Measurement<long>[]
            {
                new(Interlocked.Read(ref _pendingStripe),
                    new KeyValuePair<string, object?>("provider", "stripe")),
                new(Interlocked.Read(ref _pendingManual),
                    new KeyValuePair<string, object?>("provider", "manual")),
            });
        Meter.CreateObservableGauge(
            "refunds_pending_oldest_seconds",
            () => new Measurement<long>(Interlocked.Read(ref _oldestSeconds)));
    }

    public static void Publish(int pendingStripe, int pendingManual, double oldestSeconds)
    {
        Interlocked.Exchange(ref _pendingStripe, pendingStripe);
        Interlocked.Exchange(ref _pendingManual, pendingManual);
        Interlocked.Exchange(ref _oldestSeconds, (long)oldestSeconds);
    }

    // Test observability: the last published snapshot.
    internal static long PendingStripeSnapshot => Interlocked.Read(ref _pendingStripe);
    internal static long PendingManualSnapshot => Interlocked.Read(ref _pendingManual);
    internal static long OldestSecondsSnapshot => Interlocked.Read(ref _oldestSeconds);
}
