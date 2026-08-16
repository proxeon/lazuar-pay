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
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;

namespace Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// BYOK wrap of Xendit hosted invoices. Money settles on the tenant Xendit account.
/// Reminder-only until a payment-token soak proves off-session. We do not rebuild wallets.
/// </summary>
public class XenditGatewayAdapter : IPaymentGatewayAdapter
{
    public const string LiveApiBase = "https://api.xendit.co";
    internal const string CallbackTokenHeader = "x-callback-token";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<XenditGatewayAdapter> _logger;

    public XenditGatewayAdapter(IHttpClientFactory httpFactory, ILogger<XenditGatewayAdapter> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string GatewayType => "XENDIT";

    public async Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey,
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        string? merchantId,
        bool setupFutureUsage = false,
        int quantity = 1)
    {
        _ = merchantId;
        _ = setupFutureUsage; // hosted invoice only — no token vault in v1

        var payload = BuildInvoicePayload(tenantId, amount, currency, productName, customerEmail, successUrl, cancelUrl, metadata, quantity);

        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{LiveApiBase}/v2/invoices");
            request.Headers.Authorization = BasicAuth(apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xendit invoice create failed for tenant {TenantId}: {Status} {Body}", tenantId, response.StatusCode, body);
                return new GatewayCheckoutResult(false, null, null, $"Xendit API error: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var invoiceUrl = doc.RootElement.TryGetProperty("invoice_url", out var urlEl) ? urlEl.GetString() : null;
            var invoiceId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(invoiceUrl))
            {
                return new GatewayCheckoutResult(false, null, null, "Xendit returned no invoice_url.");
            }

            return new GatewayCheckoutResult(true, invoiceUrl, invoiceId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xendit checkout exception for tenant {TenantId}", tenantId);
            return new GatewayCheckoutResult(false, null, null, ex.Message);
        }
    }

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey,
        string webhookSecret,
        string rawBody,
        Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0,
        decimal fixedFee = 0,
        decimal taxRate = 0)
    {
        _ = apiKey;
        _ = estimatedFeePercentage;
        _ = fixedFee;
        _ = taxRate;

        try
        {
            if (!VerifyCallbackToken(webhookSecret, headers))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(
                    false, "", "", 0, "", null, new(), 0, 0, 0, 1, "",
                    "Missing or invalid x-callback-token."));
            }

            return Task.FromResult(MapInvoiceCallback(rawBody));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xendit webhook parse failed");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message));
        }
    }

    public async Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{LiveApiBase}/refunds");
            request.Headers.Authorization = BasicAuth(apiKey);
            request.Content = JsonContent.Create(new Dictionary<string, object>
            {
                ["invoice_id"] = transactionId,
                ["amount"] = amount,
                ["reason"] = "REQUESTED_BY_CUSTOMER"
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Xendit refund failed for {TransactionId}: {Status} {Body}", transactionId, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xendit refund exception for {TransactionId}", transactionId);
            return false;
        }
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("Xendit does not provide a managed customer billing portal.");
    }

    public Task<bool> ChargeOffSessionAsync(
        string apiKey,
        string customerId,
        string tokenId,
        decimal amount,
        string currency,
        string description,
        string receipt,
        Guid tenantId,
        Guid? dunningCampaignId = null,
        string? idempotencyKey = null,
        Guid? chargeAttemptId = null)
    {
        _ = (apiKey, customerId, tokenId, amount, currency, description, receipt, tenantId, dunningCampaignId, idempotencyKey, chargeAttemptId);
        // Honest: hosted invoices do not vault. Stay reminder-only until payment tokens soak.
        return Task.FromResult(false);
    }

    internal static Dictionary<string, object> BuildInvoicePayload(
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        int quantity)
    {
        var line = GatewayCommon.ToMinorUnitsRounded(amount, quantity) / 100m;
        metadata["tenant_id"] = tenantId.ToString();

        var payload = new Dictionary<string, object>
        {
            ["external_id"] = "lazuar_" + Guid.CreateVersion7().ToString("N"),
            ["amount"] = line,
            ["currency"] = (currency ?? "MYR").Trim().ToUpperInvariant(),
            ["description"] = GatewayCommon.ProductDescription(productName, quantity),
            ["payer_email"] = GatewayCommon.ResolveEmail(customerEmail),
            ["success_redirect_url"] = successUrl,
            ["failure_redirect_url"] = cancelUrl,
            ["metadata"] = metadata
        };

        var methods = ResolveRequestedPaymentMethods(metadata);
        if (methods.Count > 0)
        {
            payload["payment_methods"] = methods;
        }

        return payload;
    }

    /// <summary>
    /// Only request channels Xendit documents on hosted invoices. Unknown codes are dropped.
    /// Empty list = merchant dashboard defaults (honest wrap).
    /// </summary>
    internal static IReadOnlyList<string> MalaysiaHostedChannels { get; } =
    [
        "CREDIT_CARD",
        "DD_FPX",
        "QR_CODE",
        "OVO",
        "DANA",
        "LINKAJA",
        "SHOPEEPAY",
        "GCASH",
        "GRABPAY",
        "PAYMAYA"
    ];

    internal static List<string> ResolveRequestedPaymentMethods(IReadOnlyDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("xendit_payment_methods", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .Where(s => MalaysiaHostedChannels.Contains(s))
            .Distinct()
            .ToList();
    }

    internal static bool VerifyCallbackToken(string webhookSecret, Dictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return false;
        }

        var headerKey = headers.Keys.FirstOrDefault(k => k.Equals(CallbackTokenHeader, StringComparison.OrdinalIgnoreCase));
        if (headerKey == null || !headers.TryGetValue(headerKey, out var presented) || string.IsNullOrEmpty(presented))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(webhookSecret);
        var actual = Encoding.UTF8.GetBytes(presented);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    internal static GatewayWebhookParsedResult MapInvoiceCallback(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var invoice = root;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            invoice = data;
        }

        var status = ReadString(invoice, "status") ?? ReadString(root, "event") ?? "";
        var mapped = MapStatus(status);
        if (mapped == null)
        {
            return new GatewayWebhookParsedResult(true, status, "", 0, "", null, new(), 0, 0, 0, 1, "", null);
        }

        var invoiceId = ReadString(invoice, "id");
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            return new GatewayWebhookParsedResult(false, mapped, "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing Xendit invoice id.");
        }

        var currency = ReadString(invoice, "currency");
        if (string.IsNullOrWhiteSpace(currency))
        {
            return new GatewayWebhookParsedResult(
                false, mapped, "", 0, "", null, new(), 0, 0, 0, 1, "",
                "Missing invoice currency; refusing to default to MYR.");
        }

        var amount = 0m;
        if (invoice.TryGetProperty("paid_amount", out var paid) && paid.ValueKind == JsonValueKind.Number)
        {
            amount = paid.GetDecimal();
        }
        else if (invoice.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number)
        {
            amount = amt.GetDecimal();
        }

        var meta = new Dictionary<string, string>();
        if (invoice.TryGetProperty("metadata", out var metaNode) && metaNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in metaNode.EnumerateObject())
            {
                meta[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.ToString();
            }
        }

        if (invoice.TryGetProperty("external_id", out var ext) && ext.ValueKind == JsonValueKind.String)
        {
            meta["external_id"] = ext.GetString() ?? "";
        }

        var fee = 0m;
        if (invoice.TryGetProperty("fees_paid_amount", out var feeEl) && feeEl.ValueKind == JsonValueKind.Number)
        {
            fee = feeEl.GetDecimal();
        }

        return new GatewayWebhookParsedResult(
            Verified: true,
            EventType: mapped,
            EventId: invoiceId,
            AmountPaid: amount,
            Currency: currency.Trim().ToUpperInvariant(),
            GatewayTransactionId: invoiceId,
            Metadata: meta,
            GatewayFee: fee,
            TaxAmount: 0,
            NetAmount: amount - fee,
            FxRate: 1m,
            BaseCurrency: currency.Trim().ToUpperInvariant(),
            Error: mapped == "PAYMENT_FAILED" ? status : null);
    }

    internal static string? MapStatus(string status)
    {
        var s = status.Trim();
        if (s.Equals("PAID", StringComparison.OrdinalIgnoreCase)
            || s.Equals("SETTLED", StringComparison.OrdinalIgnoreCase)
            || s.Equals("invoice.paid", StringComparison.OrdinalIgnoreCase))
        {
            return "PAYMENT_COMPLETED";
        }

        if (s.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase)
            || s.Equals("FAILED", StringComparison.OrdinalIgnoreCase)
            || s.Equals("invoice.expired", StringComparison.OrdinalIgnoreCase)
            || s.Equals("invoice.failed", StringComparison.OrdinalIgnoreCase))
        {
            return "PAYMENT_FAILED";
        }

        return null;
    }

    private static AuthenticationHeaderValue BasicAuth(string apiKey)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey.Trim() + ":"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
