using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.Pay.Rails;

/// <summary>
/// The refund attempt's determined outcome (plans/031/02). The old bool collapsed three
/// very different situations into "not settled": the processor definitively refused
/// (nothing moved), the outcome is unknown (money may have moved), and the rail has no
/// refund implementation at all. The worker needs the distinction — Rejected can release
/// the row as failed, Unknown schedules a bounded retry, unimplemented rails stay manual.
/// </summary>
public enum RefundLateOutcome
{
    Settled,
    Rejected,
    Unknown,
}

/// <summary>
/// Refund settlement retry schedule (plans/031/02). The stripe idempotency key is pruned
/// after >= 24 h (docs.stripe.com/api/idempotent_requests) — a reused key after pruning
/// creates a NEW refund, so attempts must stay inside the window: the original attempt
/// counts as 1, and worker retries at +1m/+5m/+30m/+2h/+8h keep the last retry at
/// ~10 h 36 m with margin.
/// </summary>
internal static class RefundSchedule
{
    /// <summary>Original settle attempt + 5 worker retries.</summary>
    public const int MaxAttempts = 6;

    /// <summary>Stripe prunes idempotency keys after >= 24 h — never claim rows older.</summary>
    public static readonly TimeSpan ClaimWindow = TimeSpan.FromHours(24);

    public static TimeSpan Backoff(int completedAttempts) => completedAttempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        4 => TimeSpan.FromHours(2),
        _ => TimeSpan.FromHours(8),
    };
}

/// <summary>
/// Expire unpaid hosted sessions and refund late PSP captures. Named rails, not
/// <c>IEnumerable&lt;IHostedRail&gt;</c>. Failures are swallowed so occupancy still expires locally.
/// </summary>
public sealed class ProcessorRemote(
    PayDbContext db,
    SecretBox box,
    IHttpClientFactory http)
{
    // plans/031/02 test seam: the Stripe SDK builds its own HTTP client, so tests replace
    // this to route refund calls through a fake handler (default is the real network).
    internal Func<string, StripeClient> StripeClientFactory { get; set; } = secret => new StripeClient(secret);

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
                await ChipPostAsync(checkout, "cancel/", null, "lazuar-cancel:" + checkout.Id, ct);
            }
        }
        catch (Exception)
        {
            // Local expire already persisted. Processor cancel is best-effort.
        }
    }

    /// <summary>
    /// Best-effort late-capture refund. Callers book the refund row as <c>pending</c> first;
    /// anything but <see cref="RefundLateOutcome.Settled"/> leaves it pending — the capture
    /// is real, so it must never read as settled. Stripe retries are safe inside the
    /// idempotency-key window (same key + same params replay the original result);
    /// CHIP retries are NOT — its refund docs promise no idempotency semantics, so the
    /// settle worker never claims CHIP rows.
    /// </summary>
    public async Task<RefundLateOutcome> RefundLateAsync(CheckoutRow checkout, string? providerRef, long? amountMinor, string refundId, CancellationToken ct)
    {
        if (!PayProviders.TryNormalize(checkout.Provider, out var provider))
        {
            return RefundLateOutcome.Unknown;
        }

        if (PayProviders.IsTest(provider))
        {
            return RefundLateOutcome.Settled;
        }

        try
        {
            if (provider == PayProviders.Stripe)
            {
                try
                {
                    await RefundStripeAsync(checkout, providerRef, amountMinor, ct, refundId);
                    return RefundLateOutcome.Settled;
                }
                catch (StripeException ex) when (ex.StripeError?.Code == "charge_already_refunded")
                {
                    // The refund provably exists (dashboard or a lost original response) —
                    // the row's intent is satisfied, so this reads as settled.
                    return RefundLateOutcome.Settled;
                }
                catch (StripeException ex) when ((int)ex.HttpStatusCode >= 500)
                {
                    // Response lost or server error: the refund may already have executed.
                    return RefundLateOutcome.Unknown;
                }
                catch (StripeException)
                {
                    // Definitive (<500) answer other than already-refunded: nothing moved.
                    return RefundLateOutcome.Rejected;
                }
            }

            if (provider == PayProviders.Chip)
            {
                if (string.IsNullOrWhiteSpace(checkout.ProviderSessionId)
                    || !await db.GatewayCredentials.AsNoTracking()
                        .AnyAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Chip, ct))
                {
                    // No purchase to refund. Honest pending beats a fake settled row.
                    return RefundLateOutcome.Unknown;
                }

                object? body = amountMinor is long minor ? new { amount = minor } : null;
                await ChipPostAsync(checkout, "refund/", body, "lazuar-refund:" + refundId, ct);
                return RefundLateOutcome.Settled;
            }

            // Rails with no refund API: the row is the ops marker, resolved by a human.
            return RefundLateOutcome.Unknown;
        }
        catch (ProcessorRejectedException)
        {
            return RefundLateOutcome.Rejected;
        }
        catch (ProcessorOutcomeUnknownException)
        {
            return RefundLateOutcome.Unknown;
        }
        catch (Exception)
        {
            // Cash at the processor; the pending row is the ops follow-up marker.
            return RefundLateOutcome.Unknown;
        }
    }

    /// <summary>
    /// Settle a pending late-pay refund AFTER the caller's transaction has committed: move
    /// the money, then flip the row to succeeded. Anything but Settled schedules a bounded
    /// worker retry (stripe late_pay rows only) and returns false — the row stays pending.
    /// </summary>
    public async Task<bool> SettlePendingRefundAsync(
        string refundId, CheckoutRow checkout, string? providerRef, long? amountMinor, CancellationToken ct)
    {
        var outcome = await RefundLateAsync(checkout, providerRef, amountMinor, refundId, ct);
        var row = await db.Refunds.FirstAsync(x => x.Id == refundId, ct);
        if (outcome != RefundLateOutcome.Settled)
        {
            // plans/031/02: the original attempt counts as attempt 1; the worker takes over
            // from here (stripe late_pay rows only — other providers stay manual).
            row.AttemptCount = Math.Max(1, row.AttemptCount);
            row.NextAttemptAt = DateTimeOffset.UtcNow.Add(RefundSchedule.Backoff(row.AttemptCount));
            row.LastError = "settle outcome unknown";
            await db.SaveChangesAsync(ct);
            return false;
        }

        row.Status = "succeeded";
        row.NextAttemptAt = null;
        row.LastError = null;
        // plans/031/02: merchants reconcile on webhooks — late-pay settlements were
        // previously invisible to them; emit the same envelope the merchant path does.
        await OutboundWebhookEnqueue.TryAddAsync(
            db,
            row.OrgId,
            row.Id,
            PayWebhookEnvelope.RefundCreated,
            new
            {
                refund_id = row.Id,
                checkout_id = row.CheckoutId,
                charge_id = row.ChargeId,
                amount = row.Amount,
                currency = row.Currency,
                provider = row.Provider,
                number = (string?)null
            },
            ct);
        await db.SaveChangesAsync(ct);
        return true;
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
            try
            {
                await RefundStripeAsync(checkout, charge.ProviderRef, minor, ct, refundId);
            }
            catch (StripeException ex) when ((int)ex.HttpStatusCode >= 500)
            {
                // 5xx from Stripe is ambiguous: the refund may have been created before the
                // server errored. Callers must hold the reservation pending (issue 001) —
                // releasing it on a 5xx is how a retry double-refunded.
                throw new ProcessorOutcomeUnknownException("stripe refund status " + (int)ex.HttpStatusCode);
            }
            catch (StripeException ex)
            {
                // A definitive (<500) Stripe answer: the refund was NOT created.
                throw new ProcessorRejectedException(ex.StripeError?.Message ?? ex.Message);
            }

            return;
        }

        if (provider == PayProviders.Chip)
        {
            if (string.IsNullOrWhiteSpace(checkout.ProviderSessionId))
            {
                throw new InvalidOperationException("chip purchase is missing; nothing to refund");
            }

            var hasCred = await db.GatewayCredentials.AsNoTracking()
                .AnyAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Chip, ct);
            if (!hasCred)
            {
                throw new InvalidOperationException("chip is not configured; nothing to refund with");
            }

            await ChipPostAsync(checkout, "refund/", new { amount = minor }, "lazuar-refund:" + refundId, ct);
            return;
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

        var service = new SessionService(StripeClientFactory(secret));
        await service.ExpireAsync(checkout.ProviderSessionId, cancellationToken: ct);
    }

    async Task RefundStripeAsync(CheckoutRow checkout, string? providerRef, long? amountMinor, CancellationToken ct, string? refundId = null)
    {
        var secret = await StripeSecretAsync(checkout.OrgId, ct);
        if (secret is null)
        {
            // Throwing, not silently returning: the caller books the refund from our outcome,
            // and a silent no-op used to read as a settled refund.
            throw new InvalidOperationException("stripe is not configured; cannot refund");
        }

        var client = StripeClientFactory(secret);
        var sessionId = !string.IsNullOrWhiteSpace(checkout.ProviderSessionId)
            ? checkout.ProviderSessionId
            : providerRef;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("no stripe session or payment intent to refund");
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
            throw new InvalidOperationException("stripe capture has no payment intent to refund");
        }

        var options = new RefundCreateOptions { PaymentIntent = paymentIntent };
        // Issue 001 (issues/003): an amount-less RefundCreate is Stripe's "refund the full
        // remaining amount" — a supplied amount that rounds to zero minor units must never
        // degrade into it. A thrown ProcessorRejectedException keeps the merchant refund
        // row "failed" and the late-pay row "pending"; no money moves on an ambiguous ask.
        // Null stays the late-pay fallback (hand back the whole capture) when the PSP event
        // carried no readable amount.
        if (amountMinor is long minor)
        {
            if (minor <= 0)
            {
                throw new ProcessorRejectedException("refund amount rounds to zero minor units; refusing an amount-less Stripe refund");
            }

            options.Amount = minor;
        }

        var req = string.IsNullOrWhiteSpace(refundId)
            ? new RequestOptions()
            : new RequestOptions { IdempotencyKey = "lazuar-refund:" + refundId };
        await new RefundService(client).CreateAsync(options, req, ct);
    }

    async Task ChipPostAsync(CheckoutRow checkout, string action, object? body, string? idempotencyKey, CancellationToken ct)
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
        // plans/031/02 step 0: CHIP's refund docs do not document idempotency semantics, but
        // the header is harmless and the mint path already sends one. The settle worker
        // treats CHIP as manual until CHIP confirms key behavior on refunds.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            // Issue 001: a CHIP 4xx is a definitive no (nothing executed), but a 5xx — like a
            // lost response or timeout — is ambiguous: the purchase may already have been
            // refunded before the server errored. Callers distinguish the two to decide
            // whether releasing the refund reservation is safe.
            if (code >= 500)
            {
                throw new ProcessorOutcomeUnknownException($"chip {action.Trim('/')} returned {code}");
            }

            throw new ProcessorRejectedException($"chip {action.Trim('/')} rejected ({code})");
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

/// <summary>
/// The processor definitively answered "no" (a &lt;500 response). No money moved, so callers may
/// release the refund reservation (<c>failed</c>) safely. Issue 001.
/// </summary>
public sealed class ProcessorRejectedException(string message) : Exception(message);

/// <summary>
/// The refund's outcome is unknown: the response was lost, timed out, or the processor returned
/// 5xx — the refund may already have executed. Callers must keep the reservation <c>pending</c>
/// (capacity stays reserved) and reconcile before releasing. Issue 001.
/// </summary>
public sealed class ProcessorOutcomeUnknownException(string message) : Exception(message);
