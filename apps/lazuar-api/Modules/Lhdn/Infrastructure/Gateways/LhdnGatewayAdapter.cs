using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
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

    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _loginLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _submitLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _pollLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _tinLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _cancelLimiters = new();

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

    private async Task EnforceRateLimitAsync(ConcurrentDictionary<string, TokenBucketRateLimiter> registry, string clientId, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return;

        var limiter = registry.GetOrAdd(clientId, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = limit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = limit,
            AutoReplenishment = true
        }));

        await limiter.AcquireAsync(1, ct);
    }

    private void TryAddIntermediaryHeader(HttpRequestMessage request, bool isIntermediary, string? tenantTin)
    {
        if (isIntermediary && !string.IsNullOrWhiteSpace(tenantTin))
        {
            request.Headers.Add("onbehalfof", tenantTin.Trim());
        }
    }

    private static int? ExtractRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
        }

        if (response.Headers.TryGetValues("x-rate-limit-reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out var resetEpoch))
        {
            var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch).UtcDateTime;
            var delay = (int)(resetTime - DateTime.UtcNow).TotalSeconds;
            return delay > 0 ? delay : 60;
        }

        return 60;
    }

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

    public async Task<LhdnSubmissionResult> SubmitDocumentAsync(string clientId, string token, string payloadJson, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        await EnforceRateLimitAsync(_submitLimiters, clientId, 100, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/api/v1.0/documentsubmissions");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var response = await client.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return new LhdnSubmissionResult(false, null, null, "Rate limit exceeded by LHDN.", ExtractRetryAfterSeconds(response));
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            try
            {
                var errorJson = JsonDocument.Parse(responseBody);
                if (errorJson.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("details", out var details) && details.GetArrayLength() > 0)
                {
                    var firstDetail = details[0];
                    var msg = firstDetail.TryGetProperty("message", out var mProp) ? mProp.GetString() : 
                              firstDetail.TryGetProperty("error", out var eProp) ? eProp.GetString() : responseBody;
                              
                    return new LhdnSubmissionResult(false, null, null, $"LHDN Rejected: {msg}");
                }
            }
            catch { }

            return new LhdnSubmissionResult(false, null, null, responseBody);
        }

        var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;
        
        var submissionUid = root.TryGetProperty("submissionUid", out var uidProp) ? uidProp.GetString() : null;
        if (string.IsNullOrEmpty(submissionUid))
            submissionUid = root.TryGetProperty("submissionUID", out var uidPropCap) ? uidPropCap.GetString() : null;

        if (root.TryGetProperty("rejectedDocuments", out var rejectedDocs) && rejectedDocs.GetArrayLength() > 0)
        {
            var firstReject = rejectedDocs[0];
            if (firstReject.TryGetProperty("error", out var errObj))
            {
                string rejectMessage = "Validation Error";
                if (errObj.TryGetProperty("details", out var errDetails) && errDetails.ValueKind == JsonValueKind.Array && errDetails.GetArrayLength() > 0)
                {
                    var detailObj = errDetails[0];
                    if (detailObj.TryGetProperty("message", out var mProp))
                        rejectMessage = mProp.GetString()!;
                    else if (detailObj.TryGetProperty("error", out var eProp))
                        rejectMessage = eProp.GetString()!;
                    else
                        rejectMessage = detailObj.ToString(); // Dump full object if format is unknown
                }
                else if (errObj.TryGetProperty("message", out var msgProp2))
                {
                    rejectMessage = msgProp2.GetString()!;
                }
                else
                {
                    rejectMessage = errObj.ToString(); // Dump full error object
                }
                
                return new LhdnSubmissionResult(false, submissionUid, null, $"Rejected by LHDN: {rejectMessage}");
            }
        }

        string? uuid = null;
        if (root.TryGetProperty("acceptedDocuments", out var acceptedDocs) && acceptedDocs.GetArrayLength() > 0)
        {
            uuid = acceptedDocs[0].TryGetProperty("uuid", out var uuidProp) ? uuidProp.GetString() : null;
        }

        return new LhdnSubmissionResult(true, submissionUid, uuid, null);
    }

    public async Task<LhdnDocumentStatusResult> GetDocumentStatusAsync(string clientId, string token, string submissionUid, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        await EnforceRateLimitAsync(_pollLimiters, clientId, 300, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUrl()}/api/v1.0/documentsubmissions/{submissionUid}");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var response = await client.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return new LhdnDocumentStatusResult(false, null, null, null, "Rate limit exceeded by LHDN.", ExtractRetryAfterSeconds(response));
        }

        // Extremely common LHDN Sandbox behavior: They queue submissions asynchronously. 
        // A 404 here just means their internal processor hasn't reached it yet. Do not crash.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new LhdnDocumentStatusResult(false, "PENDING", null, null, null, 5); // Force retry in 5 seconds
        }

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

        string? errorMessage = null;

        // Fetch detailed error messages if the document was marked as invalid
        if (status?.Equals("Invalid", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrEmpty(uuid))
        {
            var detailsReq = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUrl()}/api/v1.0/documents/{uuid}/details");
            detailsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            TryAddIntermediaryHeader(detailsReq, isIntermediary, tenantTin);

            try 
            {
                var detailsRes = await client.SendAsync(detailsReq, ct);
                if (detailsRes.IsSuccessStatusCode)
                {
                    var detailsBody = await detailsRes.Content.ReadAsStringAsync(ct);
                    var detailsJson = JsonDocument.Parse(detailsBody);
                    
                    if (detailsJson.RootElement.TryGetProperty("validationResults", out var valRes) &&
                        valRes.TryGetProperty("validationSteps", out var valSteps))
                    {
                        var errors = new List<string>();
                        foreach (var step in valSteps.EnumerateArray())
                        {
                            if (step.TryGetProperty("status", out var stepStatus) && stepStatus.GetString() == "Invalid" && step.TryGetProperty("error", out var errObj))
                            {
                                if (errObj.TryGetProperty("innerError", out var innerArr) && innerArr.ValueKind == JsonValueKind.Array && innerArr.GetArrayLength() > 0)
                                {
                                    var innerObj = innerArr[0];
                                    if (innerObj.TryGetProperty("error", out var innerErrMsg))
                                    {
                                        errors.Add(innerErrMsg.GetString()!);
                                    }
                                }
                                else if (errObj.TryGetProperty("error", out var errMsg))
                                {
                                    errors.Add(errMsg.GetString()!);
                                }
                            }
                        }
                        if (errors.Count > 0)
                        {
                            errorMessage = string.Join(" | ", errors);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch detailed validation errors for UUID {Uuid}", uuid);
                errorMessage = "Validation failed at LHDN. Detailed error fetch failed.";
            }
        }

        return new LhdnDocumentStatusResult(true, status?.ToUpperInvariant(), uuid, longId, errorMessage);
    }

    public async Task<LhdnTinValidationResult> ValidateTaxpayerTinAsync(string clientId, string token, string tin, string idType, string idValue, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        await EnforceRateLimitAsync(_tinLimiters, clientId, 60, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUrl()}/api/v1.0/taxpayer/validate/{Uri.EscapeDataString(tin)}?idType={Uri.EscapeDataString(idType)}&idValue={Uri.EscapeDataString(idValue)}");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var json = JsonDocument.Parse(responseBody);
                var taxpayerName = json.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                return new LhdnTinValidationResult(true, true, taxpayerName, null);
            }
            catch
            {
                return new LhdnTinValidationResult(true, true, null, null);
            }
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new LhdnTinValidationResult(true, false, null, null);
        }

        _logger.LogError("LHDN TIN Validation failed: {Status} {Body}", response.StatusCode, responseBody);
        return new LhdnTinValidationResult(false, false, null, responseBody);
    }

    public async Task<LhdnCancelResult> CancelDocumentAsync(string clientId, string token, string uuid, string reason, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        await EnforceRateLimitAsync(_cancelLimiters, clientId, 12, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Put, $"{GetBaseUrl()}/api/v1.0/documents/state/{uuid}/state");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var payload = new { status = "cancelled", reason = reason };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return new LhdnCancelResult(true, "CANCELLED", null);
        }

        _logger.LogError("LHDN Cancel Document failed: {Status} {Body}", response.StatusCode, responseBody);
        return new LhdnCancelResult(false, null, responseBody);
    }
}
