using System;
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
    public async Task<LhdnTinValidationResult> ValidateTaxpayerTinAsync(string clientId, string token, string tin, string idType, string idValue, bool isIntermediary, string? tenantTin, CancellationToken ct = default)
    {
        await EnforceRateLimitAsync(_tinLimiters, clientId, 60, ct);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUrl(clientId)}/api/v1.0/taxpayer/validate/{Uri.EscapeDataString(tin)}?idType={Uri.EscapeDataString(idType)}&idValue={Uri.EscapeDataString(idValue)}");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        TryAddIntermediaryHeader(request, isIntermediary, tenantTin);

        var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return InterpretSuccessTinBody(responseBody);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new LhdnTinValidationResult(true, false, null, null);
        }

        _logger.LogError("LHDN TIN Validation failed: {Status} {Body}", response.StatusCode, responseBody);
        return new LhdnTinValidationResult(false, false, null, responseBody);
    }

    internal static LhdnTinValidationResult InterpretSuccessTinBody(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return new LhdnTinValidationResult(true, false, null, "Empty TIN validation body.");
        }

        try
        {
            var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new LhdnTinValidationResult(true, false, null, "TIN validation body was not a JSON object.");
            }

            var taxpayerName = json.RootElement.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null;
            return new LhdnTinValidationResult(true, true, taxpayerName, null);
        }
        catch (JsonException)
        {
            return new LhdnTinValidationResult(true, false, null, "Unparseable TIN validation body.");
        }
    }
}
