using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Gateways;

public sealed class StripeHosted(PayDbContext db, SecretBox box, IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public string Provider => PayProviders.Stripe;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Stripe, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = MoneyMath.ToMinor(checkout.Amount);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = checkout.Id,
            SuccessUrl = CheckoutUrls.Success(checkout, config, env),
            CancelUrl = CheckoutUrls.Cancel(checkout, config, env),
            Metadata = new Dictionary<string, string> { ["checkout_id"] = checkout.Id, ["org_id"] = checkout.OrgId },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = checkout.Currency.ToLowerInvariant(),
                        UnitAmount = cents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Pay" }
                    }
                }
            ]
        }, new RequestOptions { IdempotencyKey = "lazuar-checkout:" + checkout.Id }, cancellationToken: ct);
        var url = session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
        return new HostedSession(url, session.Id);
    }
}
