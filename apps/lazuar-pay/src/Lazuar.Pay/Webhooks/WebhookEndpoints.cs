using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Money;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Solana;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Webhooks;

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
        IConfiguration config,
        IHostEnvironment env,
        SecretBox box,
        IFulfillPaid fulfillment,
        ProcessorRemote remote,
        CancellationToken ct)
    {
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return PayErrors.Status(400, "Bad Request", "empty body");
        }

        GatewayCredentialRow? cred = null;
        if (PayProviders.IsTest(name))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }
        else
        {
            cred = await db.GatewayCredentials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == name, ct);
            if (cred is null)
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }

        PspParseResult parsed;
        try
        {
            parsed = name switch
            {
                PayProviders.Stripe => StripeWebhook.Parse(raw, request.Headers, cred!, box, config, env),
                PayProviders.Chip => ChipWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Billplz => BillplzWebhook.Parse(raw, cred!, box),
                PayProviders.Xendit => XenditWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Razorpay => RazorpayWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Solana => SolanaWebhook.Parse(raw, request.Headers),
                PayProviders.Test => TestWebhook.Parse(raw, request.Headers, config),
                _ => throw new InvalidOperationException("unknown provider")
            };
        }
        catch (PspVerifyException ex)
        {
            return PayErrors.Status(400, "Bad Request", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("webhook secret", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        if (await db.PspWebhookEvents.FindAsync([orgId, name, parsed.EventId], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }

        if (parsed.Ignored && !parsed.Failed)
        {
            await InsertEventAsync(db, orgId, name, parsed.EventId, ct);
            return Results.Json(new { ignored = parsed.IgnoreReason }, OneClient.Json);
        }

        string? checkoutId = parsed.CheckoutId;
        if (string.IsNullOrWhiteSpace(checkoutId) && !string.IsNullOrWhiteSpace(parsed.HostedSessionId))
        {
            var bySession = await db.Checkouts.FirstOrDefaultAsync(
                x => x.OrgId == orgId && x.Provider == name && x.ProviderSessionId == parsed.HostedSessionId, ct);
            checkoutId = bySession?.Id;
        }

        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            return PayErrors.Status(400, "Bad Request", "checkout not found");
        }

        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null || checkout.OrgId != orgId)
        {
            return PayErrors.Status(400, "Bad Request", "checkout not found");
        }

        if (string.IsNullOrWhiteSpace(checkout.Provider)
            || !string.Equals(checkout.Provider, name, StringComparison.OrdinalIgnoreCase))
        {
            return PayErrors.Status(400, "Bad Request", "provider mismatch");
        }

        var orgSettings = await db.OrgSettings.FindAsync([orgId], ct);
        if (orgSettings?.ChargesPaused == true)
        {
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }

        if (parsed.Failed)
        {
            if (checkout.Status == "paid")
            {
                await InsertEventAsync(db, orgId, name, parsed.EventId, ct);
                return Results.Json(new { ignored = "already_paid" }, OneClient.Json);
            }

            await using var failTx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow
                {
                    OrgId = orgId,
                    Provider = name,
                    EventId = parsed.EventId,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
                if (checkout.Status == "open")
                {
                    checkout.Status = "failed";
                    var sub = await db.Subscriptions.FirstOrDefaultAsync(x => x.CheckoutId == checkout.Id, ct);
                    if (sub is not null)
                    {
                        sub.Status = "past_due";
                        sub.PastDueAt ??= DateTimeOffset.UtcNow;
                        sub.AttemptCount += 1;
                    }

                    await OutboundWebhookEnqueue.TryAddAsync(
                        db,
                        checkout.OrgId,
                        parsed.EventId,
                        PayWebhookEnvelope.Failed,
                        new
                        {
                            checkout_id = checkout.Id,
                            reason = parsed.IgnoreReason ?? "payment_failed",
                            provider = name
                        },
                        ct);
                }

                await db.SaveChangesAsync(ct);
                await failTx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await failTx.RollbackAsync(ct);
                return Results.Ok(new { duplicate = true });
            }

            return Results.Json(new { failed = true }, OneClient.Json);
        }

        if (checkout.Status is "expired" or "failed")
        {
            // Book the refund pending before any money moves. The old flow wrote "succeeded"
            // up front and swallowed RefundLateAsync failures, which left rails with no refund
            // API (billplz/xendit/razorpay) holding permanent fake settled refunds.
            var refundId = Guid.NewGuid().ToString("N");
            var refundRow = new RefundRow
            {
                Id = refundId,
                OrgId = orgId,
                CheckoutId = checkout.Id,
                Amount = checkout.Amount,
                Currency = checkout.Currency,
                Status = "pending",
                Provider = name,
                ProviderRef = parsed.ProviderRef,
                Reason = "late_pay",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await using var lateTx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow
                {
                    OrgId = orgId,
                    Provider = name,
                    EventId = parsed.EventId,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
                db.Refunds.Add(refundRow);
                await db.SaveChangesAsync(ct);
                await lateTx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await lateTx.RollbackAsync(ct);
                return Results.Ok(new { duplicate = true });
            }

            var refunded = await remote.RefundLateAsync(checkout, parsed.ProviderRef, parsed.AmountMinor, refundId, ct);
            if (refunded)
            {
                refundRow.Status = "succeeded";
                await db.SaveChangesAsync(ct);
            }

            return Results.Json(new { refunded }, OneClient.Json);
        }

        if (parsed.Currency is not null
            && !string.Equals(parsed.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PayErrors.Status(400, "Bad Request", "currency mismatch");
        }

        if (parsed.AmountMinor is not null && parsed.AmountMinor.Value != MoneyMath.ToMinor(checkout.Amount))
        {
            return PayErrors.Status(400, "Bad Request", "amount mismatch");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow
            {
                OrgId = orgId,
                Provider = name,
                EventId = parsed.EventId,
                ReceivedAt = DateTimeOffset.UtcNow
            });
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            // "duplicate" only if the concurrent winner actually paid this checkout. Anything
            // else must 5xx so the PSP retries — answering ok on a rolled-back fulfill is how
            // a real payment disappears.
            db.ChangeTracker.Clear();
            var fresh = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == checkout.Id, ct);
            if (fresh?.Status == "paid")
            {
                return Results.Ok(new { duplicate = true });
            }

            return PayErrors.Status(500, "Internal Server Error", "fulfill conflict");
        }
        catch (ChargesPausedException)
        {
            // Dedicated type so this cannot be shadowed by InvalidOperationException → 500.
            await tx.RollbackAsync(ct);
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(500, "Internal Server Error", "fulfill failed");
        }

        return Results.Json(new { ok = true }, OneClient.Json);
    }

    static async Task InsertEventAsync(PayDbContext db, string orgId, string provider, string eventId, CancellationToken ct)
    {
        db.PspWebhookEvents.Add(new PspWebhookEventRow
        {
            OrgId = orgId,
            Provider = provider,
            EventId = eventId,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // duplicate ignore
        }
    }
}
