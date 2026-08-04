using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Shared readiness evaluation used by <c>/health/ready</c> and unit tests.
/// </summary>
public static class HealthReadiness
{
    public sealed record Result(
        bool IsReady,
        string Status,
        bool DatabaseReachable,
        double? OutboxLagSeconds,
        string? Reason);

    public static async Task<Result> EvaluateAsync(
        IPlatformMetricsCollector collector,
        ObservabilityOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collector);
        ArgumentNullException.ThrowIfNull(options);

        var dbOk = await collector.CanConnectAsync(cancellationToken);
        if (!dbOk)
        {
            return new Result(
                IsReady: false,
                Status: "unhealthy",
                DatabaseReachable: false,
                OutboxLagSeconds: null,
                Reason: "Database connection failed.");
        }

        if (options.OutboxLagReadyThreshold is not { } lagThreshold || lagThreshold <= TimeSpan.Zero)
        {
            return new Result(
                IsReady: true,
                Status: "ready",
                DatabaseReachable: true,
                OutboxLagSeconds: null,
                Reason: null);
        }

        var snapshot = await collector.CollectAsync(cancellationToken);
        if (!snapshot.DatabaseReachable)
        {
            return new Result(
                IsReady: false,
                Status: "unhealthy",
                DatabaseReachable: false,
                OutboxLagSeconds: null,
                Reason: snapshot.Error ?? "Metrics collection failed.");
        }

        var lag = snapshot.OutboxLagSeconds;
        var thresholdSeconds = lagThreshold.TotalSeconds;
        if (lag > thresholdSeconds)
        {
            return new Result(
                IsReady: false,
                Status: "unhealthy",
                DatabaseReachable: true,
                OutboxLagSeconds: lag,
                Reason: $"Outbox lag {lag:F0}s exceeds threshold {thresholdSeconds:F0}s.");
        }

        return new Result(
            IsReady: true,
            Status: "ready",
            DatabaseReachable: true,
            OutboxLagSeconds: lag,
            Reason: null);
    }

    public static Task<Result> EvaluateAsync(
        IPlatformMetricsCollector collector,
        IOptions<ObservabilityOptions> options,
        CancellationToken cancellationToken = default)
        => EvaluateAsync(collector, options.Value, cancellationToken);
}
