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
            var client = GetClient(apiKey);
            var amountPaise = (int)(amount * quantity * 100);
            var finalDescription = quantity > 1 ? $"{productName} (x{quantity})" : productName;
            
            metadata.TryGetValue("customer_name", out var customerName);
            metadata.TryGetValue("customer_phone", out var customerPhone);

            var finalName = !string.IsNullOrWhiteSpace(customerName) ? customerName : ExtractName(customerEmail);
            var finalPhone = !string.IsNullOrWhiteSpace(customerPhone) ? customerPhone : "+60100000000";

            var customer = new Dictionary<string, object>
            {
                { "name", finalName },
                { "email", customerEmail },
                { "contact", finalPhone }
            };
            
            var notes = metadata.ToDictionary(k => k.Key, v => (object)v.Value);

            if (setupFutureUsage)
            {
                var subReg = new Dictionary<string, object>
                {
                    { "method", "card" }, 
                    { "max_amount", amountPaise * 10 },
                    { "expire_at", DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds() }
                };

                var req = new Dictionary<string, object>
                {
                    { "type", "link" },
                    { "amount", amountPaise },
                    { "currency", currency.ToUpperInvariant() },
                    { "description", finalDescription },
                    { "customer", customer },
                    { "subscription_registration", subReg },
                    { "receipt", "rcpt_" + Guid.NewGuid().ToString("N")[..10] },
                    { "notes", notes },
                    { "callback_url", successUrl },      
                    { "callback_method", "get" }         
                };

                var invoice = client.Invoice.CreateRegistrationLink(req);
                return Task.FromResult(new GatewayCheckoutResult(true, invoice["short_url"].ToString(), invoice["id"].ToString(), null));
            }
            else
            {
                var req = new Dictionary<string, object>
                {
                    { "amount", amountPaise },
                    { "currency", currency.ToUpperInvariant() },
                    { "description", finalDescription },
                    { "customer", customer },
                    { "notes", notes },
                    { "callback_url", successUrl },
                    { "callback_method", "get" }
                };

                var link = client.PaymentLink.Create(req);
                return Task.FromResult(new GatewayCheckoutResult(true, link["short_url"].ToString(), link["id"].ToString(), null));
            }
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
            
            if (eventType != "payment.captured")
            {
                return Task.FromResult(new GatewayWebhookParsedResult(true, eventType ?? "", "", 0, "", null, new(), 0, 0, 0, 1, "", null));
            }

            var paymentEntity = doc.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            
            var eventIdHeaderKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Razorpay-Event-Id", StringComparison.OrdinalIgnoreCase));
            var eventId = !string.IsNullOrEmpty(eventIdHeaderKey) ? headers[eventIdHeaderKey] : paymentEntity.GetProperty("id").GetString();

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
            var currency = paymentEntity.TryGetProperty("currency", out var cur) ? cur.GetString() : "MYR";

            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_COMPLETED",
                EventId: eventId ?? Guid.NewGuid().ToString(),
                AmountPaid: amount,
                Currency: currency ?? "MYR",
                GatewayTransactionId: paymentEntity.GetProperty("id").GetString(),
                Metadata: meta,
                GatewayFee: fee,
                TaxAmount: tax,
                NetAmount: netAmount,
                FxRate: 1m,
                BaseCurrency: currency ?? "MYR",
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
        string description, string receipt, Guid tenantId, Guid? dunningCampaignId = null)
    {
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

            var orderReq = new Dictionary<string, object>
            {
                { "amount", (int)(amount * 100) },
                { "currency", currency.ToUpperInvariant() },
                { "receipt", receipt },
                { "payment_capture", true },
                { "notes", notes }
            };
            var order = client.Order.Create(orderReq);

            var payReq = new Dictionary<string, object>
            {
                { "email", "billing@lazuar.com" },
                { "contact", "0000000000" },
                { "amount", (int)(amount * 100) },
                { "currency", currency.ToUpperInvariant() },
                { "order_id", order["id"].ToString() },
                { "customer_id", customerId },
                { "token", tokenId },
                { "recurring", true },
                { "description", description },
                { "notes", notes }
            };
            
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
            var refundReq = new Dictionary<string, object> { { "amount", (int)(amount * 100) } };
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

    private static string ExtractName(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Customer";
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : "Customer";
    }
}
