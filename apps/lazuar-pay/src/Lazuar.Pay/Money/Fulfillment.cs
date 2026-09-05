using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

public interface IFulfillPaid
{
    Task<FulfillOutcome> FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct);
}

/// <summary>
/// What the fulfill attempt did. PendingLateRefundId marks an over-capacity late capture whose
/// refund row was booked <c>pending</c> inside the transaction — the caller must settle it via
/// ProcessorRemote only AFTER its own transaction commits, using this id as the Stripe
/// idempotency key.
/// </summary>
public sealed record FulfillOutcome(bool Fulfilled, string? PendingLateRefundId = null, long? LateRefundAmountMinor = null);

public sealed class Fulfillment(PayDbContext db) : IFulfillPaid
{
    static readonly KeyedGates CheckoutGates = new();

    public Task<FulfillOutcome> FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct) =>
        CheckoutGates.RunAsync(checkoutId, () => FulfillPaidCoreAsync(checkoutId, provider, providerRef, ct), ct);

    async Task<FulfillOutcome> FulfillPaidCoreAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null)
        {
            return new FulfillOutcome(false);
        }

        if (checkout.Amount <= 0)
        {
            return new FulfillOutcome(false);
        }

        if (checkout.Status != "open")
        {
            return new FulfillOutcome(false);
        }

        var settings = await db.OrgSettings.FindAsync([checkout.OrgId], ct);
        if (settings?.ChargesPaused == true)
        {
            throw new ChargesPausedException();
        }

        if (checkout.PaymentLinkId is not null)
        {
            var link = await db.PaymentLinks.FirstOrDefaultAsync(x => x.Id == checkout.PaymentLinkId, ct);
            if (link is not null)
            {
                // Issue 008: serialize the capacity check against minting and other fulfillers
                // on the parent link row (FOR UPDATE), exactly as MintOrResume does. The old
                // unlocked count let two concurrent late captures on a full link both read
                // paid = max-1 and both fulfill, exceeding MaxPayers with no refund for the
                // excess. The in-process CheckoutGates only serialize per-checkout, not
                // per-link, and only within one process.
                await PaymentLinkOccupancy.LockParentAsync(db, link.Id, ct);
                var paid = await db.Checkouts.CountAsync(
                    x => x.PaymentLinkId == link.Id && x.Status == "paid",
                    ct);
                if (PaymentLinkOccupancy.IsFull(link.MaxPayers, paid))
                {
                    // Over capacity: money already arrived. Book the refund pending NOW (same
                    // transaction as the expiry) and let the caller settle it after commit with
                    // this row id as the idempotency key — a fresh Guid per attempt previously
                    // let a retry move money twice. Rails with no refund API keep the row
                    // pending as the ops marker.
                    await CheckoutTransitions.TryLeaveOpenAsync(db, checkout, "expired", ct);
                    await OutboundWebhookEnqueue.TryAddAsync(
                        db,
                        checkout.OrgId,
                        "expired:" + checkout.Id,
                        PayWebhookEnvelope.Expired,
                        new { checkout_id = checkout.Id, payment_link_id = checkout.PaymentLinkId, reason = "over_capacity" },
                        ct);
                    return await BookLateRefundAsync(checkout, provider, providerRef, ct);
                }
            }
        }

        // Issue 002: claim the checkout with a compare-and-set off "open". The previous blind
        // "paid" write could land over a concurrently committed "expired" (TTL sweep / charges
        // paused), turning a late capture into a fulfilled order with a spurious expired
        // webhook behind it.
        if (!await CheckoutTransitions.TryLeaveOpenAsync(db, checkout, "paid", ct))
        {
            // Another writer moved the row between our read and here. If it was fulfilled,
            // this delivery is a duplicate; if it expired/failed, the money arrived late and
            // follows the late-pay refund route instead of a forced fulfillment.
            // (Detach only this entity — the caller's tracked webhook-event row must survive
            // so the dedupe insert still commits with the caller's transaction.)
            db.Entry(checkout).State = EntityState.Detached;
            var current = await db.Checkouts.AsNoTracking()
                .Where(x => x.Id == checkout.Id)
                .Select(x => x.Status)
                .FirstAsync(ct);
            if (current is "expired" or "failed")
            {
                return await BookLateRefundAsync(checkout, provider, providerRef, ct);
            }

            return new FulfillOutcome(false);
        }
        var chargeId = Guid.NewGuid().ToString("N");
        db.Charges.Add(new ChargeRow
        {
            Id = chargeId,
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Provider = provider,
            ProviderRef = providerRef,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            Status = "paid"
        });

        if (!string.IsNullOrWhiteSpace(checkout.PayerEmail) || !string.IsNullOrWhiteSpace(checkout.PayerName))
        {
            db.Payers.Add(new PayerRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = checkout.OrgId,
                Email = checkout.PayerEmail,
                Name = checkout.PayerName
            });
        }

        // plans/031/01 (Option A): no subscription activation here — recurring billing is
        // not offered, every checkout is one_off, and no code path mints subscription rows.

        var entryId = Guid.NewGuid().ToString("N");
        db.JournalEntries.Add(new JournalEntryRow
        {
            Id = entryId,
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Currency = checkout.Currency,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "cash",
            Dc = "D",
            Amount = checkout.Amount
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "revenue",
            Dc = "C",
            Amount = checkout.Amount
        });

        var year = MalaysiaTime.Year(DateTimeOffset.UtcNow);
        var number = await DocumentNumbers.AllocateAsync(db, checkout.OrgId, "RCPT", year, ct);
        db.Documents.Add(new DocumentRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Number = number,
            Title = "Official Receipt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            Action = "checkout.paid",
            At = DateTimeOffset.UtcNow
        });

        await OutboundWebhookEnqueue.TryAddAsync(
            db,
            checkout.OrgId,
            chargeId,
            PayWebhookEnvelope.Completed,
            new
            {
                checkout_id = checkout.Id,
                charge_id = chargeId,
                amount = checkout.Amount,
                currency = checkout.Currency,
                provider,
                provider_ref = providerRef,
                number,
                payer_name = checkout.PayerName
            },
            ct);

        // Deliberately no catch: a SaveChanges failure must unwind the caller's transaction,
        // which holds the PSP event dedupe row. Swallowing here acked the webhook while the
        // charge, journal, and receipt silently never landed — a real payment acknowledged lost.
        // The in-process gate plus the unique charges.CheckoutId index are the dupes guard;
        // receipt numbering is atomic (DocumentNumbers), so a DbUpdateException here means the
        // checkout was already fulfilled concurrently and the caller answers "duplicate".
        await db.SaveChangesAsync(ct);
        return new FulfillOutcome(true);
    }

    /// <summary>
    /// Book a pending late_pay refund for money that arrived after the checkout left "open" —
    /// over capacity (issue 008), or expired/failed concurrently with fulfillment (issue 002).
    /// The caller must settle the returned refund id via ProcessorRemote only after its own
    /// transaction commits; rails with no refund API leave the row pending as the ops marker.
    /// </summary>
    async Task<FulfillOutcome> BookLateRefundAsync(
        CheckoutRow checkout, string provider, string? providerRef, CancellationToken ct)
    {
        var refundId = Guid.NewGuid().ToString("N");
        db.Refunds.Add(new RefundRow
        {
            Id = refundId,
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            Status = "pending",
            Provider = provider,
            ProviderRef = providerRef,
            Reason = "late_pay",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return new FulfillOutcome(false, refundId, MoneyMath.ToMinor(checkout.Amount));
    }
}

public sealed class ChargesPausedException() : Exception("Org charges are paused");

public static class MalaysiaTime
{
    public static int Year(DateTimeOffset utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        return TimeZoneInfo.ConvertTime(utc, zone).Year;
    }
}
