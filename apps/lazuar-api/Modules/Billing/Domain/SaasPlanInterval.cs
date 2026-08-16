using System;

namespace Modules.Billing.Domain;

public static class SaasPlanInterval
{
    public const string Month = "mo";
    public const string Year = "yr";

    public static bool IsValid(string? interval) =>
        string.Equals(interval, Month, StringComparison.Ordinal)
        || string.Equals(interval, Year, StringComparison.Ordinal);

    public static DateTime AddPeriod(DateTime startUtc, string interval) =>
        string.Equals(interval, Year, StringComparison.Ordinal)
            ? startUtc.AddYears(1)
            : startUtc.AddMonths(1);

    public static string Adjective(string interval) =>
        string.Equals(interval, Year, StringComparison.Ordinal) ? "yearly" : "monthly";

    public static string Noun(string interval) =>
        string.Equals(interval, Year, StringComparison.Ordinal) ? "year" : "month";

    public static string ProductName(string planName, string interval) =>
        $"{planName} ({Adjective(interval)})";

    public static string LineDescription(string planName, string interval) =>
        $"{planName} — {Noun(interval)} software subscription";
}
