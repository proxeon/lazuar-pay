using System;

namespace Modules.Payments.Contracts;

/// <summary>
/// Metadata <c>type</c> values for tenant → Lazuar (platform) checkouts.
/// Plane U = utility credits. Plane S = Hub SaaS fee. Never reuse Commerce
/// <c>saas_subscription</c> (plane G).
/// </summary>
public static class PlatformCheckoutTypes
{
    public const string UtilityCreditTopup = "utility_credit_topup";
    public const string PlatformSaasFee = "platform_saas_fee";

    public static readonly Guid SystemOrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static bool IsPlatformCollected(string? type) =>
        string.Equals(type, UtilityCreditTopup, StringComparison.Ordinal)
        || string.Equals(type, PlatformSaasFee, StringComparison.Ordinal);
}
