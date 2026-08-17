using System;

namespace BuildingBlocks.Domain;

public static class Iso3166Country
{
    /// <summary>
    /// MyInvois wants ISO 3166-1 alpha-3. Checkout used to default to alpha-2 "MY".
    /// </summary>
    public static string NormalizeToAlpha3(string? code, string fallback = "MYS")
    {
        if (string.IsNullOrWhiteSpace(code))
            return fallback;

        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed switch
        {
            "MY" => "MYS",
            _ => trimmed
        };
    }
}
