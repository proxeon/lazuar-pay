// apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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
