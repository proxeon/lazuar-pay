using System;
using System.Collections.Generic;
using System.Linq;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Domain;

/// <summary>
/// Shared campaign targeting: empty product/method lists match all; caller supplies
/// PriorityOrder desc, CreatedAt desc order.
/// </summary>
public static class DunningCampaignMatcher
{
    public const string Manual = "MANUAL";
    public const string OnlineGateway = "ONLINE_GATEWAY";

    /// <summary>
    /// ONLINE_GATEWAY is the collection surface, not "has a vault token" (B03-C26).
    /// Unvaulted Stripe/CHIP/Billplz/Xendit/Razorpay still match ONLINE_GATEWAY campaigns.
    /// </summary>
    public static string InferPaymentMethod(string? vaultedTokenId, string? gatewayName = null)
    {
        var g = (gatewayName ?? "").Trim().ToUpperInvariant();
        if (g is "STRIPE" or "CHIP" or "BILLPLZ" or "XENDIT" or "RAZORPAY")
        {
            return OnlineGateway;
        }

        return string.IsNullOrEmpty(vaultedTokenId) ? Manual : OnlineGateway;
    }

    public static DunningCampaign? FindBest(
        IEnumerable<DunningCampaign> campaigns,
        Guid organizationId,
        Guid productId,
        string paymentMethod) =>
        campaigns.FirstOrDefault(c => c.Matches(organizationId, productId, paymentMethod));
}
