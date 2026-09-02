using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Checkouts;

/// <summary>
/// Issue 002 (issues/001): checkout status transitions are compare-and-set at the database.
/// <see cref="CheckoutRow"/> carries no EF concurrency token, so the previous blind tracked
/// writes were last-writer-wins — the TTL expiry sweep could overwrite a just-committed
/// "paid" to "expired" (freeing capacity for a delivered order and arming the late-pay
/// refund path against a fulfilled checkout), and the fulfiller could symmetrically stamp
/// "paid" over a committed "expired". Every transition now issues
/// <c>UPDATE checkouts SET Status = @to WHERE Id = @id AND Status = 'open'</c> and treats
/// 0 affected rows as "another writer moved it — not ours to change".
/// </summary>
public static class CheckoutTransitions
{
    public static bool IsNpgsql(PayDbContext db) =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

    /// <summary>
    /// CAS transition from "open" to <paramref name="status"/>. On success the tracked entity
    /// is synced and backdated so the caller's SaveChanges cannot re-issue a blind status
    /// write over whatever the row becomes next. Returns false when another writer already
    /// moved the row off "open". On the InMemory test provider there is no cross-request
    /// concurrency to guard, so the tracked write is the transition.
    /// </summary>
    public static Task<bool> TryLeaveOpenAsync(
        PayDbContext db, CheckoutRow checkout, string status, CancellationToken ct) =>
        TryTransitionAsync(db, checkout, from: "open", to: status, ct);

    /// <summary>
    /// CAS transition from <paramref name="from"/> to <paramref name="to"/> — same tracker
    /// hygiene as <see cref="TryLeaveOpenAsync"/>. Issue 004 uses the failed→open direction
    /// to re-open a past_due subscription's checkout for retry.
    /// </summary>
    public static async Task<bool> TryTransitionAsync(
        PayDbContext db, CheckoutRow checkout, string from, string to, CancellationToken ct)
    {
        if (!IsNpgsql(db))
        {
            if (!string.Equals(checkout.Status, from, StringComparison.Ordinal))
            {
                return false;
            }

            checkout.Status = to;
            return true;
        }

        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE public.checkouts SET \"Status\" = {to} WHERE \"Id\" = {checkout.Id} AND \"Status\" = {from}",
            ct);
        if (affected == 0)
        {
            return false;
        }

        // Sync the tracker to the committed state, then backdate the original value so
        // SaveChanges sees no modification for Status and cannot blindly rewrite it.
        checkout.Status = to;
        db.Entry(checkout).Property(x => x.Status).OriginalValue = to;
        return true;
    }
}
