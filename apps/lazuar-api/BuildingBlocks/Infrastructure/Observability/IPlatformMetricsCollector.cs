using System.Threading;
using System.Threading.Tasks;

namespace BuildingBlocks.Infrastructure.Observability;

public interface IPlatformMetricsCollector
{
    /// <summary>
    /// Opens the Default connection, scrapes registered outbox schemas, and runs
    /// <see cref="BuildingBlocks.Application.Observability.IPlatformMetricsContributor"/> plugins.
    /// Updates <see cref="LazuarMetricsGauges"/> observables. Safe to call from health/metrics endpoints.
    /// </summary>
    Task<PlatformMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>Lightweight DB ping for readiness (SELECT 1).</summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
