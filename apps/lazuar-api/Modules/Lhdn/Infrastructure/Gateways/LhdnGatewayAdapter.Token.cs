using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Modules.Lhdn.Infrastructure.Gateways;

public partial class LhdnGatewayAdapter
{
    public async Task<string> GetTokenAsync(Guid organizationId, string clientId, string clientSecret, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        var cacheKey = $"lhdn_token_{organizationId}";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        await EnforceRateLimitAsync(_loginLimiters, clientId, 12, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/connect/token");

        var formData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "client_credentials" },
            { "scope", "InvoicingAPI" }
        };

        request.Content = new FormUrlEncodedContent(formData);
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("LHDN Token acquisition failed for Tenant {OrganizationId}: {Error}", organizationId, responseBody);
            throw new InvalidOperationException($"Failed to obtain LHDN access token: {responseBody}");
        }

        var json = JsonDocument.Parse(responseBody);
        var token = json.RootElement.GetProperty("access_token").GetString()!;

        _cache.Set(cacheKey, token, TimeSpan.FromMinutes(55));

        return token;
    }
}
