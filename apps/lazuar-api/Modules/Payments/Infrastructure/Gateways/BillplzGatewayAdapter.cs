// ==============================================================================================
// DONT DELETE COMMENT HERE. IF you need to modify, just modify specific code and dont delete the comment.
//
// HISTORICAL BUG CONTEXT: BILLPLZ WEBHOOK VERIFICATION (x_signature)
// 
// Billplz sends webhook callbacks as `application/x-www-form-urlencoded`. To verify the authenticity 
// of the payload, we must compute an HMAC-SHA256 signature using the X-Signature Key.
//
// PREVIOUS BUG:
// We originally used ASP.NET's standard `QueryHelpers.ParseQuery(body)` to parse the incoming webhook. 
// However, `QueryHelpers` applies aggressive URL-decoding (e.g., automatically converting `+` to spaces, 
// handling form-data arrays, etc.). This caused slight mutations in the extracted string values.
// Because HMAC hashing requires byte-for-byte exactness, these tiny mutations caused the computed 
// signature to mismatch the provided `x_signature`, leading to false-positive 400 Bad Request errors.
//
// FIX IMPLEMENTED & REQUIRED TO MAINTAIN:
// We MUST use the custom `ParseFormBody` method at the bottom of this class. It manually splits the 
// raw body by `&` and `=`, and strictly uses `Uri.UnescapeDataString()`. This perfectly mimics the 
// PHP/Ruby parameter extraction logic used by Billplz internally, guaranteeing that our signature 
// computation always matches theirs. We also explicitly map '+' to ' ' before unescaping.
// ==============================================================================================

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

public class BillplzGatewayAdapter : IPaymentGatewayAdapter
{
    private const string ProductionApiUrl = "https://www.billplz.com/api/v3/";
    private const string SandboxApiUrl = "https://www.billplz-sandbox.com/api/v3/";
    
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BillplzGatewayAdapter> _logger;

    public string GatewayType => "BILLPLZ";

    // Fields that Billplz only includes in x_signature computation when "Enable Extra Payment Completion Information" is checked.
    private static readonly HashSet<string> ExtraFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid_at", "transaction_id", "transaction_status"
    };

    // Fields that are never part of the signature.
    private static readonly HashSet<string> AlwaysExclude = new(StringComparer.OrdinalIgnoreCase)
    {
        "x_signature"
    };

    public BillplzGatewayAdapter(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<BillplzGatewayAdapter> logger)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail,
        string successUrl, string cancelUrl, Dictionary<string, string> metadata, string? merchantId)
    {
        if (string.IsNullOrEmpty(merchantId))
        {
            return new GatewayCheckoutResult(false, null, null, "MerchantId (Collection ID) is required for Billplz.");
        }

        var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        var isProd = apiBaseUrl.Contains("lazuar.com");
        var endpoint = isProd ? ProductionApiUrl : SandboxApiUrl;

        metadata.TryGetValue("type", out var type);
        var ref1 = metadata.TryGetValue("subscription_id", out var subId) ? subId : tenantId.ToString();
        
        // Clean webhook URL without query parameters. Context is preserved in reference_1 and reference_2
        var webhookUrl = $"{apiBaseUrl}/webhooks/payments/billplz/{tenantId}";

        if (webhookUrl.Contains("localhost"))
        {
            webhookUrl = webhookUrl.Replace("localhost", "lazuar-local-dev.com");
        }

        var amountCents = (int)(amount * 100);
        var payload = new Dictionary<string, object>
        {
            ["collection_id"] = merchantId,
            ["email"] = string.IsNullOrWhiteSpace(customerEmail) ? "customer@example.com" : customerEmail,
            ["name"] = ExtractName(customerEmail),
            ["amount"] = amountCents,
            ["description"] = string.IsNullOrWhiteSpace(productName) ? "Lazuar Payment" : productName,
            ["callback_url"] = webhookUrl,
            ["redirect_url"] = successUrl,
            ["reference_1_label"] = "Reference",
            ["reference_1"] = ref1,
            ["reference_2_label"] = "Type",
            ["reference_2"] = type ?? "payment",
        };

        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}bills");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:")));
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Billplz checkout failed for Tenant {TenantId}: {Status} - {Body}", tenantId, response.StatusCode, responseBody);
                return new GatewayCheckoutResult(false, null, null, $"Billplz API error: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var billUrl = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            var billId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            if (string.IsNullOrEmpty(billUrl))
            {
                return new GatewayCheckoutResult(false, null, null, "Billplz returned no checkout URL.");
            }

            return new GatewayCheckoutResult(true, billUrl, billId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Billplz checkout exception for Tenant {TenantId}", tenantId);
            return new GatewayCheckoutResult(false, null, null, ex.Message);
        }
    }

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey, string webhookSecret, string rawBody, Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0, decimal fixedFee = 0, decimal taxRate = 0)
    {
        try
        {
            var formData = ParseFormBody(rawBody);
            
            if (!formData.TryGetValue("x_signature", out var providedSignature) || string.IsNullOrEmpty(providedSignature))
            {
                _logger.LogWarning("Billplz webhook failed: Missing x_signature in payload. Payload: {RawBody}", rawBody);
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing x_signature in Billplz callback."));
            }

            // Strategy 1: Exclude extra fields (When "Enable Extra Payment Completion Information" is UNCHECKED)
            var withoutExtra = ComputeHmac(formData, webhookSecret, excludeExtra: true);
            
            // Strategy 2: Include extra fields (When "Enable Extra Payment Completion Information" is CHECKED)
            var withExtra = ComputeHmac(formData, webhookSecret, excludeExtra: false);

            if (!string.Equals(providedSignature, withoutExtra, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(providedSignature, withExtra, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Billplz webhook failed: Signature mismatch. Provided: {Provided}, ComputedWithoutExtra: {WithoutExtra}, ComputedWithExtra: {WithExtra}", providedSignature, withoutExtra, withExtra);
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Billplz x_signature verification failed."));
            }

            var paid = formData.GetValueOrDefault("paid", "false");
            var state = formData.GetValueOrDefault("state", "due");
            var billId = formData.GetValueOrDefault("id", "");
            var paidAmountCents = int.TryParse(formData.GetValueOrDefault("paid_amount", "0"), out var pac) ? pac : 0;
            var paidAmountMyr = paidAmountCents / 100m;
            var isPaid = paid.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         state.Equals("paid", StringComparison.OrdinalIgnoreCase);

            // Extract metadata from custom references sent by Billplz in the body (set during checkout)
            var reference1 = formData.GetValueOrDefault("reference_1", "");
            var reference2 = formData.GetValueOrDefault("reference_2", "");

            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(reference2)) metadata["type"] = reference2;
            if (!string.IsNullOrEmpty(reference1)) metadata["subscription_id"] = reference1;

            decimal gatewayFee = (paidAmountMyr * (estimatedFeePercentage / 100m)) + fixedFee;
            if (gatewayFee < 0) gatewayFee = 0;
            
            decimal taxAmount = 0; 
            decimal netAmount = paidAmountMyr - gatewayFee;

            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED",
                EventId: billId,
                AmountPaid: paidAmountMyr,
                Currency: "MYR",
                GatewayTransactionId: billId,
                Metadata: metadata,
                GatewayFee: gatewayFee,
                TaxAmount: taxAmount,
                NetAmount: netAmount,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Billplz webhook");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message));
        }
    }

    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        _logger.LogWarning("Billplz does not support automated API refunds. Transaction {TransactionId} must be refunded manually via the Billplz Dashboard.", transactionId);
        return Task.FromResult(false);
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("Billplz does not provide a managed customer billing portal.");
    }

    private static string ComputeHmac(Dictionary<string, string> formData, string secretKey, bool excludeExtra)
    {
        var elements = formData
            .Where(kv =>
            {
                if (AlwaysExclude.Contains(kv.Key)) return false;
                if (excludeExtra && ExtraFields.Contains(kv.Key)) return false;
                return true;
            })
            .Select(kv => $"{kv.Key}{kv.Value}")
            .OrderBy(element => element, StringComparer.Ordinal)
            .ToList();

        var sourceString = string.Join("|", elements);
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var dataBytes = Encoding.UTF8.GetBytes(sourceString);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static Dictionary<string, string> ParseFormBody(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(body)) return result;

        foreach (var pair in body.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                // Form payloads encode spaces as '+'. We must replace them with ' ' 
                // BEFORE unescaping, otherwise the HMAC signature will mismatch.
                var key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
                var value = Uri.UnescapeDataString(parts[1].Replace("+", " "));
                result[key] = value;
            }
        }

        return result;
    }

    private static string ExtractName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Customer";
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : "Customer";
    }
}
