using Lazuar.Pay.Hosting;

namespace Lazuar.Pay.Money;

/// <summary>
/// Recurring billing is not offered (plans/031/01, "Option A"): a `mo`/`yr` checkout bills
/// exactly once while the subscriptions row claims otherwise, so the intervals are refused
/// at every entry point instead of half-building a billing engine. The subscriptions table
/// and list endpoint are kept for a future real implementation; legacy rows (if any) read
/// as historical, and failed checkouts stay terminal — a fresh checkout is the retry.
/// </summary>
public static class BillingIntervals
{
    public const string OneOff = "one_off";

    public static IResult? Error(string? interval) =>
        string.IsNullOrWhiteSpace(interval) || interval.Trim() == OneOff
            ? null
            : PayErrors.Status(400, "Bad Request", "interval must be one_off; recurring billing is not offered");
}
