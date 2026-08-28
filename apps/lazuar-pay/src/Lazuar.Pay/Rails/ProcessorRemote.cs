using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Rails;

/// <summary>
/// Expire unpaid hosted sessions and refund late PSP captures. Named rails, not
/// <c>IEnumerable&lt;IHostedRail&gt;</c>. Failures are swallowed so occupancy still expires locally.
/// </summary>
public sealed class ProcessorRemote(
    PayDbContext db,
    SecretBox box,
    IHttpClientFactory http)
{
    public async Task ExpireAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (checkout.Status != "expired")
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(checkout.ProviderSessionId)
            || !PayProviders.TryNormalize(checkout.Provider, out var provider)
            || PayProviders.IsTest(provider))
        {
            return;
        }

        try
        {
            if (provider == PayProviders.Stripe)
            {
                await ExpireStripeAsync(checkout, ct);
            }
            else if (provider == PayProviders.Chip)
            {
                await ChipPostAsync(checkout, "cancel/", null, ct);
            }
        }
        catch (Exception)
        {
            // Local expire already persisted. Processor cancel is best-effort.
        }
    }

    public async Task RefundLateAsync(CheckoutRow checkout, string? providerRef, long? amountMinor, CancellationToken ct)
    {
        if (!PayProviders.TryNormalize(checkout.Provider, out var provider))
        {
            return;
        }

        if (PayProviders.IsTest(provider))
        {
            return;
        }

        try
        {
            if (provider == PayProviders.Stripe)
            {
                await RefundStripeAsync(checkout, providerRef, amountMinor, ct);
            }
            else if (provider == PayProviders.Chip)
            {
                object? body = amountMinor is long minor ? new { amount = minor } : null;
                await ChipPostAsync(checkout, "refund/", body, ct);
            }
        }
        catch (Exception)
        {
            // Caller already refused fulfill. Cash at the processor is an ops follow-up.
        }
    }

    public async Task RefundChargeAsync(ChargeRow charge, CheckoutRow checkout, decimal amount, string refundId, CancellationToken ct)
    {
        if (!PayProviders.TryNormalize(charge.Provider, out var provider))
        {
            throw new InvalidOperationException("unknown provider");
        }

        if (PayProviders.IsTest(provider))
        {
            return;
        }

        var minor = MoneyMath.ToMinor(amount);
        if (provider == PayProviders.Stripe)
        {
            await RefundStripeAsync(checkout, charge.ProviderRef, minor, ct, refundId);
            return;
        }

        if (provider == PayProviders.Chip)
        {
            await ChipPostAsync(checkout, "refund/", new { amount = minor }, ct);
            return;
        }

        if (PayProviders.IsSolana(provider))
        {
            throw new InvalidOperationException("refund not supported on this rail");
        }

        throw new InvalidOperationException("refund not supported on this rail");
    }

    async Task ExpireStripeAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var secret = await StripeSecretAsync(checkout.OrgId, ct);
        if (secret is null || string.IsNullOrWhiteSpace(checkout.ProviderSessionId))
        {
            return;
        }

        var service = new SessionService(new StripeClient(secret));
        await service.ExpireAsync(checkout.ProviderSessionId, cancellationToken: ct);
    }

    async Task RefundStripeAsync(CheckoutRow checkout, string? providerRef, long? amountMinor, CancellationToken ct, string? refundId = null)
    {
        var secret = await StripeSecretAsync(checkout.OrgId, ct);
        if (secret is null)
        {
            return;
        }

        var client = new StripeClient(secret);
        var sessionId = !string.IsNullOrWhiteSpace(checkout.ProviderSessionId)
            ? checkout.ProviderSessionId
            : providerRef;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        string? paymentIntent = null;
        if (sessionId.StartsWith("pi_", StringComparison.Ordinal))
        {
            paymentIntent = sessionId;
        }
        else
        {
            var session = await new SessionService(client).GetAsync(sessionId, cancellationToken: ct);
            paymentIntent = session.PaymentIntentId;
        }

        if (string.IsNullOrWhiteSpace(paymentIntent))
        {
            return;
        }

        var options = new RefundCreateOptions { PaymentIntent = paymentIntent };
        if (amountMinor is > 0)
        {
            options.Amount = amountMinor;
        }

        var req = string.IsNullOrWhiteSpace(refundId)
            ? new RequestOptions()
            : new RequestOptions { IdempotencyKey = "lazuar-refund:" + refundId };
        await new RefundService(client).CreateAsync(options, req, ct);
    }

    async Task ChipPostAsync(CheckoutRow checkout, string action, object? body, CancellationToken ct)
    {
        var purchaseId = checkout.ProviderSessionId;
        if (string.IsNullOrWhiteSpace(purchaseId))
        {
            return;
        }

        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Chip, ct);
        if (cred is null)
        {
            return;
        }

        var client = http.CreateClient("chip");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Rails.Chip.ChipHosted.ApiBase + "purchases/" + Uri.EscapeDataString(purchaseId) + "/" + action);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", box.Unprotect(cred.Ciphertext));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("CHIP " + action.Trim('/') + " rejected");
        }
    }

    async Task<string?> StripeSecretAsync(string orgId, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == PayProviders.Stripe, ct);
        if (cred is null)
        {
            return null;
        }

        return box.Unprotect(cred.Ciphertext);
    }
}
