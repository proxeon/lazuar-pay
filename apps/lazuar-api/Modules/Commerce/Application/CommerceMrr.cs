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
        decimal fallbackUnit = 0m)
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

        var unit = unitAmount > 0 ? unitAmount : fallbackUnit;
        var line = unit * Math.Max(1, quantity);
        return interval == "yr" ? line / 12m : line;
    }
}
