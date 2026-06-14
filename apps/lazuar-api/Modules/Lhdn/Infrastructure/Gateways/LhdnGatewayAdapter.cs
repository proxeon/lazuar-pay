using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Gateways;

public class LhdnGatewayAdapter : ILhdnGatewayAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LhdnGatewayAdapter> _logger;

    public LhdnGatewayAdapter(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<LhdnGatewayAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetBaseUrl()
    {
        return _configuration["Lhdn:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
    }

    public async Task<string> GetTokenAsync(Guid organizationId, string clientId, string clientSecret, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        var cacheKey = $"lhdn_token_{organizationId}";

        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

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

        if (isIntermediary && !string.IsNullOrEmpty(tenantTin))
        {
            request.Headers.Add("onbehalfof", tenantTin);
        }

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

    public async Task<LhdnSubmissionResult> SubmitDocumentAsync(string token, string payloadJson, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/api/v1.0/documentsubmissions");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            return new LhdnSubmissionResult(false, null, null, responseBody);
        }

        var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;
        
        var submissionUid = root.TryGetProperty("submissionUid", out var uidProp) ? uidProp.GetString() : null;
        string? uuid = null;

        if (root.TryGetProperty("acceptedDocuments", out var acceptedDocs) && acceptedDocs.GetArrayLength() > 0)
        {
            uuid = acceptedDocs[0].TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
        }

        return new LhdnSubmissionResult(true, submissionUid, uuid, null);
    }

    public async Task<LhdnDocumentStatusResult> GetDocumentStatusAsync(string token, string submissionUid, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUrl()}/api/v1.0/documentsubmissions/{submissionUid}");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new LhdnDocumentStatusResult(false, null, null, null, responseBody);
        }

        var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;

        var status = root.TryGetProperty("overallStatus", out var statusProp) ? statusProp.GetString() : null;
        string? uuid = null;
        string? longId = null;

        if (root.TryGetProperty("documentSummary", out var docSummary) && docSummary.GetArrayLength() > 0)
        {
            var doc = docSummary[0];
            uuid = doc.TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
            longId = doc.TryGetProperty("longId", out var longIdProp) ? longIdProp.GetString() : null;
            
            if (string.IsNullOrEmpty(status))
            {
                status = doc.TryGetProperty("status", out var docStatusProp) ? docStatusProp.GetString() : null;
            }
        }

        return new LhdnDocumentStatusResult(true, status?.ToUpperInvariant(), uuid, longId, null);
    }
}
