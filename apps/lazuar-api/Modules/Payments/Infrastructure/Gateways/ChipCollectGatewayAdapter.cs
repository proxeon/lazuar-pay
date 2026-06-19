using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;

namespace Modules.Payments.Infrastructure.Gateways;

public class ChipCollectGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChipCollectGatewayAdapter> _logger;

    private const string ApiBaseUrl = "https://gate.chip-in.asia/api/v1/";

    public ChipCollectGatewayAdapter(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<ChipCollectGatewayAdapter> logger)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string GatewayType => "CHIP";

    public async Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail, string successUrl, string cancelUrl,
        Dictionary<string, string> metadata, string? merchantId, bool setupFutureUsage = false)
    {
        if (string.IsNullOrEmpty(merchantId))
        {
            return new GatewayCheckoutResult(false, null, null, "MerchantId (Brand ID) is required for CHIP Collect.");
        }

        var amountInCents = (int)Math.Round(amount * 100, 0);

        metadata["tenant_id"] = tenantId.ToString();
        var clientName = ExtractName(customerEmail);

        var payload = new Dictionary<string, object>
        {
            ["brand_id"] = merchantId,
            ["client"] = new
            {
                email = string.IsNullOrWhiteSpace(customerEmail) ? "customer@example.com" : customerEmail,
                full_name = clientName
            },
            ["purchase"] = new
            {
                products = new[]
                {
                    new
                    {
                        name = string.IsNullOrWhiteSpace(productName) ? "Lazuar Payment" : productName,
                        price = amountInCents
                    }
                },
                metadata = metadata
            },
            ["success_redirect"] = successUrl,
            ["failure_redirect"] = cancelUrl,
            ["cancel_redirect"] = cancelUrl
        };

        if (setupFutureUsage)
        {
            payload["force_recurring"] = true;

            // Free trial / Pre-Auth logic
            if (amountInCents == 0)
            {
                payload["skip_capture"] = true;
            }
        }

        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}purchases/");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CHIP Collect checkout failed for Tenant {TenantId}: {Status} - {Body}", tenantId, response.StatusCode, responseBody);
                return new GatewayCheckoutResult(false, null, null, $"CHIP API error: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var checkoutUrl = root.TryGetProperty("checkout_url", out var urlEl) ? urlEl.GetString() : null;
            var purchaseId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            if (string.IsNullOrEmpty(checkoutUrl))
            {
                return new GatewayCheckoutResult(false, null, null, "CHIP returned no checkout URL.");
            }

            return new GatewayCheckoutResult(true, checkoutUrl, purchaseId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CHIP Collect checkout exception for Tenant {TenantId}", tenantId);
            return new GatewayCheckoutResult(false, null, null, ex.Message);
        }
    }

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey, string webhookSecret, string rawBody, Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0, decimal fixedFee = 0, decimal taxRate = 0)
    {
        throw new NotImplementedException("Implementation will be added in Phase 4.");
    }

    public Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, 
        string currency, string description, string receipt)
    {
        throw new NotImplementedException("Implementation will be added in Phase 5.");
    }

    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        throw new NotImplementedException("Implementation will be added in Phase 6.");
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("CHIP Collect does not provide a managed customer billing portal.");
    }

    private static string ExtractName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Customer";
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : "Customer";
    }
}
