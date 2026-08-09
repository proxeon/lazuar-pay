using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Gateways;

public partial class LhdnGatewayAdapter
{
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
}
