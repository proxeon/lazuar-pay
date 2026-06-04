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
        
        // Billplz requires a specific webhook URL per-bill
        var webhookUrl = $"{apiBaseUrl}/webhooks/payments/billplz/{tenantId}";

        var amountCents = (int)(amount * 100);

        // Billplz only allows two references. We map our metadata to them.
        metadata.TryGetValue("type", out var type);
        var ref1 = metadata.TryGetValue("subscription_id", out var subId) ? subId : tenantId.ToString();

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
        string webhookSecret, string rawBody, Dictionary<string, string> headers)
    {
        try
        {
            var formData = ParseFormBody(rawBody);

            if (!formData.TryGetValue("x_signature", out var providedSignature) || string.IsNullOrEmpty(providedSignature))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), "Missing x_signature in Billplz callback."));
            }

            // Strategy 1: Include extra fields in signature computation
            var computedSig = ComputeHmac(formData, webhookSecret, excludeExtra: false);

            if (!string.Equals(providedSignature, computedSig, StringComparison.OrdinalIgnoreCase))
            {
                // Strategy 2: Exclude extra fields
                computedSig = ComputeHmac(formData, webhookSecret, excludeExtra: true);
                if (!string.Equals(providedSignature, computedSig, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), "Billplz x_signature verification failed."));
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
            var reference2 = formData.GetValueOrDefault("reference_2", "");

            // Reconstruct the metadata dictionary
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(reference2)) metadata["type"] = reference2;
            if (!string.IsNullOrEmpty(reference1)) metadata["subscription_id"] = reference1;

            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED",
                EventId: billId, // Use billId as EventId for idempotency
                AmountPaid: paidAmountMyr,
                Currency: "MYR",
                GatewayTransactionId: billId,
                Metadata: metadata,
                Error: null
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Billplz webhook");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), ex.Message));
        }
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
