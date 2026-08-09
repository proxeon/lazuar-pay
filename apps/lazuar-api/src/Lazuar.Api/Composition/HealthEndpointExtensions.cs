using BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace Lazuar.Api.Composition;

/// <summary>
/// Host health surface: liveness, readiness (DB + optional outbox lag), metrics snapshot.
/// </summary>
public static class HealthEndpointExtensions
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        // Liveness for deploy health-gates / Caddy (no auth, no CORS requirement)
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Readiness: DB connectivity; optional outbox lag threshold (Observability:OutboxLagReadyThreshold)
        app.MapGet("/health/ready", async (
            IPlatformMetricsCollector collector,
            IOptions<ObservabilityOptions> observabilityOptions,
            CancellationToken ct) =>
        {
            var result = await HealthReadiness.EvaluateAsync(collector, observabilityOptions, ct);
            var body = new
            {
                status = result.Status,
                database = result.DatabaseReachable ? "up" : "down",
                outbox_lag_seconds = result.OutboxLagSeconds,
                reason = result.Reason
            };
            return result.IsReady
                ? Results.Ok(body)
                : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        // Lightweight metrics snapshot (process counters + on-demand DB gauges)
        app.MapGet("/health/metrics", async (IPlatformMetricsCollector collector, CancellationToken ct) =>
        {
            var snapshot = await collector.CollectAsync(ct);
            return Results.Ok(new
            {
                collected_at_utc = snapshot.CollectedAtUtc,
                database_reachable = snapshot.DatabaseReachable,
                error = snapshot.Error,
                outbox_lag_seconds = snapshot.OutboxLagSeconds,
                outbox_pending_count = snapshot.OutboxPendingCount,
                dead_letter_count = snapshot.DeadLetterCount,
                lhdn_stuck_count = snapshot.LhdnStuckCount,
                counters = new
                {
                    dead_letters_since_start = snapshot.DeadLettersSinceStart,
                    webhook_failed_since_start = snapshot.WebhookFailedSinceStart,
                    dunning_cancels_since_start = snapshot.DunningCancelsSinceStart
                },
                schemas = snapshot.Schemas
            });
        });

        return app;
    }
}
