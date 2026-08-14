// apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.Workers;

public class OutboundWebhookDispatcherJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboundWebhookDispatcherJob> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BackgroundWorkerOptions _options;

    public OutboundWebhookDispatcherJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboundWebhookDispatcherJob> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
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

            await Task.Delay(_options.OutboundWebhookInterval, stoppingToken);
        }
    }

    /// <summary>One poll cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessWebhooksAsync(ct);

    private async Task ProcessWebhooksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneDbContext>();

        var leaseUntil = DateTime.UtcNow.Add(_options.ClaimLeaseDuration);
        var pendingDeliveries = await ClaimPendingDeliveriesAsync(db, leaseUntil, ct);
        if (pendingDeliveries.Count == 0) return;

        var client = _httpClientFactory.CreateClient("DeveloperWebhooks");
        var vault = scope.ServiceProvider.GetRequiredService<ISecretVault>();

        foreach (var delivery in pendingDeliveries)
        {
            var endpoint = await db.TenantWebhookEndpoints
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == delivery.EndpointId, ct);

            if (endpoint == null || !endpoint.IsActive)
            {
                delivery.RecordFailure("Endpoint not found or inactive.");
                LazuarMetrics.RecordWebhookFailed("outbound");
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
                var signingSecret = ResolveSigningSecret(vault, endpoint);
                var signature = OutboundWebhookSignature.ComputeHeaderValue(
                    signingSecret,
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
                    var status = (int)response.StatusCode;
                    var error = $"HTTP {status} {response.StatusCode}";
                    if (IsPermanentHttpFailure(status))
                    {
                        delivery.RecordPermanentFailure(error);
                    }
                    else
                    {
                        delivery.RecordFailure(error);
                    }

                    LazuarMetrics.RecordWebhookFailed("outbound");
                    _logger.LogWarning(
                        "Webhook delivery {DeliveryId} to {Url} failed: {Error} (attempt {Attempt}, status={Status}).",
                        delivery.Id,
                        endpoint.Url,
                        error,
                        delivery.AttemptCount,
                        delivery.Status);
                }
            }
            catch (Exception ex)
            {
                delivery.RecordFailure(ex.Message);
                LazuarMetrics.RecordWebhookFailed("outbound");
                _logger.LogError(
                    ex,
                    "Webhook delivery {DeliveryId} to endpoint {EndpointId} threw (attempt {Attempt}).",
                    delivery.Id,
                    delivery.EndpointId,
                    delivery.AttemptCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Claims PENDING deliveries with FOR UPDATE SKIP LOCKED and bumps NextAttemptAt lease before HTTP.
    /// </summary>
    internal static async Task<List<WebhookDeliveryOutbox>> ClaimPendingDeliveriesAsync(
        OneDbContext db,
        DateTime leaseUntilUtc,
        CancellationToken ct)
    {
        List<WebhookDeliveryOutbox> pendingDeliveries;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            const string sql = """
                SELECT * FROM "one"."WebhookDeliveryOutboxes"
                WHERE "Status" = 'PENDING'
                  AND "NextAttemptAt" <= NOW()
                ORDER BY "NextAttemptAt"
                LIMIT 50
                FOR UPDATE SKIP LOCKED;
                """;

            pendingDeliveries = await db.WebhookDeliveryOutboxes
                .FromSqlRaw(sql)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            if (pendingDeliveries.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return pendingDeliveries;
            }

            foreach (var delivery in pendingDeliveries)
            {
                delivery.ClaimLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            var now = DateTime.UtcNow;
            pendingDeliveries = await db.WebhookDeliveryOutboxes
                .IgnoreQueryFilters()
                .Where(d => d.Status == "PENDING" && d.NextAttemptAt <= now)
                .OrderBy(d => d.NextAttemptAt)
                .Take(50)
                .ToListAsync(ct);

            if (pendingDeliveries.Count == 0) return pendingDeliveries;

            foreach (var delivery in pendingDeliveries)
            {
                delivery.ClaimLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
        }

        return pendingDeliveries;
    }

    /// <summary>
    /// Decrypt stored HMAC material. Lazy-encrypt leftover plaintext <c>whsec_</c> rows.
    /// Full decrypted string is used (prefix is not stripped).
    /// </summary>
    internal static string ResolveSigningSecret(ISecretVault vault, TenantWebhookEndpoint endpoint)
    {
        if (endpoint.SecretKey.StartsWith("whsec_", StringComparison.Ordinal))
        {
            endpoint.RotateSecret(vault.Encrypt(endpoint.SecretKey));
        }

        return vault.DecryptOrPlaintext(endpoint.SecretKey);
    }

    /// <summary>4xx is a permanent client reject (401/422 policy). 5xx / transport still retry.</summary>
    internal static bool IsPermanentHttpFailure(int statusCode) =>
        statusCode is >= 400 and < 500;
}
