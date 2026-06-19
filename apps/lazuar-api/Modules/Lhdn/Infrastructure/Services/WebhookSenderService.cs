using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// Dispatches JSON payloads to external developer endpoints.
/// Secures the transmission using HMAC-SHA256 so clients can verify authenticity.
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
                _logger.LogWarning("Webhook delivery failed for URL {Url} with status {StatusCode}", subscription.Url, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver webhook to {Url}", subscription.Url);
        }
    }
}
