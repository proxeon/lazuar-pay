using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.One;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Lazuar.Pay.Gateways;

internal static class WebhookEndpoints
{
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
    }

    static async Task<IResult> Handle(
        string provider,
        string orgId,
        HttpRequest request,
        PayDbContext db,
        SecretBox box,
        Fulfillment fulfillment,
        CancellationToken ct)
    {
        if (string.Equals(provider, StripeHosted.Provider, StringComparison.OrdinalIgnoreCase) == false)
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return PayErrors.Status(400, "Bad Request", "empty body");
        }

        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == StripeHosted.Provider, ct);
        if (cred is null)
        {
            return PayErrors.Status(400, "Bad Request", "rail not configured");
        }

        request.Headers.TryGetValue("Stripe-Signature", out var sig);
        Event stripeEvent;
        try
        {
            // Webhook signing secret is stored as the BYOK secret for local dogfood
            // when the merchant pastes whsec_…; sk_ keys cannot verify signatures.
            stripeEvent = EventUtility.ConstructEvent(json, sig.ToString(), box.Unprotect(cred.Ciphertext), throwOnApiVersionMismatch: false);
        }
        catch (Exception)
        {
            return PayErrors.Status(400, "Bad Request", "invalid signature");
        }

        if (await db.PspWebhookEvents.FindAsync([orgId, StripeHosted.Provider, stripeEvent.Id], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }

        db.PspWebhookEvents.Add(new PspWebhookEventRow
        {
            OrgId = orgId,
            Provider = StripeHosted.Provider,
            EventId = stripeEvent.Id,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        if (stripeEvent.Type is "checkout.session.completed")
        {
            if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                if (session.Mode == "setup" || (session.AmountTotal is null or 0))
                {
                    return Results.Json(new { ignored = "setup_or_zero" }, OneClient.Json);
                }

                var checkoutId = session.ClientReferenceId ?? session.Metadata?["checkout_id"];
                if (!string.IsNullOrWhiteSpace(checkoutId))
                {
                    await fulfillment.FulfillPaidAsync(checkoutId, StripeHosted.Provider, session.Id, ct);
                }
            }
        }

        return Results.Json(new { ok = true }, OneClient.Json);
    }
}
