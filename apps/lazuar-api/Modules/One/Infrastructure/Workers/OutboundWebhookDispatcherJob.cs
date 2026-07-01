using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.One.Domain;
using Modules.Messaging.Contracts;

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
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("OneEventBus");

        var pendingDeliveries = await db.WebhookDeliveryOutboxes
            .IgnoreQueryFilters()
            .Where(w => w.Status == "PENDING" && w.NextAttemptAt <= DateTime.UtcNow)
            .OrderBy(w => w.NextAttemptAt)
            .Take(50)
            .ToListAsync(ct);

        if (!pendingDeliveries.Any()) return;

        var client = _httpClientFactory.CreateClient("DeveloperWebhooks");

        foreach (var delivery in pendingDeliveries)
        {
            var endpoint = await db.TenantWebhookEndpoints
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == delivery.EndpointId, ct);
                
            if (endpoint == null || !endpoint.IsActive)
            {
                delivery.RecordFailure("Endpoint not found or inactive.");
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
                var signature = ComputeHmacSha256(delivery.Payload, endpoint.SecretKey);
                
                request.Headers.Add("X-Lazuar-Signature", signature);
                request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    delivery.RecordSuccess();
                }
                else
                {
                    var error = $"HTTP {response.StatusCode}";
                    delivery.RecordFailure(error);
                }
            }
            catch (Exception ex)
            {
                delivery.RecordFailure(ex.Message);
            }

            if (delivery.Status == "FAILED" && delivery.AttemptCount == 5)
            {
                var org = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == delivery.OrganizationId, ct);
                var owner = await db.TenantMemberships
                    .IgnoreQueryFilters()
                    .Where(m => m.OrganizationId == delivery.OrganizationId && m.Role == "ADMIN")
                    .Join(db.GlobalUsers.IgnoreQueryFilters(), m => m.GlobalUserId, u => u.Id, (m, u) => u)
                    .FirstOrDefaultAsync(ct);

                if (owner != null && org != null)
                {
                    var subject = "URGENT: Webhook Delivery Permanently Failed";
                    var htmlBody = $@"Hi {owner.Name},<br/><br/>
A critical webhook delivery to your endpoint <strong>{endpoint.Url}</strong> has permanently failed after 5 retry attempts.<br/><br/>
<strong>Event Type:</strong> {delivery.EventType}<br/>
<strong>Last Error:</strong> {delivery.LastError}<br/><br/>
Please check your server logs. If this event was an <code>order.completed</code> or <code>subscription.activated</code> payload, your customer may not have received access to their purchase. Manual intervention may be required to fulfill this order.<br/><br/>
Once your server is back online, you can manually trigger a retry for this payload from the <strong>Developer > Delivery Logs</strong> section of your Lazuar Dashboard.";

                    await eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                        Guid.Empty,
                        owner.Email,
                        null,
                        subject,
                        htmlBody,
                        null,
                        "EMAIL"
                    ));
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
