using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
        try
        {
            var signatureHeaderKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Signature", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(signatureHeaderKey) || !headers.TryGetValue(signatureHeaderKey, out var signatureBase64))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing X-Signature header."));
            }

            var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(webhookSecret);

            bool isValid = rsa.VerifyData(bodyBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValid)
            {
                _logger.LogWarning("CHIP Collect RSA signature verification failed.");
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "RSA signature verification failed."));
            }

            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var rawEventType = root.TryGetProperty("event_type", out var etProp) ? etProp.GetString() : null;
            var mappedEventType = "";

            if (rawEventType == "purchase.paid" || rawEventType == "purchase.preauthorized")
            {
                mappedEventType = "PAYMENT_COMPLETED";
            }
            else if (rawEventType == "purchase.payment_failure")
            {
                mappedEventType = "PAYMENT_FAILED";
            }
            else
            {
                return Task.FromResult(new GatewayWebhookParsedResult(true, rawEventType ?? "", "", 0, "", null, new(), 0, 0, 0, 1, "", null));
            }

            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var purchaseId = eventId; 

            var purchaseNode = root.TryGetProperty("purchase", out var pNode) ? pNode : default;
            var amountCents = purchaseNode.ValueKind != JsonValueKind.Undefined && purchaseNode.TryGetProperty("total", out var tProp) ? tProp.GetDecimal() : 0m;
            var amountPaid = amountCents / 100m;
            
            var currency = purchaseNode.ValueKind != JsonValueKind.Undefined && purchaseNode.TryGetProperty("currency", out var cProp) ? cProp.GetString() ?? "MYR" : "MYR";

            decimal gatewayFee = 0m;
            decimal netAmount = amountPaid;

            if (root.TryGetProperty("payment", out var paymentNode) && paymentNode.ValueKind == JsonValueKind.Object)
            {
                gatewayFee = paymentNode.TryGetProperty("fee_amount", out var faProp) ? faProp.GetDecimal() / 100m : 0m;
                netAmount = paymentNode.TryGetProperty("net_amount", out var naProp) ? naProp.GetDecimal() / 100m : amountPaid;
            }

            var meta = new Dictionary<string, string>();
            if (purchaseNode.ValueKind != JsonValueKind.Undefined && purchaseNode.TryGetProperty("metadata", out var metaNode) && metaNode.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in metaNode.EnumerateObject())
                {
                    meta[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            string? tokenId = null;
            if (root.TryGetProperty("is_recurring_token", out var isRecProp) && isRecProp.GetBoolean())
            {
                tokenId = purchaseId;
            }

            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: mappedEventType,
                EventId: eventId,
                AmountPaid: amountPaid,
                Currency: currency,
                GatewayTransactionId: purchaseId,
                Metadata: meta,
                GatewayFee: gatewayFee,
                TaxAmount: 0m,
                NetAmount: netAmount,
                FxRate: 1m,
                BaseCurrency: currency,
                Error: null,
                GatewayCustomerId: null, 
                GatewayTokenId: tokenId
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CHIP Collect webhook");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message));
        }
    }

    public async Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, 
        string currency, string description, string receipt)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Fetch the original purchase to extract required fields (brand_id and client details)
            var oldPurchaseResponse = await client.GetAsync($"{ApiBaseUrl}purchases/{tokenId}/");
            if (!oldPurchaseResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch original CHIP purchase {TokenId} for off-session charge.", tokenId);
                return false;
            }

            var oldPurchaseJson = await oldPurchaseResponse.Content.ReadAsStringAsync();
            using var oldDoc = JsonDocument.Parse(oldPurchaseJson);
            var oldRoot = oldDoc.RootElement;
            
            var brandId = oldRoot.GetProperty("brand_id").GetString();
            var clientNode = oldRoot.GetProperty("client");
            var clientEmail = clientNode.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "customer@example.com";
            var clientName = clientNode.TryGetProperty("full_name", out var nameProp) ? nameProp.GetString() : "Customer";

            // Create a new unpaid purchase mapped to the original customer
            var amountInCents = (int)Math.Round(amount * 100, 0);
            var newPurchasePayload = new Dictionary<string, object>
            {
                ["brand_id"] = brandId!,
                ["client"] = new { email = clientEmail, full_name = clientName },
                ["purchase"] = new
                {
                    products = new[]
                    {
                        new { name = description, price = amountInCents }
                    }
                }
            };

            var createResponse = await client.PostAsJsonAsync($"{ApiBaseUrl}purchases/", newPurchasePayload);
            if (!createResponse.IsSuccessStatusCode)
            {
                var errorBody = await createResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create new CHIP purchase for off-session charge. Error: {Error}", errorBody);
                return false;
            }

            var createJson = await createResponse.Content.ReadAsStringAsync();
            using var createDoc = JsonDocument.Parse(createJson);
            var newPurchaseId = createDoc.RootElement.GetProperty("id").GetString();

            // Execute the charge using the vaulted recurring_token
            var chargePayload = new { recurring_token = tokenId };
            var chargeResponse = await client.PostAsJsonAsync($"{ApiBaseUrl}purchases/{newPurchaseId}/charge/", chargePayload);
            
            if (chargeResponse.IsSuccessStatusCode)
            {
                var chargeJson = await chargeResponse.Content.ReadAsStringAsync();
                using var chargeDoc = JsonDocument.Parse(chargeJson);
                var status = chargeDoc.RootElement.GetProperty("status").GetString();
                
                return status == "paid" || status == "pending_charge";
            }

            var chargeError = await chargeResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to charge CHIP token {TokenId} for purchase {NewPurchaseId}. Error: {Error}", tokenId, newPurchaseId, chargeError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during CHIP off-session charge for token {TokenId}", tokenId);
            return false;
        }
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
