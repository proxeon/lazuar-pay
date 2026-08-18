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

        var collectForLag = options.OutboxLagReadyThreshold is { } lag && lag > TimeSpan.Zero;
        if (!collectForLag && !options.FailReadyOnDeadLetters)
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

        if (collectForLag)
        {
            var thresholdSeconds = options.OutboxLagReadyThreshold!.Value.TotalSeconds;
            if (snapshot.OutboxLagSeconds > thresholdSeconds)
            {
                return new Result(
                    IsReady: false,
                    Status: "unhealthy",
                    DatabaseReachable: true,
                    OutboxLagSeconds: snapshot.OutboxLagSeconds,
                    Reason: $"Outbox lag {snapshot.OutboxLagSeconds:F0}s exceeds threshold {thresholdSeconds:F0}s.");
            }
        }

        if (options.FailReadyOnDeadLetters && snapshot.DeadLetterCount > 0)
        {
            return new Result(
                IsReady: false,
                Status: "unhealthy",
                DatabaseReachable: true,
                OutboxLagSeconds: snapshot.OutboxLagSeconds,
                Reason: $"Dead letters present ({snapshot.DeadLetterCount}).");
        }

        return new Result(
            IsReady: true,
            Status: "ready",
            DatabaseReachable: true,
            OutboxLagSeconds: snapshot.OutboxLagSeconds,
            Reason: null);
    }

    public static Task<Result> EvaluateAsync(
        IPlatformMetricsCollector collector,
        IOptions<ObservabilityOptions> options,
        CancellationToken cancellationToken = default)
        => EvaluateAsync(collector, options.Value, cancellationToken);
}
