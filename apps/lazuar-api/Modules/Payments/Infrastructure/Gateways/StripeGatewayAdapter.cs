// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
using Microsoft.Extensions.Logging;
using Modules.Payments.Application;
using Modules.Payments.Application.Ports;
using Stripe;
using Stripe.Checkout;

namespace Modules.Payments.Infrastructure.Gateways;

public class StripeGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly ILogger<StripeGatewayAdapter> _logger;

    public StripeGatewayAdapter(ILogger<StripeGatewayAdapter> logger)
    {
        _logger = logger;
    }

    public string GatewayType => "STRIPE";

    public async Task<GatewayCheckoutResult> GenerateCheckoutAsync(
        string apiKey, Guid tenantId, decimal amount, string currency,
        string productName, string customerEmail,
        string successUrl, string cancelUrl, Dictionary<string, string> metadata,
        string? merchantId, bool setupFutureUsage = false, int quantity = 1)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new SessionService(client);
            var options = CreateCheckoutSessionOptions(
                tenantId, amount, currency, productName, customerEmail,
                successUrl, cancelUrl, metadata, setupFutureUsage, quantity);

            var session = await service.CreateAsync(options);
            return new GatewayCheckoutResult(true, session.Url, session.Id, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout generation failed for Tenant {TenantId}", tenantId);
            return new GatewayCheckoutResult(false, null, null, ex.Message);
        }
    }

    public async Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string apiKey, string webhookSecret, string rawBody, Dictionary<string, string> headers,
        decimal estimatedFeePercentage = 0, decimal fixedFee = 0, decimal taxRate = 0)
    {
        try
        {
            var signatureHeader = headers.Keys.FirstOrDefault(k => k.Equals("Stripe-Signature", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(signatureHeader) || !headers.TryGetValue(signatureHeader, out var signature))
            {
                return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", "Missing Stripe-Signature header.");
            }

            var stripeEvent = EventUtility.ConstructEvent(rawBody, signature, webhookSecret);

            if (stripeEvent.Type == "checkout.session.completed" || stripeEvent.Type == "payment_intent.succeeded")
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    var amount = (session.AmountTotal ?? 0L) / 100m;
                    var meta = session.Metadata != null ? new Dictionary<string, string>(session.Metadata) : new Dictionary<string, string>();
                    
                    decimal gatewayFee = 0;
                    decimal fxRate = 1;
                    string baseCurrency = session.Currency ?? "myr";
                    decimal taxAmount = (session.TotalDetails?.AmountTax ?? 0L) / 100m;
                    string? customerId = session.CustomerId;
                    string? paymentMethodId = null;

                    if (!string.IsNullOrEmpty(session.PaymentIntentId))
                    {
                        try
                        {
                            var client = new StripeClient(apiKey);
                            var piService = new PaymentIntentService(client);
                            var pi = await piService.GetAsync(session.PaymentIntentId, new PaymentIntentGetOptions
                            {
                                Expand = new List<string> { "latest_charge.balance_transaction", "payment_method" }
                            });
                            
                            paymentMethodId = pi.PaymentMethodId;

                            var charge = pi.LatestCharge as Charge;
                            if (charge?.BalanceTransaction != null)
                            {
                                var bt = charge.BalanceTransaction;
                                gatewayFee = Math.Abs(bt.Fee / 100m);
                                
                                if (bt.ExchangeRate.HasValue)
                                {
                                    fxRate = bt.ExchangeRate.Value;
                                }
                                baseCurrency = bt.Currency ?? baseCurrency;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch Stripe balance transaction for fee extraction.");
                        }
                    }

                    decimal netAmount = amount - gatewayFee;

                    return new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        EventId: stripeEvent.Id,
                        AmountPaid: amount,
                        Currency: session.Currency ?? "myr",
                        GatewayTransactionId: session.PaymentIntentId ?? session.Id,
                        Metadata: meta,
                        GatewayFee: gatewayFee,
                        TaxAmount: taxAmount,
                        NetAmount: netAmount,
                        FxRate: fxRate,
                        BaseCurrency: baseCurrency,
                        Error: null,
                        GatewayCustomerId: customerId,
                        GatewayTokenId: paymentMethodId
                    );
                }
                else if (stripeEvent.Data.Object is PaymentIntent pi)
                {
                    var amount = pi.AmountReceived / 100m;
                    var meta = pi.Metadata != null ? new Dictionary<string, string>(pi.Metadata) : new Dictionary<string, string>();

                    // Mirror checkout.session.completed: expand latest_charge.balance_transaction for real fee.
                    // If expand fails, leave GatewayFee=0 (gross-only) rather than blocking fulfillment.
                    decimal gatewayFee = 0;
                    decimal fxRate = 1;
                    string baseCurrency = pi.Currency ?? "myr";

                    try
                    {
                        var client = new StripeClient(apiKey);
                        var piService = new PaymentIntentService(client);
                        var expanded = await piService.GetAsync(pi.Id, new PaymentIntentGetOptions
                        {
                            Expand = new List<string> { "latest_charge.balance_transaction" }
                        });

                        var charge = expanded.LatestCharge as Charge;
                        if (charge?.BalanceTransaction != null)
                        {
                            var bt = charge.BalanceTransaction;
                            gatewayFee = Math.Abs(bt.Fee / 100m);

                            if (bt.ExchangeRate.HasValue)
                            {
                                fxRate = bt.ExchangeRate.Value;
                            }

                            baseCurrency = bt.Currency ?? baseCurrency;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to expand Stripe PaymentIntent {PaymentIntentId} for fee extraction; GatewayFee=0.", pi.Id);
                        gatewayFee = 0;
                    }

                    decimal netAmount = amount - gatewayFee;

                    return new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        EventId: stripeEvent.Id,
                        AmountPaid: amount,
                        Currency: pi.Currency ?? "myr",
                        GatewayTransactionId: pi.Id,
                        Metadata: meta,
                        GatewayFee: gatewayFee,
                        TaxAmount: 0,
                        NetAmount: netAmount,
                        FxRate: fxRate,
                        BaseCurrency: baseCurrency,
                        Error: null,
                        GatewayCustomerId: pi.CustomerId,
                        GatewayTokenId: pi.PaymentMethodId
                    );
                }
            }

            // AUTO_CHARGE / billing off-session: processing → issuer fail must MarkFailed or a later offset stays blocked.
            if (stripeEvent.Type == "payment_intent.payment_failed" && stripeEvent.Data.Object is PaymentIntent failedPi)
            {
                return MapPaymentIntentPaymentFailed(failedPi, stripeEvent.Id);
            }

            if (stripeEvent.Type == "charge.dispute.created" && stripeEvent.Data.Object is Dispute dispute)
            {
                var meta = new Dictionary<string, string>();
                var amount = dispute.Amount / 100m;

                if (!string.IsNullOrEmpty(dispute.PaymentIntentId))
                {
                    try
                    {
                        var client = new StripeClient(apiKey);
                        var piService = new PaymentIntentService(client);
                        var pi = await piService.GetAsync(dispute.PaymentIntentId);
                        if (pi.Metadata != null) meta = new Dictionary<string, string>(pi.Metadata);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch PaymentIntent for dispute metadata.");
                    }
                }

                return new GatewayWebhookParsedResult(
                    Verified: true,
                    EventType: "DISPUTE_CREATED",
                    EventId: stripeEvent.Id,
                    AmountPaid: amount,
                    Currency: dispute.Currency ?? "myr",
                    GatewayTransactionId: dispute.PaymentIntentId ?? dispute.Id,
                    Metadata: meta,
                    GatewayFee: 0,
                    TaxAmount: 0,
                    NetAmount: amount,
                    FxRate: 1,
                    BaseCurrency: dispute.Currency ?? "myr",
                    Error: null,
                    GatewayCustomerId: null,
                    GatewayTokenId: null
                );
            }

            return new GatewayWebhookParsedResult(true, stripeEvent.Type, stripeEvent.Id, 0, "", null, new(), 0, 0, 0, 1, "", null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook verification failed");
            return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message);
        }
    }

    public async Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, string currency,
        string description, string receipt, Guid tenantId,
        Guid? dunningCampaignId = null, string? idempotencyKey = null,
        Guid? chargeAttemptId = null)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new PaymentIntentService(client);
            var meta = BuildOffSessionMetadata(receipt, tenantId, dunningCampaignId, chargeAttemptId);
            var resolvedKey = ResolveOffSessionIdempotencyKey(chargeAttemptId, idempotencyKey);

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = currency.ToLowerInvariant(),
                Customer = customerId,
                PaymentMethod = tokenId,
                OffSession = true,
                Confirm = true,
                Description = description,
                Metadata = meta
            };
            var intent = await service.CreateAsync(options, CreateOffSessionRequestOptions(resolvedKey));
            return intent.Status == "succeeded" || intent.Status == "processing";
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe off-session charge failed for customer {CustomerId}", customerId);
            var declineCode = ex.StripeError?.DeclineCode ?? ex.StripeError?.Code;
            throw new OffSessionDeclinedException(declineCode, ex.StripeError?.Message ?? ex.Message);
        }
    }

    public async Task<bool> IssueRefundAsync(string apiKey, string transactionId, decimal amount)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new RefundService(client);
            var options = new RefundCreateOptions
            {
                PaymentIntent = transactionId,
                Amount = (long)(amount * 100)
            };
            var refund = await service.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = FormatRefundIdempotencyKey(transactionId, amount) });
            return refund.Status == "succeeded" || refund.Status == "pending";
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for Transaction {TransactionId}", transactionId);
            return false;
        }
    }

    internal static GatewayWebhookParsedResult MapPaymentIntentPaymentFailed(PaymentIntent pi, string eventId)
    {
        var meta = pi.Metadata != null
            ? new Dictionary<string, string>(pi.Metadata)
            : new Dictionary<string, string>();
        var declineCode = pi.LastPaymentError?.DeclineCode;
        if (!string.IsNullOrWhiteSpace(declineCode))
        {
            meta["decline_code"] = declineCode;
        }

        var amount = pi.Amount / 100m;
        var currency = pi.Currency ?? "myr";

        return new GatewayWebhookParsedResult(
            Verified: true,
            EventType: "PAYMENT_FAILED",
            EventId: eventId,
            AmountPaid: amount,
            Currency: currency,
            GatewayTransactionId: pi.Id,
            Metadata: meta,
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: amount,
            FxRate: 1,
            BaseCurrency: currency,
            Error: pi.LastPaymentError?.Message,
            GatewayCustomerId: pi.CustomerId,
            GatewayTokenId: pi.PaymentMethodId);
    }

    internal const string RefundIdempotencyKeyPrefix = "lazuar-refund:";

    internal static string FormatRefundIdempotencyKey(string transactionId, decimal amount)
    {
        var amountMinor = (long)(amount * 100);
        return RefundIdempotencyKeyPrefix + transactionId + ":" + amountMinor;
    }

    internal const string OffSessionIdempotencyKeyPrefix = "lazuar-offsession:";

    internal static string FormatOffSessionIdempotencyKey(Guid chargeAttemptId) =>
        OffSessionIdempotencyKeyPrefix + chargeAttemptId.ToString();

    internal static string? ResolveOffSessionIdempotencyKey(Guid? chargeAttemptId, string? fallbackKey)
    {
        if (chargeAttemptId is { } id && id != Guid.Empty)
        {
            return FormatOffSessionIdempotencyKey(id);
        }

        return string.IsNullOrWhiteSpace(fallbackKey) ? null : fallbackKey;
    }

    internal static Dictionary<string, string> BuildOffSessionMetadata(
        string receipt,
        Guid tenantId,
        Guid? dunningCampaignId,
        Guid? chargeAttemptId)
    {
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

        return meta;
    }

    internal static RequestOptions? CreateOffSessionRequestOptions(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return new RequestOptions { IdempotencyKey = idempotencyKey };
    }

    internal const string CardPaymentMethodType = "card";

    // Wallets (Apple Pay / Google Pay) ride on card. Listing apple_pay/google_pay is invalid.
    // This list replaces Dashboard dynamic PMs for the session; the child PI inherits it.
    internal static void ApplyCardWalletPaymentMethodTypes(SessionCreateOptions options)
    {
        options.PaymentMethodTypes = new List<string> { CardPaymentMethodType };
    }

    /// <summary>
    /// Keep an existing paying <c>tenant_id</c> (platform charges). Stamp the adapter
    /// tenant as <c>platform_tenant_id</c> when it differs so system checkout does not
    /// overwrite the workspace that must be activated.
    /// </summary>
    internal static void ApplyPayingTenantMetadata(Dictionary<string, string> metadata, Guid adapterTenantId)
    {
        var adapterId = adapterTenantId.ToString();
        if (!metadata.TryGetValue("tenant_id", out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            metadata["tenant_id"] = adapterId;
            return;
        }

        if (!string.Equals(existing, adapterId, StringComparison.OrdinalIgnoreCase))
            metadata["platform_tenant_id"] = adapterId;
    }

    internal static SessionCreateOptions CreateCheckoutSessionOptions(
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        bool setupFutureUsage = false,
        int quantity = 1)
    {
        ApplyPayingTenantMetadata(metadata, tenantId);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : null,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmountDecimal = amount * 100,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(productName) ? GatewayCommon.DefaultProductName : productName
                        },
                    },
                    Quantity = quantity,
                }
            },
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        ApplyCardWalletPaymentMethodTypes(options);
        ApplySetupFutureUsage(options, setupFutureUsage);
        return options;
    }

    internal static void ApplySetupFutureUsage(SessionCreateOptions options, bool setupFutureUsage)
    {
        if (!setupFutureUsage)
        {
            return;
        }

        options.PaymentIntentData ??= new SessionPaymentIntentDataOptions();
        options.PaymentIntentData.SetupFutureUsage = "off_session";
        // Without a Customer, setup_future_usage often yields no reusable PM / null session.CustomerId.
        options.CustomerCreation = "always";
    }

    public async Task<string> GenerateCustomerPortalAsync(string apiKey, string customerEmail, string returnUrl)
    {
        var client = new StripeClient(apiKey);
        var customerService = new CustomerService(client);
        var customers = await customerService.ListAsync(new CustomerListOptions { Email = customerEmail, Limit = 1 });
        var customerId = customers.FirstOrDefault()?.Id;
        
        if (string.IsNullOrEmpty(customerId))
        {
            throw new InvalidOperationException("No Stripe customer found for this email address.");
        }

        var portalService = new Stripe.BillingPortal.SessionService(client);
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        });
        
        return session.Url;
    }
}
