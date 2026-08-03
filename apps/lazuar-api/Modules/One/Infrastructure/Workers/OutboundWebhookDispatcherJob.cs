// apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.Workers;

public class OutboundWebhookDispatcherJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboundWebhookDispatcherJob> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OutboundWebhookDispatcherJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboundWebhookDispatcherJob> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbound Webhook Dispatcher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWebhooksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbound webhooks.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessWebhooksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Claim pending rows with SKIP LOCKED so multi-instance workers do not double-deliver.
        var sql = """
            SELECT * FROM "one"."WebhookDeliveryOutboxes"
            WHERE "Status" = 'PENDING'
              AND "NextAttemptAt" <= NOW()
            ORDER BY "NextAttemptAt"
            LIMIT 50
            FOR UPDATE SKIP LOCKED;
            """;

        var pendingDeliveries = await db.WebhookDeliveryOutboxes
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        if (pendingDeliveries.Count == 0)
        {
            await transaction.RollbackAsync(ct);
            return;
        }

        // Lease: bump NextAttemptAt so a crash mid-HTTP does not re-claim immediately.
        var leaseUntil = DateTime.UtcNow.AddMinutes(2);
        foreach (var delivery in pendingDeliveries)
        {
            delivery.ClaimLease(leaseUntil);
        }

        await db.SaveChangesAsync(ct);

        var client = _httpClientFactory.CreateClient("DeveloperWebhooks");

        foreach (var delivery in pendingDeliveries)
        {
            var endpoint = await db.TenantWebhookEndpoints
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == delivery.EndpointId, ct);

            if (endpoint == null || !endpoint.IsActive)
            {
                delivery.RecordFailure("Endpoint not found or inactive.");
                _logger.LogWarning(
                    "Webhook delivery {DeliveryId} failed: endpoint {EndpointId} not found or inactive.",
                    delivery.Id,
                    delivery.EndpointId);
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
                var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var signature = OutboundWebhookSignature.ComputeHeaderValue(
                    endpoint.SecretKey,
                    delivery.Payload,
                    unixTs);

                request.Headers.TryAddWithoutValidation("X-Lazuar-Signature", signature);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Event", delivery.EventType);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Delivery-Id", delivery.Id.ToString());
                request.Headers.TryAddWithoutValidation("X-Lazuar-Webhook-Id", endpoint.Id.ToString());
                request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    delivery.RecordSuccess();
                }
                else
                {
                    var error = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    delivery.RecordFailure(error);
                    _logger.LogWarning(
                        "Webhook delivery {DeliveryId} to {Url} failed: {Error} (attempt {Attempt}).",
                        delivery.Id,
                        endpoint.Url,
                        error,
                        delivery.AttemptCount);
                }
            }
            catch (Exception ex)
            {
                delivery.RecordFailure(ex.Message);
                _logger.LogError(
                    ex,
                    "Webhook delivery {DeliveryId} to endpoint {EndpointId} threw (attempt {Attempt}).",
                    delivery.Id,
                    delivery.EndpointId,
                    delivery.AttemptCount);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
