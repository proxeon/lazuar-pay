using System.Collections.Concurrent;
using Lazuar.Pay.Data;
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

public interface IFulfillPaid
{
    Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct);
}

public sealed class Fulfillment(PayDbContext db, ProcessorRemote remote) : IFulfillPaid
{
    static readonly ConcurrentDictionary<string, SemaphoreSlim> CheckoutGates = new(StringComparer.Ordinal);

    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var gate = CheckoutGates.GetOrAdd(checkoutId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await FulfillPaidCoreAsync(checkoutId, provider, providerRef, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    async Task FulfillPaidCoreAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null)
        {
            return;
        }

        if (checkout.Amount <= 0)
        {
            return;
        }

        if (checkout.Status != "open")
        {
            return;
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
                var paid = await db.Checkouts.CountAsync(
                    x => x.PaymentLinkId == link.Id && x.Status == "paid",
                    ct);
                if (PaymentLinkOccupancy.IsFull(link.MaxPayers, paid))
                {
                    checkout.Status = "expired";
                    await OutboundWebhookEnqueue.TryAddAsync(
                        db,
                        checkout.OrgId,
                        "expired:" + checkout.Id,
                        PayWebhookEnvelope.Expired,
                        new { checkout_id = checkout.Id, payment_link_id = checkout.PaymentLinkId, reason = "over_capacity" },
                        ct);
                    await db.SaveChangesAsync(ct);
                    await remote.RefundLateAsync(
                        checkout, providerRef, MoneyMath.ToMinor(checkout.Amount), Guid.NewGuid().ToString("N"), ct);
                    return;
                }
            }
        }

        checkout.Status = "paid";
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

        string? payerId = null;
        if (!string.IsNullOrWhiteSpace(checkout.PayerEmail) || !string.IsNullOrWhiteSpace(checkout.PayerName))
        {
            payerId = Guid.NewGuid().ToString("N");
            db.Payers.Add(new PayerRow
            {
                Id = payerId,
                OrgId = checkout.OrgId,
                Email = checkout.PayerEmail,
                Name = checkout.PayerName
            });
        }

        if (checkout.Interval is "mo" or "yr")
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(x => x.CheckoutId == checkout.Id, ct);
            if (sub is null)
            {
                db.Subscriptions.Add(new SubscriptionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OrgId = checkout.OrgId,
                    CheckoutId = checkout.Id,
                    PayerId = payerId,
                    Status = "active",
                    Interval = checkout.Interval,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                sub.Status = "active";
                sub.PayerId = payerId ?? sub.PayerId;
            }
        }

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
