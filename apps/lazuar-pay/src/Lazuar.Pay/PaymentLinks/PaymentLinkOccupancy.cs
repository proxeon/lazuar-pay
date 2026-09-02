using System.Collections.Concurrent;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.PaymentLinks;

/// <summary>
/// A payer is an <c>open</c> reservation or a <c>paid</c> child.
/// Unpaid <c>open</c> rows older than <see cref="ReservationTtl"/> become <c>expired</c>
/// and no longer occupy. Do not invent expiry in the SPA.
/// </summary>
internal static class PaymentLinkOccupancy
{
    public const int DefaultReservationTtlMinutes = 30;

    static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static bool IsOverCapacity(int? maxPayers, int taken) =>
        maxPayers is int max && taken > max;

    public static string MerchantStatus(int? maxPayers, int taken)
    {
        if (IsOverCapacity(maxPayers, taken))
        {
            return "over_capacity";
        }

        return IsFull(maxPayers, taken) ? "full" : "open";
    }

    /// <summary>Buyer remaining is clamped. Merchant list uses <see cref="RemainingUnclamped"/> so over-admit is visible.</summary>
    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;

    public static int? RemainingUnclamped(int? maxPayers, int taken) =>
        maxPayers is int max ? max - taken : null;

    public static TimeSpan ReservationTtl(IConfiguration config)
    {
        var minutes = config.GetValue("Pay:ReservationTtlMinutes", DefaultReservationTtlMinutes);
        return TimeSpan.FromMinutes(Math.Max(1, minutes));
    }

    public static async Task<T> SerializeAsync<T>(string linkId, Func<Task<T>> work, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(linkId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task LockParentAsync(PayDbContext db, string linkId, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM public.payment_links WHERE "Id" = {linkId} FOR UPDATE""",
            ct).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<CheckoutRow>> ExpireStaleAsync(PayDbContext db, string linkId, TimeSpan ttl, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - ttl;
        var stale = await db.Checkouts
            .Where(x => x.PaymentLinkId == linkId && x.Status == "open" && x.CreatedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return await MarkExpiredAsync(db, stale, "ttl", ct).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<CheckoutRow>> ExpireOpenAsync(PayDbContext db, string linkId, CancellationToken ct)
    {
        var open = await db.Checkouts
            .Where(x => x.PaymentLinkId == linkId && x.Status == "open")
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return await MarkExpiredAsync(db, open, "charges_paused", ct).ConfigureAwait(false);
    }

    // internal for OccupancyRaceTests: the SELECT→write race must be driven with a stale row
    // list, which the public Expire*Async entry points re-query away.
    internal static async Task<IReadOnlyList<CheckoutRow>> MarkExpiredAsync(
        PayDbContext db,
        List<CheckoutRow> rows,
        string reason,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var expired = new List<CheckoutRow>(rows.Count);
        foreach (var row in rows)
        {
            // Issue 002: expiry is a compare-and-set off "open" — the previous blind write
            // raced the fulfiller (a sweep that had SELECTed the row while open could
            // overwrite a just-committed "paid", freeing capacity for a delivered order and
            // arming a late-pay refund against a fulfilled checkout). Rows another writer
            // already moved are skipped, and the webhook only fires for rows we expired.
            if (!await CheckoutTransitions.TryLeaveOpenAsync(db, row, "expired", ct).ConfigureAwait(false))
            {
                continue;
            }

            await OutboundWebhookEnqueue.TryAddAsync(
                db,
                row.OrgId,
                "expired:" + row.Id,
                PayWebhookEnvelope.Expired,
                new { checkout_id = row.Id, payment_link_id = row.PaymentLinkId, reason },
                ct).ConfigureAwait(false);
            expired.Add(row);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return expired;
    }
}
