using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Periodically refreshes outbox/LHDN gauge snapshots for <see cref="LazuarMetricsGauges"/> observables.
/// </summary>
public sealed class PlatformMetricsRefreshJob : BackgroundService
{
    private readonly IPlatformMetricsCollector _collector;
    private readonly ObservabilityOptions _options;
    private readonly ILogger<PlatformMetricsRefreshJob> _logger;

    public PlatformMetricsRefreshJob(
        IPlatformMetricsCollector collector,
        IOptions<ObservabilityOptions> options,
        ILogger<PlatformMetricsRefreshJob> logger)
    {
        _collector = collector;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first tick so migrations/boot can settle.
        var interval = _options.MetricsRefreshInterval;
        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromSeconds(30);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("Platform metrics refresh job started (interval {Interval}).", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _collector.CollectAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Platform metrics refresh failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
