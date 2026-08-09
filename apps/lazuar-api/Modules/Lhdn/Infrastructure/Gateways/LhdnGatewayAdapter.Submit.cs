using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Gateways;

public partial class LhdnGatewayAdapter
{
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
}
