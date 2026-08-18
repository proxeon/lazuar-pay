// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs
using System.Text.Json;
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

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(rawBody, signature, webhookSecret);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook verification failed");
                return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe webhook payload could not be constructed");
                return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message)
                    .AsUnusable();
            }

            if (stripeEvent.Type == "checkout.session.completed" || stripeEvent.Type == "payment_intent.succeeded")
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    var amount = (session.AmountTotal ?? 0L) / 100m;
                    var meta = session.Metadata != null ? new Dictionary<string, string>(session.Metadata) : new Dictionary<string, string>();
                    
                    decimal gatewayFee = 0;
                    decimal fxRate = 1;
                    if (!GatewayCommon.TryNormalizeCurrency(session.Currency, out var sessionCurrency))
                    {
                        return new GatewayWebhookParsedResult(
                            false, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", null, new(), 0, 0, 0, 1, "",
                            "Missing session currency; refusing to invent MYR.").AsUnusable();
                    }

                    string baseCurrency = sessionCurrency;
                    decimal taxAmount = (session.TotalDetails?.AmountTax ?? 0L) / 100m;
                    string? customerId = session.CustomerId;
                    string? paymentMethodId = null;
                    var feeKnown = string.IsNullOrEmpty(session.PaymentIntentId);

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
                            feeKnown = ApplyBalanceTransactionFee(pi.LatestCharge as Charge, ref gatewayFee, ref fxRate, ref baseCurrency);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch Stripe balance transaction for fee extraction.");
                            feeKnown = false;
                        }
                    }
                    else
                    {
                        ReadSetupSessionVaultIds(session, ref customerId, ref paymentMethodId);
                        if (string.IsNullOrEmpty(paymentMethodId)
                            && !string.IsNullOrEmpty(session.SetupIntentId))
                        {
                            try
                            {
                                var client = new StripeClient(apiKey);
                                var siService = new SetupIntentService(client);
                                var si = await siService.GetAsync(session.SetupIntentId, new SetupIntentGetOptions
                                {
                                    Expand = new List<string> { "payment_method" }
                                });
                                paymentMethodId = si.PaymentMethodId;
                                customerId ??= si.CustomerId;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to fetch Stripe SetupIntent for payment method extraction.");
                            }
                        }

                        // B04-P20: do not tell Commerce the seat is vaulted when we have no PM.
                        // Verified=false so Stripe retries checkout.session.completed.
                        if (string.IsNullOrWhiteSpace(paymentMethodId))
                        {
                            return RefuseSetupSessionWithoutToken(
                                stripeEvent.Id,
                                sessionCurrency,
                                session.SetupIntentId ?? session.Id,
                                meta,
                                customerId);
                        }
                    }

                    decimal netAmount = amount - gatewayFee;
                    GatewayCommon.StampGatewayFeeStatus(meta, feeKnown);

                    return new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        EventId: stripeEvent.Id,
                        AmountPaid: amount,
                        Currency: sessionCurrency,
                        GatewayTransactionId: session.PaymentIntentId ?? session.SetupIntentId ?? session.Id,
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
                    if (!GatewayCommon.TryNormalizeCurrency(pi.Currency, out var piCurrency))
                    {
                        return new GatewayWebhookParsedResult(
                            false, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", pi.Id, new(), 0, 0, 0, 1, "",
                            "Missing PaymentIntent currency; refusing to invent MYR.").AsUnusable();
                    }

                    string baseCurrency = piCurrency;

                    var feeKnown = false;
                    try
                    {
                        var client = new StripeClient(apiKey);
                        var piService = new PaymentIntentService(client);
                        var expanded = await piService.GetAsync(pi.Id, new PaymentIntentGetOptions
                        {
                            Expand = new List<string> { "latest_charge.balance_transaction" }
                        });

                        feeKnown = ApplyBalanceTransactionFee(expanded.LatestCharge as Charge, ref gatewayFee, ref fxRate, ref baseCurrency);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to expand Stripe PaymentIntent {PaymentIntentId} for fee extraction; GatewayFee=0.", pi.Id);
                        gatewayFee = 0;
                        feeKnown = false;
                    }

                    decimal netAmount = amount - gatewayFee;
                    GatewayCommon.StampGatewayFeeStatus(meta, feeKnown);

                    return new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        EventId: stripeEvent.Id,
                        AmountPaid: amount,
                        Currency: piCurrency,
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

            if (stripeEvent.Type is "charge.dispute.created" or "charge.dispute.closed" or "charge.dispute.updated"
                && stripeEvent.Data.Object is Dispute dispute)
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

                var eventType = stripeEvent.Type == "charge.dispute.created"
                    ? "DISPUTE_CREATED"
                    : "DISPUTE_CLOSED";
                if (stripeEvent.Type == "charge.dispute.updated"
                    && !string.Equals(dispute.Status, "won", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dispute.Status, "lost", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dispute.Status, "warning_closed", StringComparison.OrdinalIgnoreCase))
                {
                    eventType = "DISPUTE_CREATED";
                }

                if (eventType == "DISPUTE_CLOSED")
                {
                    meta["dispute_outcome"] = dispute.Status ?? "closed";
                }

                return new GatewayWebhookParsedResult(
                    Verified: true,
                    EventType: eventType,
                    EventId: stripeEvent.Id,
                    AmountPaid: amount,
                    Currency: GatewayCommon.TryNormalizeCurrency(dispute.Currency, out var disputeCurrency)
                        ? disputeCurrency
                        : "",
                    GatewayTransactionId: dispute.PaymentIntentId ?? dispute.Id,
                    Metadata: meta,
                    GatewayFee: 0,
                    TaxAmount: 0,
                    NetAmount: amount,
                    FxRate: 1,
                    BaseCurrency: GatewayCommon.TryNormalizeCurrency(dispute.Currency, out var disputeBase)
                        ? disputeBase
                        : "",
                    Error: null,
                    GatewayCustomerId: null,
                    GatewayTokenId: null
                );
            }

            if (TryMapRefundCompleted(stripeEvent) is { } refundParsed)
                return refundParsed;

            if (TryMapSetupIntentSucceeded(stripeEvent) is { } setupParsed)
                return setupParsed;

            return new GatewayWebhookParsedResult(true, stripeEvent.Type, stripeEvent.Id, 0, "", null, new(), 0, 0, 0, 1, "", null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Stripe webhook payload unusable after verify");
            return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message)
                .AsUnusable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook mapping failed");
            return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message);
        }
    }

    public async Task<bool> ChargeOffSessionAsync(
        string apiKey, string customerId, string tokenId, decimal amount, string currency,
        string description, string receipt, Guid tenantId,
        Guid? dunningCampaignId = null, string? idempotencyKey = null,
        Guid? chargeAttemptId = null,
        decimal taxAmount = 0,
        string? taxType = null)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new PaymentIntentService(client);
            var meta = BuildOffSessionMetadata(receipt, tenantId, dunningCampaignId, chargeAttemptId, taxAmount, taxType);
            var resolvedKey = ResolveOffSessionIdempotencyKey(chargeAttemptId, idempotencyKey);

            var options = new PaymentIntentCreateOptions
            {
                Amount = GatewayCommon.ToMinorUnits(amount, currency),
                Currency = currency.ToLowerInvariant(),
                Customer = customerId,
                PaymentMethod = tokenId,
                OffSession = true,
                Confirm = true,
                Description = description,
                Metadata = meta
            };
            var intent = await service.CreateAsync(options, CreateOffSessionRequestOptions(resolvedKey));
            return IsOffSessionSucceeded(intent.Status);
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
                Amount = GatewayCommon.ToMinorUnits(amount)
            };
            var refund = await service.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = FormatRefundIdempotencyKey(transactionId, amount) });
            return IsRefundSucceeded(refund.Status);
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
        if (!GatewayCommon.TryNormalizeCurrency(pi.Currency, out var currency))
        {
            return new GatewayWebhookParsedResult(
                false, "PAYMENT_FAILED", eventId, 0, "", pi.Id, meta, 0, 0, 0, 1, "",
                "Missing PaymentIntent currency; refusing to invent MYR.").AsUnusable();
        }

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

    internal const string RefundIdempotencyKeyPrefix = GatewayCommon.RefundIdempotencyKeyPrefix;

    internal static string FormatRefundIdempotencyKey(string transactionId, decimal amount) =>
        GatewayCommon.FormatRefundIdempotencyKey(transactionId, amount);

    internal static bool IsRefundSucceeded(string? status) =>
        string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase);

    internal static GatewayWebhookParsedResult? TryMapRefundCompleted(Event stripeEvent)
    {
        Refund? refund = stripeEvent.Data.Object as Refund;
        var meta = new Dictionary<string, string>();

        if (stripeEvent.Data.Object is Charge charge)
        {
            refund = charge.Refunds?.Data?.FirstOrDefault(r => IsRefundSucceeded(r.Status))
                     ?? charge.Refunds?.Data?.FirstOrDefault();
            if (charge.Metadata is { Count: > 0 })
                meta = new Dictionary<string, string>(charge.Metadata);
        }
        else if (refund?.Metadata is { Count: > 0 })
        {
            meta = new Dictionary<string, string>(refund.Metadata);
        }

        if (refund is null || !IsRefundSucceeded(refund.Status))
            return null;

        if (!GatewayCommon.TryNormalizeCurrency(refund.Currency, out var currency))
        {
            return new GatewayWebhookParsedResult(
                false, "REFUND_COMPLETED", refund.Id, 0, "", null, meta, 0, 0, 0, 1, "",
                "Missing refund currency; refusing to invent MYR.").AsUnusable();
        }

        var paymentIntentId = refund.PaymentIntentId;
        if (string.IsNullOrEmpty(paymentIntentId) && stripeEvent.Data.Object is Charge chargeForPi)
            paymentIntentId = chargeForPi.PaymentIntentId;

        var amount = refund.Amount / 100m;
        return new GatewayWebhookParsedResult(
            Verified: true,
            EventType: "REFUND_COMPLETED",
            EventId: refund.Id,
            AmountPaid: amount,
            Currency: currency,
            GatewayTransactionId: paymentIntentId,
            Metadata: meta,
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: amount,
            FxRate: 1,
            BaseCurrency: currency,
            Error: null);
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
        Guid? chargeAttemptId,
        decimal taxAmount = 0,
        string? taxType = null)
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

        if (taxAmount > 0)
        {
            meta["sst_tax_amount"] = taxAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(taxType))
            {
                meta["sst_tax_type"] = taxType;
            }
        }

        return meta;
    }

    internal static bool IsOffSessionSucceeded(string? status) =>
        string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase);

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
    internal static void ApplyPayingTenantMetadata(Dictionary<string, string> metadata, Guid adapterTenantId) =>
        GatewayCommon.ApplyPayingTenantMetadata(metadata, adapterTenantId);

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

        // $0 + vault: Checkout setup mode (SetupIntent). A $0 PaymentIntent is invalid.
        if (amount == 0 && setupFutureUsage)
        {
            var setupOptions = new SessionCreateOptions
            {
                Mode = "setup",
                Currency = currency.ToLowerInvariant(),
                CustomerEmail = !string.IsNullOrWhiteSpace(customerEmail) ? customerEmail : null,
                Metadata = metadata,
                SetupIntentData = new SessionSetupIntentDataOptions
                {
                    Metadata = metadata
                },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerCreation = "always",
            };
            ApplyCardWalletPaymentMethodTypes(setupOptions);
            return setupOptions;
        }

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
                        UnitAmountDecimal = GatewayCommon.ToMinorUnits(amount, currency),
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

    /// <summary>
    /// Copies Stripe Balance Transaction MDR/FX when the charge is expanded.
    /// Returns false when the fee is unknown so callers can stamp
    /// <c>gateway_fee_status=unknown</c> without blocking fulfillment.
    /// </summary>
    internal static bool ApplyBalanceTransactionFee(
        Charge? charge,
        ref decimal gatewayFee,
        ref decimal fxRate,
        ref string baseCurrency)
    {
        if (charge?.BalanceTransaction is not { } bt)
        {
            return false;
        }

        gatewayFee = Math.Abs(bt.Fee / 100m);
        if (bt.ExchangeRate.HasValue)
        {
            fxRate = bt.ExchangeRate.Value;
        }

        if (GatewayCommon.TryNormalizeCurrency(bt.Currency, out var btCurrency))
        {
            baseCurrency = btCurrency;
        }

        return true;
    }

    /// <summary>
    /// Setup-mode <c>checkout.session.completed</c> has a SetupIntent and no PaymentIntent.
    /// Customer + PM may already be expanded on the event object.
    /// </summary>
    internal static GatewayWebhookParsedResult? TryMapSetupIntentSucceeded(Event stripeEvent)
    {
        if (stripeEvent.Type != "setup_intent.succeeded")
        {
            return null;
        }

        if (stripeEvent.Data.Object is not SetupIntent si)
        {
            return new GatewayWebhookParsedResult(
                false, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", null, new(), 0, 0, 0, 1, "",
                "setup_intent.succeeded without a SetupIntent object.");
        }

        var token = si.PaymentMethodId;
        if (string.IsNullOrWhiteSpace(token))
        {
            return new GatewayWebhookParsedResult(
                false, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", si.Id, new(), 0, 0, 0, 1, "",
                "setup_intent.succeeded missing payment method.",
                si.CustomerId, null);
        }

        var meta = si.Metadata != null ? new Dictionary<string, string>(si.Metadata) : new Dictionary<string, string>();
        return new GatewayWebhookParsedResult(
            true, "PAYMENT_COMPLETED", stripeEvent.Id, 0, "", si.Id, meta, 0, 0, 0, 1, "",
            null, si.CustomerId, token);
    }

    internal static GatewayWebhookParsedResult RefuseSetupSessionWithoutToken(
        string eventId,
        string? currency,
        string? transactionId,
        Dictionary<string, string> meta,
        string? customerId) =>
        new(
            false, "PAYMENT_COMPLETED", eventId, 0, currency ?? "", transactionId, meta, 0, 0, 0, 1, currency ?? "",
            "Setup session missing payment method.",
            customerId, null);

    internal static void ReadSetupSessionVaultIds(
        Session session,
        ref string? customerId,
        ref string? paymentMethodId)
    {
        customerId ??= session.CustomerId ?? session.SetupIntent?.CustomerId;
        paymentMethodId ??= session.SetupIntent?.PaymentMethodId;
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
