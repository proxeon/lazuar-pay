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

    public Task<GatewayWebhookParsedResult> ParseWebhookAsync(
        string webhookSecret, string rawBody, Dictionary<string, string> headers)
    {
        try
        {
            var signatureHeader = headers.Keys.FirstOrDefault(k => k.Equals("Stripe-Signature", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(signatureHeader) || !headers.TryGetValue(signatureHeader, out var signature))
            {
                return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), "Missing Stripe-Signature header."));
            }

            var stripeEvent = EventUtility.ConstructEvent(rawBody, signature, webhookSecret);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    var amount = (session.AmountTotal ?? 0) / 100m;
                    var meta = session.Metadata != null ? new Dictionary<string, string>(session.Metadata) : new Dictionary<string, string>();
                    
                    return Task.FromResult(new GatewayWebhookParsedResult(
                        Verified: true,
                        EventType: "PAYMENT_COMPLETED",
                        EventId: stripeEvent.Id,
                        AmountPaid: amount,
                        Currency: session.Currency ?? "myr",
                        GatewayTransactionId: session.PaymentIntentId ?? session.Id, 
                        Metadata: meta,
                        Error: null
                    ));
                }
            }

            return Task.FromResult(new GatewayWebhookParsedResult(true, stripeEvent.Type, stripeEvent.Id, 0, "", null, new(), null));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook verification failed");
            return Task.FromResult(new GatewayWebhookParsedResult(false, "", "", 0, "", null, new(), ex.Message));
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
