using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;
using Microsoft.Extensions.Hosting;
using Stripe;
using Stripe.Checkout;

using Lazuar.Pay.Rails;

using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Stripe;

internal static class StripeWebhook
{
    public static PspParseResult Parse(
        string json,
        IHeaderDictionary headers,
        GatewayCredentialRow cred,
        SecretBox box,
        IConfiguration config,
        IHostEnvironment env)
    {
        var whsec = ResolveSecret(cred, box, config, env);
        if (string.IsNullOrWhiteSpace(whsec))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        headers.TryGetValue("Stripe-Signature", out var sig);
        Event stripeEvent;
        try
        {
            EventUtility.ValidateSignature(json, sig.ToString(), whsec);
            stripeEvent = EventUtility.ConstructEvent(json, sig.ToString(), whsec, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            throw new PspVerifyException("invalid signature");
        }
        catch (Exception)
        {
            throw new PspVerifyException("invalid event");
        }

        if (stripeEvent.Type is not "checkout.session.completed")
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = stripeEvent.Type };
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "no_session" };
        }

        if (session.Mode == "setup" || session.AmountTotal is null or 0)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "setup_or_zero" };
        }

        var checkoutId = session.ClientReferenceId ?? session.Metadata?.GetValueOrDefault("checkout_id");
        // AmountTotal is Stripe cents (minor). Do not ToMinor again.
        if (!MoneyMath.TryNormalizeCurrency(session.Currency, out var currency))
        {
            throw new PspVerifyException("missing currency");
        }

        return new PspParseResult
        {
            EventId = stripeEvent.Id,
            CheckoutId = checkoutId,
            ProviderRef = session.Id,
            AmountMinor = session.AmountTotal,
            Currency = currency
        };
    }

    static string? ResolveSecret(GatewayCredentialRow cred, SecretBox box, IConfiguration config, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
    }
}
