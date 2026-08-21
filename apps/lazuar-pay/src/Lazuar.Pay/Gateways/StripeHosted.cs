using Lazuar.Pay.Data;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Gateways;

public sealed class StripeHosted(PayDbContext db, SecretBox box)
{
    public const string Provider = "stripe";

    public async Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == Provider, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = (long)Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = checkout.Id,
            SuccessUrl = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            CancelUrl = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
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
        }, cancellationToken: ct);
        return session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
    }
}
