using System;

namespace Modules.Commerce.Application;

/// <summary>
/// Committed monthly equivalent on ACTIVE rows using the subscription snapshot (LP-161).
/// Not cash. PAST_DUE and collection-paused rows are excluded.
/// </summary>
public static class CommerceMrr
{
    public static decimal MonthlyEquivalent(
        string status,
        DateTime? collectionPausedUntil,
        DateTime utcNow,
        string? interval,
        decimal unitAmount,
        int quantity,
        decimal fallbackUnit = 0m,
        bool hasSnapshot = false)
    {
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (collectionPausedUntil.HasValue && collectionPausedUntil.Value > utcNow)
        {
            return 0m;
        }

        if (interval is not ("mo" or "yr"))
        {
            return 0m;
        }

        var unit = SubscriptionBillingAmount.ResolveUnit(hasSnapshot, unitAmount, fallbackUnit);
        var line = unit * Math.Max(1, quantity);
        return interval == "yr" ? line / 12m : line;
    }

    /// <summary>Stats SQL: COALESCE(NULLIF(TRIM(BillingInterval), ''), product.Interval).</summary>
    public static string CoalesceInterval(string? billingInterval, string productInterval) =>
        string.IsNullOrWhiteSpace(billingInterval) ? productInterval : billingInterval.Trim();

    public static bool ContributesToMrr(
        string status,
        DateTime? collectionPausedUntil,
        DateTime utcNow,
        string? interval)
    {
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (collectionPausedUntil.HasValue && collectionPausedUntil.Value > utcNow)
        {
            return false;
        }

        return interval is "mo" or "yr";
    }

    public static double Arpu(decimal mrr, int contributingSeats) =>
        contributingSeats > 0 ? (double)(mrr / contributingSeats) : 0;
}
