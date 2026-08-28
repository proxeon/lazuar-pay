using System.Collections.Concurrent;
using Lazuar.Pay.Data;
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

    public static async Task ExpireStaleAsync(PayDbContext db, string linkId, TimeSpan ttl, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - ttl;
        var stale = await db.Checkouts
            .Where(x => x.PaymentLinkId == linkId && x.Status == "open" && x.CreatedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (stale.Count == 0)
        {
            return;
        }

        foreach (var row in stale)
        {
            row.Status = "expired";
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public static async Task ExpireOpenAsync(PayDbContext db, string linkId, CancellationToken ct)
    {
        var open = await db.Checkouts
            .Where(x => x.PaymentLinkId == linkId && x.Status == "open")
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (open.Count == 0)
        {
            return;
        }

        foreach (var row in open)
        {
            row.Status = "expired";
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
