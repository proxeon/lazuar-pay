using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application.Observability;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// Dispatches JSON payloads to external developer endpoints (LHDN fire-and-forget path).
/// Secures the transmission using HMAC-SHA256 of the body only.
/// Frozen special-case until LHDN routes through One durable dispatcher (decision 00.2 A).
/// Do not expand into a second outbox/retry stack without reopening 00.2.
/// </summary>
public class WebhookSenderService : IWebhookSenderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookSenderService> _logger;

    public WebhookSenderService(IHttpClientFactory httpClientFactory, ILogger<WebhookSenderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendWebhookAsync(WebhookSubscription subscription, string payloadJson, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url);

            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var secretBytes = Encoding.UTF8.GetBytes(subscription.Secret);

            var signature = Convert.ToHexString(HMACSHA256.HashData(secretBytes, payloadBytes)).ToLowerInvariant();

            request.Headers.Add("X-Lazuar-Signature", signature);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                LazuarMetrics.RecordWebhookFailed("lhdn");
                _logger.LogWarning(
                    "LHDN fire-and-forget webhook delivery failed OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} Url={Url} StatusCode={StatusCode}",
                    subscription.OrganizationId,
                    subscription.Id,
                    subscription.Url,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            LazuarMetrics.RecordWebhookFailed("lhdn");
            _logger.LogError(
                ex,
                "LHDN fire-and-forget webhook delivery threw OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} Url={Url}",
                subscription.OrganizationId,
                subscription.Id,
                subscription.Url);
        }
    }
}
