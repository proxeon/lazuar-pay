// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs
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
        Dictionary<string, string> metadata, string? merchantId, bool setupFutureUsage = false, int quantity = 1)
    {
        if (string.IsNullOrEmpty(merchantId))
        {
            return new GatewayCheckoutResult(false, null, null, "MerchantId (Brand ID) is required for CHIP Collect.");
        }

        var amountInCents = GatewayCommon.ToMinorUnitsRounded(amount, quantity);
        var finalDescription = GatewayCommon.ProductDescription(productName, quantity);

        GatewayCommon.ApplyPayingTenantMetadata(metadata, tenantId);
        var clientName = GatewayCommon.ExtractName(customerEmail);

        var payload = new Dictionary<string, object>
        {
            ["brand_id"] = merchantId,
            ["client"] = new
            {
                email = GatewayCommon.ResolveEmail(customerEmail),
                full_name = clientName
            },
            ["purchase"] = new
            {
                products = new[]
                {
                    new
                    {
                        name = finalDescription,
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
            var (previewCustomerId, previewTokenId) = ExtractVaultIds(root);

            // purchase.paid is captured money. $0 skip_capture vaulting only fires
            // purchase.preauthorized — treat that as completed when a recurring token is present.
            if (rawEventType == "purchase.paid"
                || (rawEventType == "purchase.preauthorized" && !string.IsNullOrWhiteSpace(previewTokenId)))
            {
                mappedEventType = "PAYMENT_COMPLETED";
            }
            else if (rawEventType == "purchase.payment_failure")
            {
                mappedEventType = "PAYMENT_FAILED";
            }
            else if (rawEventType == "payment.refunded")
            {
                mappedEventType = "REFUND_COMPLETED";
            }
            else
            {
                return Task.FromResult(new GatewayWebhookParsedResult(true, rawEventType ?? "", "", 0, "", null, new(), 0, 0, 0, 1, "", null));
            }

            var purchaseId = ReadStablePurchaseId(root);
            if (string.IsNullOrWhiteSpace(purchaseId))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(
                    false, mappedEventType, "", 0, "", null, new(), 0, 0, 0, 1, "",
                    "Missing stable CHIP purchase id").AsUnusable());
            }

            var eventId = $"{mappedEventType}:{purchaseId}";

            var purchaseNode = root.TryGetProperty("purchase", out var pNode) ? pNode : default;
            var amountCents = purchaseNode.ValueKind != JsonValueKind.Undefined && purchaseNode.TryGetProperty("total", out var tProp) ? tProp.GetDecimal() : 0m;
            var amountPaid = amountCents / 100m;
            
            var rawCurrency = purchaseNode.ValueKind != JsonValueKind.Undefined && purchaseNode.TryGetProperty("currency", out var cProp)
                ? cProp.GetString()
                : null;
            if (!GatewayCommon.TryNormalizeCurrency(rawCurrency, out var currency))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(
                    false, mappedEventType, eventId, 0, "", purchaseId, new(), 0, 0, 0, 1, "",
                    "Missing purchase currency; refusing to default to MYR.").AsUnusable());
            }

            decimal gatewayFee = 0m;
            decimal netAmount = amountPaid;
            var feeKnown = false;

            if (root.TryGetProperty("payment", out var paymentNode) && paymentNode.ValueKind == JsonValueKind.Object)
            {
                if (paymentNode.TryGetProperty("fee_amount", out var faProp))
                {
                    gatewayFee = faProp.GetDecimal() / 100m;
                    feeKnown = true;
                }

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

            GatewayCommon.StampGatewayFeeStatus(meta, feeKnown);

            var customerId = previewCustomerId;
            var tokenId = previewTokenId;

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
                GatewayCustomerId: customerId,
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
        string currency, string description, string receipt, Guid tenantId,
        Guid? dunningCampaignId = null, string? idempotencyKey = null,
        Guid? chargeAttemptId = null,
        decimal taxAmount = 0,
        string? taxType = null)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var (brandId, clientEmail, clientName) = await ResolveOffSessionClientAsync(
                client, tokenId, customerId);
            if (string.IsNullOrWhiteSpace(brandId))
            {
                _logger.LogError(
                    "Failed to resolve CHIP brand for off-session charge (token {TokenId}, customer {CustomerId}).",
                    tokenId, customerId);
                return false;
            }

            var meta = new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = receipt,
                ["tenant_id"] = tenantId.ToString(),
                ["receipt"] = receipt
            };
            if (dunningCampaignId.HasValue)
            {
                meta["dunning_campaign_id"] = dunningCampaignId.Value.ToString();
            }

            if (chargeAttemptId.HasValue)
            {
                meta["charge_attempt_id"] = chargeAttemptId.Value.ToString();
            }

            if (taxAmount > 0)
            {
                meta["sst_tax_amount"] = taxAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(taxType))
                {
                    meta["sst_tax_type"] = taxType;
                }
            }

            var processorKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            if (!string.IsNullOrWhiteSpace(processorKey))
            {
                var existingId = await TryFindPurchaseIdByReferenceAsync(client, processorKey);
                if (!string.IsNullOrWhiteSpace(existingId))
                {
                    return await ChargeOrReusePurchaseAsync(client, existingId, tokenId, processorKey, lookupStatus: true);
                }
            }

            var amountInCents = GatewayCommon.ToMinorUnitsRounded(amount);
            var newPurchasePayload = new Dictionary<string, object>
            {
                ["brand_id"] = brandId!,
                ["client"] = new { email = clientEmail, full_name = clientName },
                ["purchase"] = new
                {
                    products = new[]
                    {
                        new { name = description, price = amountInCents }
                    },
                    metadata = meta
                }
            };
            if (!string.IsNullOrWhiteSpace(processorKey))
            {
                newPurchasePayload["reference"] = processorKey;
            }

            var createResponse = await SendJsonAsync(
                client, HttpMethod.Post, $"{ApiBaseUrl}purchases/", newPurchasePayload, processorKey);
            if (!createResponse.IsSuccessStatusCode)
            {
                var errorBody = await createResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create new CHIP purchase for off-session charge. Error: {Error}", errorBody);
                return false;
            }

            var createJson = await createResponse.Content.ReadAsStringAsync();
            using var createDoc = JsonDocument.Parse(createJson);
            var newPurchaseId = createDoc.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(newPurchaseId))
            {
                return false;
            }

            return await ChargeOrReusePurchaseAsync(client, newPurchaseId, tokenId, processorKey, lookupStatus: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during CHIP off-session charge for token {TokenId}", tokenId);
            return false;
        }
    }

    public async Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            object payload = new { };
            
            if (amount > 0)
            {
                payload = new { amount = GatewayCommon.ToMinorUnitsRounded(amount) };
            }

            var response = await SendJsonAsync(
                client,
                HttpMethod.Post,
                $"{ApiBaseUrl}purchases/{transactionId}/refund/",
                payload,
                GatewayCommon.FormatRefundIdempotencyKey(transactionId, amount));

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("CHIP Collect refund failed for Transaction {TransactionId}. Status: {Status}, Error: {Error}", transactionId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during CHIP Collect refund for Transaction {TransactionId}", transactionId);
            return false;
        }
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("CHIP Collect does not provide a managed customer billing portal.");
    }

    private async Task<bool> ChargeOrReusePurchaseAsync(
        HttpClient client,
        string purchaseId,
        string tokenId,
        string? processorKey,
        bool lookupStatus)
    {
        if (lookupStatus)
        {
            var get = await client.GetAsync($"{ApiBaseUrl}purchases/{purchaseId}/");
            if (get.IsSuccessStatusCode)
            {
                using var existingDoc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
                var existingStatus = existingDoc.RootElement.TryGetProperty("status", out var st)
                    ? st.GetString()
                    : null;
                if (IsOffSessionPaid(existingStatus))
                {
                    return true;
                }
            }
        }

        var chargePayload = new { recurring_token = tokenId };
        var chargeResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"{ApiBaseUrl}purchases/{purchaseId}/charge/",
            chargePayload,
            processorKey);

        if (chargeResponse.IsSuccessStatusCode)
        {
            var chargeJson = await chargeResponse.Content.ReadAsStringAsync();
            using var chargeDoc = JsonDocument.Parse(chargeJson);
            var status = chargeDoc.RootElement.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString()
                : null;
            return IsOffSessionPaid(status);
        }

        var chargeError = await chargeResponse.Content.ReadAsStringAsync();
        _logger.LogError(
            "Failed to charge CHIP token {TokenId} for purchase {PurchaseId}. Error: {Error}",
            tokenId, purchaseId, chargeError);
        return false;
    }

    private async Task<string?> TryFindPurchaseIdByReferenceAsync(HttpClient client, string reference)
    {
        var url = $"{ApiBaseUrl}purchases/?reference={Uri.EscapeDataString(reference)}";
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("results", out var results)
                && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in results.EnumerateArray())
                {
                    var id = ReadString(item, "id");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var id = ReadString(item, "id");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object payload,
        string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Content = JsonContent.Create(payload);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private async Task<(string? BrandId, string ClientEmail, string ClientName)> ResolveOffSessionClientAsync(
        HttpClient client,
        string tokenId,
        string customerId)
    {
        foreach (var candidate in new[] { tokenId, customerId }.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            var purchase = await client.GetAsync($"{ApiBaseUrl}purchases/{candidate}/");
            if (purchase.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await purchase.Content.ReadAsStringAsync());
                return ReadBrandAndClient(doc.RootElement);
            }
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            var chipClient = await client.GetAsync($"{ApiBaseUrl}clients/{customerId}/");
            if (chipClient.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await chipClient.Content.ReadAsStringAsync());
                return ReadBrandAndClient(doc.RootElement);
            }
        }

        return (null, GatewayCommon.PlaceholderEmail, "Customer");
    }

    private static (string? BrandId, string ClientEmail, string ClientName) ReadBrandAndClient(JsonElement root)
    {
        var brandId = root.TryGetProperty("brand_id", out var brand) ? brand.GetString() : null;
        var clientNode = root.TryGetProperty("client", out var node) && node.ValueKind == JsonValueKind.Object
            ? node
            : root;
        var clientEmail = clientNode.TryGetProperty("email", out var emailProp)
            ? emailProp.GetString()
            : GatewayCommon.PlaceholderEmail;
        var clientName = clientNode.TryGetProperty("full_name", out var nameProp)
            ? nameProp.GetString()
            : "Customer";
        return (brandId, clientEmail ?? GatewayCommon.PlaceholderEmail, clientName ?? "Customer");
    }

    internal static bool IsOffSessionPaid(string? status) =>
        string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recurring token from root or purchase; client.id from root.client or purchase.client.
    /// Charge path only needs the token — if customer is missing, customer falls back to token.
    /// </summary>
    /// <summary>
    /// Nested <c>purchase.id</c> first, then root <c>id</c>. Never invent a Guid.
    /// </summary>
    internal static string? ReadStablePurchaseId(JsonElement root)
    {
        if (root.TryGetProperty("purchase", out var purchase)
            && purchase.ValueKind == JsonValueKind.Object)
        {
            var nested = ReadString(purchase, "id");
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return ReadString(root, "id");
    }

    internal static (string? CustomerId, string? TokenId) ExtractVaultIds(JsonElement root)
    {
        var purchaseId = ReadString(root, "id");
        var purchaseNode = root.TryGetProperty("purchase", out var pNode) ? pNode : default;

        var isRecurring = IsTrue(root, "is_recurring_token") || IsTrue(purchaseNode, "is_recurring_token");
        var recurringToken = ReadString(root, "recurring_token") ?? ReadString(purchaseNode, "recurring_token");

        string? tokenId = null;
        if (!string.IsNullOrWhiteSpace(recurringToken))
        {
            tokenId = recurringToken;
        }
        else if (isRecurring)
        {
            tokenId = ReadStablePurchaseId(root) ?? purchaseId;
        }

        var customerId = ReadClientId(root) ?? ReadClientId(purchaseNode);
        if (string.IsNullOrWhiteSpace(customerId) && !string.IsNullOrWhiteSpace(tokenId))
        {
            customerId = tokenId;
        }

        return (customerId, tokenId);
    }

    private static bool IsTrue(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var prop)
               && prop.ValueKind == JsonValueKind.True;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var prop)
            || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = prop.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadClientId(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("client", out var client)
            || client.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(client, "id");
    }

}
