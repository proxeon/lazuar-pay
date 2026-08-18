using System;

namespace Modules.Billing.Contracts;

/// <summary>
/// Shared MYT clock for document series years and B2C consolidation months.
/// Linux: Asia/Kuala_Lumpur. Windows: Singapore Standard Time.
/// </summary>
public static class MalaysiaTime
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    public static DateTime ToMyt(DateTime utc)
    {
        var instant = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(instant, Zone);
    }

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
    }
}
