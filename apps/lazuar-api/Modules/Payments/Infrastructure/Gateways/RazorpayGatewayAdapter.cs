// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;
using Razorpay.Api;

namespace Modules.Payments.Infrastructure.Gateways;

public class RazorpayGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly ILogger<RazorpayGatewayAdapter> _logger;

    public RazorpayGatewayAdapter(ILogger<RazorpayGatewayAdapter> logger)
    {
        _logger = logger;
    }

    public string GatewayType => "RAZORPAY";

    private RazorpayClient GetClient(string apiKey)
    {
        var parts = apiKey.Split(':');
        var keyId = parts[0];
        var keySecret = parts.Length > 1 ? parts[1] : "";
        return new RazorpayClient(keyId, keySecret);
    }

    public Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail, string successUrl, string cancelUrl,
        Dictionary<string, string> metadata, string? merchantId, bool setupFutureUsage = false, int quantity = 1)
    {
        try
        {
            // Reminder-only: we do not claim e-mandate. SetupFutureUsage still mints a
            // payment link, not a card-registration mandate (max_amount = 10× first charge).
            _ = setupFutureUsage;
            var client = GetClient(apiKey);
            var req = BuildPaymentLinkRequest(
                amount, currency, productName, customerEmail, successUrl, metadata, quantity);
            var link = client.PaymentLink.Create(req);
            return Task.FromResult(new GatewayCheckoutResult(true, link["short_url"].ToString(), link["id"].ToString(), null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay checkout generation failed for Tenant {TenantId}", tenantId);
            return Task.FromResult(new GatewayCheckoutResult(false, null, null, ex.Message));
        }
    }

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey, string webhookSecret, string rawBody, Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0, decimal fixedFee = 0, decimal taxRate = 0)
    {
        try
        {
            var signatureHeaderKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Razorpay-Signature", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(signatureHeaderKey) || !headers.TryGetValue(signatureHeaderKey, out var signature))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing X-Razorpay-Signature header."));
            }

            Utils.verifyWebhookSignature(rawBody, signature, webhookSecret);

            using var doc = JsonDocument.Parse(rawBody);
            var eventType = doc.RootElement.GetProperty("event").GetString();

            if (IsPaymentFailedEvent(eventType))
            {
                return Task.FromResult(MapPaymentFailed(doc, headers, eventType));
            }

            if (eventType != "payment.captured")
            {
                return Task.FromResult(new GatewayWebhookParsedResult(true, eventType ?? "", "", 0, "", null, new(), 0, 0, 0, 1, "", null));
            }

            var paymentEntity = doc.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var paymentId = paymentEntity.TryGetProperty("id", out var paymentIdEl) ? paymentIdEl.GetString() : null;

            // Prefer Razorpay delivery id. Bare payment id is not an EventId — fail and
            // capture for the same pay_ would otherwise collide (008 residual).
            var eventId = ResolveEventId(headers, "PAYMENT_COMPLETED", paymentId);

            if (string.IsNullOrWhiteSpace(eventId))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(
                    false, "", "", 0, "", null, new(), 0, 0, 0, 1, "",
                    "Missing stable EventId: no X-Razorpay-Event-Id header and no payment id."));
            }

            var amount = paymentEntity.GetProperty("amount").GetDecimal() / 100m;
            var fee = paymentEntity.TryGetProperty("fee", out var feeEl) && feeEl.ValueKind != JsonValueKind.Null ? feeEl.GetDecimal() / 100m : 0m;
            var tax = paymentEntity.TryGetProperty("tax", out var taxEl) && taxEl.ValueKind != JsonValueKind.Null ? taxEl.GetDecimal() / 100m : 0m;
            var netAmount = amount - fee;

            var meta = new Dictionary<string, string>();
            if (paymentEntity.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in notes.EnumerateObject())
                {
                    meta[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            var customerId = paymentEntity.TryGetProperty("customer_id", out var cid) ? cid.GetString() : null;
            var tokenId = paymentEntity.TryGetProperty("token_id", out var tid) ? tid.GetString() : null;
            if (!TryReadCurrency(paymentEntity, out var currency))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(
                    false, "", "", 0, "", null, new(), 0, 0, 0, 1, "",
                    "Missing payment currency; refusing to default to MYR."));
            }

            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_COMPLETED",
                EventId: eventId,
                AmountPaid: amount,
                Currency: currency,
                GatewayTransactionId: paymentId,
                Metadata: meta,
                GatewayFee: fee,
                TaxAmount: tax,
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
            _logger.LogError(ex, "Razorpay webhook verification failed");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message));
        }
    }

    public Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, string currency,
        string description, string receipt, Guid tenantId,
        Guid? dunningCampaignId = null, string? idempotencyKey = null,
        Guid? chargeAttemptId = null,
        decimal taxAmount = 0,
        string? taxType = null)
    {
        _ = idempotencyKey; // Razorpay recurring create has no idempotency key (best-effort).
        _ = taxAmount;
        _ = taxType;
        try
        {
            var client = GetClient(apiKey);

            var notes = new Dictionary<string, object>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = receipt,
                ["tenant_id"] = tenantId.ToString(),
                ["receipt"] = receipt
            };
            if (dunningCampaignId.HasValue)
            {
                notes["dunning_campaign_id"] = dunningCampaignId.Value.ToString();
            }

            if (chargeAttemptId.HasValue)
            {
                notes["charge_attempt_id"] = chargeAttemptId.Value.ToString();
            }

            var orderReq = new Dictionary<string, object>
            {
                { "amount", GatewayCommon.ToMinorUnitsTruncating(amount) },
                { "currency", currency.ToUpperInvariant() },
                { "receipt", receipt },
                { "payment_capture", true },
                { "notes", notes }
            };
            var order = client.Order.Create(orderReq);

            var payReq = new Dictionary<string, object>
            {
                { "amount", GatewayCommon.ToMinorUnitsTruncating(amount) },
                { "currency", currency.ToUpperInvariant() },
                { "order_id", order["id"].ToString() },
                { "customer_id", customerId },
                { "token", tokenId },
                { "recurring", true },
                { "description", description },
                { "notes", notes }
            };
            // Never invent billing@lazuar.com. Buyer email/phone come from checkout notes when present.
            if (notes.TryGetValue("customer_email", out var emailObj)
                && emailObj is string email
                && !string.IsNullOrWhiteSpace(email))
            {
                payReq["email"] = email;
            }

            if (notes.TryGetValue("customer_phone", out var phoneObj)
                && phoneObj is string phone
                && !string.IsNullOrWhiteSpace(phone))
            {
                payReq["contact"] = phone;
            }
            
            var payment = client.Payment.CreateRecurringPayment(payReq);
            return Task.FromResult(payment != null && payment["razorpay_payment_id"] != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay off-session charge failed for customer {CustomerId}", customerId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        try
        {
            var client = GetClient(apiKey);
            var refundReq = new Dictionary<string, object> { { "amount", GatewayCommon.ToMinorUnitsTruncating(amount) } };
            var refund = client.Payment.Fetch(transactionId).Refund(amount > 0 ? refundReq : null);
            return Task.FromResult(refund != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay refund failed for Transaction {TransactionId}", transactionId);
            return Task.FromResult(false);
        }
    }

    public Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        throw new InvalidOperationException("Razorpay does not provide a managed customer billing portal.");
    }

    internal static Dictionary<string, object> BuildPaymentLinkRequest(
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        Dictionary<string, string> metadata,
        int quantity)
    {
        var amountPaise = GatewayCommon.ToMinorUnitsTruncating(amount, quantity);
        var finalDescription = GatewayCommon.ProductDescription(productName, quantity);
        metadata.TryGetValue("customer_name", out var customerName);
        metadata.TryGetValue("customer_phone", out var customerPhone);
        var finalName = !string.IsNullOrWhiteSpace(customerName) ? customerName : GatewayCommon.ExtractName(customerEmail);
        var finalPhone = !string.IsNullOrWhiteSpace(customerPhone) ? customerPhone : "+60100000000";
        var notes = metadata.ToDictionary(k => k.Key, v => (object)v.Value);
        return new Dictionary<string, object>
        {
            ["amount"] = amountPaise,
            ["currency"] = currency.ToUpperInvariant(),
            ["description"] = finalDescription,
            ["customer"] = new Dictionary<string, object>
            {
                ["name"] = finalName,
                ["email"] = customerEmail,
                ["contact"] = finalPhone
            },
            ["notes"] = notes,
            ["callback_url"] = successUrl,
            ["callback_method"] = "get"
        };
    }

    internal static string? ResolveEventId(
        Dictionary<string, string> headers,
        string mappedEventType,
        string? paymentId)
    {
        var eventIdHeaderKey = headers.Keys.FirstOrDefault(k =>
            k.Equals("X-Razorpay-Event-Id", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(eventIdHeaderKey)
            && headers.TryGetValue(eventIdHeaderKey, out var headerEventId)
            && !string.IsNullOrWhiteSpace(headerEventId))
        {
            return headerEventId.Trim();
        }

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return null;
        }

        return mappedEventType + ":" + paymentId;
    }

    internal static bool IsPaymentFailedEvent(string? eventType) =>
        eventType is "payment.failed";

    internal static bool TryReadCurrency(JsonElement paymentEntity, out string currency)
    {
        currency = "";
        if (!paymentEntity.TryGetProperty("currency", out var cur) || cur.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = cur.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        currency = value.Trim().ToUpperInvariant();
        return true;
    }

    private static GatewayWebhookParsedResult MapPaymentFailed(
        JsonDocument doc,
        Dictionary<string, string> headers,
        string? eventType)
    {
        JsonElement paymentEntity = default;
        var hasEntity = doc.RootElement.TryGetProperty("payload", out var payload)
            && payload.TryGetProperty("payment", out var payment)
            && payment.TryGetProperty("entity", out paymentEntity);

        var paymentId = hasEntity && paymentEntity.TryGetProperty("id", out var paymentIdEl)
            ? paymentIdEl.GetString()
            : null;

        var eventId = ResolveEventId(headers, "PAYMENT_FAILED", paymentId);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new GatewayWebhookParsedResult(
                false, "", "", 0, "", null, new(), 0, 0, 0, 1, "",
                "Missing stable EventId for failed payment.");
        }

        var meta = new Dictionary<string, string>();
        if (hasEntity && paymentEntity.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in notes.EnumerateObject())
            {
                meta[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        var amount = hasEntity && paymentEntity.TryGetProperty("amount", out var amountEl)
            ? amountEl.GetDecimal() / 100m
            : 0m;
        string currency;
        if (hasEntity)
        {
            if (!TryReadCurrency(paymentEntity, out currency))
            {
                return new GatewayWebhookParsedResult(
                    false, "", "", 0, "", null, new(), 0, 0, 0, 1, "",
                    "Missing payment currency; refusing to default to MYR.");
            }
        }
        else
        {
            currency = "";
        }

        return new GatewayWebhookParsedResult(
            Verified: true,
            EventType: "PAYMENT_FAILED",
            EventId: eventId,
            AmountPaid: amount,
            Currency: currency,
            GatewayTransactionId: paymentId,
            Metadata: meta,
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: amount,
            FxRate: 1m,
            BaseCurrency: currency,
            Error: eventType);
    }
}
