using Microsoft.Extensions.Logging;
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
        string? merchantId)
    {
        try
        {
            var client = new StripeClient(apiKey);
            var service = new SessionService(client);
            metadata["tenant_id"] = tenantId.ToString();

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
                                Name = string.IsNullOrWhiteSpace(productName) ? "Lazuar Payment" : productName
                            },
                        },
                        Quantity = 1,
                    }
                },
                Metadata = metadata,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
            };

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

            if (stripeEvent.Type == "checkout.session.completed")
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    var amount = (session.AmountTotal ?? 0L) / 100m;
                    var meta = session.Metadata != null ? new Dictionary<string, string>(session.Metadata) : new Dictionary<string, string>();
                    
                    decimal gatewayFee = 0;
                    decimal fxRate = 1;
                    string baseCurrency = session.Currency ?? "myr";
                    decimal taxAmount = (session.TotalDetails?.AmountTax ?? 0L) / 100m;

                    if (!string.IsNullOrEmpty(session.PaymentIntentId))
                    {
                        try
                        {
                            var client = new StripeClient(apiKey);
                            var piService = new PaymentIntentService(client);
                            var pi = await piService.GetAsync(session.PaymentIntentId, new PaymentIntentGetOptions
                            {
                                Expand = new List<string> { "latest_charge.balance_transaction" }
                            });
                            
                            var charge = pi.LatestCharge as Charge;
                            if (charge?.BalanceTransaction != null)
                            {
                                var bt = charge.BalanceTransaction;
                                
                                // FIX: bt.Fee is a non-nullable long in the Stripe.net SDK. 
                                // Applying ?? 0L causes CS0019. Math.Abs handles the negative fee integer correctly.
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
                        Error: null
                    );
                }
            }
            
            return new GatewayWebhookParsedResult(true, stripeEvent.Type, stripeEvent.Id, 0, "", null, new(), 0, 0, 0, 1, "", null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook verification failed");
            return new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), 0, 0, 0, 1, "", ex.Message);
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
            var refund = await service.CreateAsync(options);
            return refund.Status == "succeeded" || refund.Status == "pending";
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for Transaction {TransactionId}", transactionId);
            return false;
        }
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
