// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
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
using Microsoft.AspNetCore.WebUtilities;
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

    private static readonly HashSet<string> ExtraFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid_at", "transaction_id", "transaction_status"
    };

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
        string successUrl, string cancelUrl, Dictionary<string, string> metadata, string? merchantId, bool setupFutureUsage = false, int quantity = 1)
    {
        if (string.IsNullOrEmpty(merchantId))
        {
            return new GatewayCheckoutResult(false, null, null, "MerchantId (Collection ID) is required for Billplz.");
        }

        var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
        if (!BillplzPublicBase.TryResolveCallbackBase(_configuration, apiBaseUrl, out var callbackBase, out var baseError))
        {
            return new GatewayCheckoutResult(false, null, null, baseError);
        }

        var isProd = BillplzPublicBase.IsProductionApi(_configuration, apiBaseUrl);
        var endpoint = isProd ? ProductionApiUrl : SandboxApiUrl;

        metadata.TryGetValue("type", out var type);
        metadata.TryGetValue("subscription_id", out var subId);
        metadata.TryGetValue("tenant_id", out var metaTenantId); 
        
        var ref1 = subId ?? metaTenantId ?? tenantId.ToString();
        var typeValue = type ?? "payment";
        
        var webhookUrl = $"{callbackBase}/webhooks/payments/billplz/{tenantId}";

        webhookUrl = $"{webhookUrl}?type={Uri.EscapeDataString(typeValue)}&reference_1={Uri.EscapeDataString(ref1)}";

        // M2M / integration checkouts: preserve checkout_id on callback query (Billplz strips body metadata).
        // Server-side session merge by bill id remains the safety net if this query param is lost.
        if (metadata.TryGetValue("checkout_id", out var checkoutId)
            && !string.IsNullOrWhiteSpace(checkoutId))
        {
            webhookUrl = $"{webhookUrl}&checkout_id={Uri.EscapeDataString(checkoutId)}";
        }

        var totalAmountCents = GatewayCommon.ToMinorUnitsTruncating(amount, quantity);
        var finalDescription = GatewayCommon.ProductDescription(productName, quantity);

        var payload = new Dictionary<string, object>
        {
            ["collection_id"] = merchantId,
            ["email"] = GatewayCommon.ResolveEmail(customerEmail),
            ["name"] = GatewayCommon.ExtractName(customerEmail),
            ["amount"] = totalAmountCents,
            ["description"] = finalDescription,
            ["callback_url"] = webhookUrl,
            ["redirect_url"] = successUrl,
            ["reference_1_label"] = "Reference",
            ["reference_1"] = ref1,
            ["reference_2_label"] = "Type",
            ["reference_2"] = typeValue,
        };

        try
        {
            var client = _httpFactory.CreateClient(PublicDnsFallback.HttpClientName);
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
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing x_signature in Billplz callback."));
            }

            var computedSigWithExtra = ComputeHmac(formData, webhookSecret, excludeExtra: false);

            if (!string.Equals(providedSignature, computedSigWithExtra, StringComparison.OrdinalIgnoreCase))
            {
                var computedSigWithoutExtra = ComputeHmac(formData, webhookSecret, excludeExtra: true);
                if (!string.Equals(providedSignature, computedSigWithoutExtra, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Billplz x_signature verification failed."));
                }
            }

            var paid = formData.GetValueOrDefault("paid", "false");
            var state = formData.GetValueOrDefault("state", "due");
            var billId = formData.GetValueOrDefault("id", "");
            var paidAmountCents = int.TryParse(formData.GetValueOrDefault("paid_amount", "0"), out var pac) ? pac : 0;
            var paidAmountMyr = paidAmountCents / 100m;

            var isPaid = paid.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         state.Equals("paid", StringComparison.OrdinalIgnoreCase);

            var reference1 = formData.GetValueOrDefault("reference_1", "");
            if (string.IsNullOrEmpty(reference1) && headers.TryGetValue("Query-reference_1", out var qsRef1))
            {
                reference1 = qsRef1;
            }
            if (string.IsNullOrEmpty(reference1) && headers.TryGetValue("Query-subscription_id", out var qsSubId))
            {
                reference1 = qsSubId; 
            }

            var reference2 = formData.GetValueOrDefault("reference_2", "");
            if (string.IsNullOrEmpty(reference2) && headers.TryGetValue("Query-type", out var qsType))
            {
                reference2 = qsType;
            }

            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(reference2)) 
            {
                metadata["type"] = reference2;
            }
            
            if (!string.IsNullOrEmpty(reference1)) 
            {
                if (reference2 == "utility_credit_topup")
                    metadata["tenant_id"] = reference1;
                else
                    metadata["subscription_id"] = reference1;
            }

            // Callback query checkout_id (set at GenerateCheckout for M2M).
            if (headers.TryGetValue("Query-checkout_id", out var qsCheckoutId)
                && !string.IsNullOrWhiteSpace(qsCheckoutId))
            {
                metadata["checkout_id"] = qsCheckoutId;
            }
            else if (formData.TryGetValue("checkout_id", out var bodyCheckoutId)
                     && !string.IsNullOrWhiteSpace(bodyCheckoutId))
            {
                metadata["checkout_id"] = bodyCheckoutId;
            }

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

    public Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, string currency,
        string description, string receipt, Guid tenantId, Guid? dunningCampaignId = null)
    {
        throw new NotSupportedException("Billplz does not support vaulted token off-session charges.");
    }

    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
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

        var parsed = QueryHelpers.ParseQuery(body);
        foreach (var parameter in parsed)
        {
            result[parameter.Key] = parameter.Value.ToString();
        }
        return result;
    }

}
