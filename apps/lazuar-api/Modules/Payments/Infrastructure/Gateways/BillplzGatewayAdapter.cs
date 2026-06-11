using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var queryParams = $"?type={Uri.EscapeDataString(type ?? "payment")}&subscription_id={Uri.EscapeDataString(ref1)}";
        var webhookUrl = $"{apiBaseUrl}/webhooks/payments/billplz/{tenantId}{queryParams}";

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
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing x_signature in Billplz callback."));
            }

            var computedSig = ComputeHmac(formData, webhookSecret, excludeExtra: false);
            if (!string.Equals(providedSignature, computedSig, StringComparison.OrdinalIgnoreCase))
            {
                computedSig = ComputeHmac(formData, webhookSecret, excludeExtra: true);
                if (!string.Equals(providedSignature, computedSig, StringComparison.OrdinalIgnoreCase))
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

            var reference1 = headers.GetValueOrDefault("Query-subscription_id", "");
            var reference2 = headers.GetValueOrDefault("Query-type", "");

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
        var extraFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "paid_at", "transaction_id", "transaction_status" };
        var elements = formData
            .Where(kv => !string.Equals(kv.Key, "x_signature", StringComparison.OrdinalIgnoreCase))
            .Where(kv => !(excludeExtra && extraFields.Contains(kv.Key)))
            .Select(kv => $"{kv.Key}{kv.Value}")
            .OrderBy(element => element, StringComparer.Ordinal)
            .ToList();

        var sourceString = string.Join("|", elements);
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var dataBytes = Encoding.UTF8.GetBytes(sourceString);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Dictionary<string, string> ParseFormBody(string body)
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

    private static string ExtractName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Customer";
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : "Customer";
    }
}
