using System.Threading;
using System.Threading.Tasks;

namespace BuildingBlocks.Infrastructure.Observability;

public interface IPlatformMetricsCollector
{
    /// <summary>
    /// Opens the Default connection and queries outbox lag, dead letters, and LHDN stuck docs.
    /// Updates <see cref="LazuarMetricsGauges"/> observables. Safe to call from health/metrics endpoints.
    /// </summary>
    Task<PlatformMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>Lightweight DB ping for readiness (SELECT 1).</summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
