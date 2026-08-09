using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Gateways;

public partial class LhdnGatewayAdapter
{
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
