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

    public static string InferPaymentMethod(string? vaultedTokenId) =>
        string.IsNullOrEmpty(vaultedTokenId) ? Manual : OnlineGateway;

    public static DunningCampaign? FindBest(
        IEnumerable<DunningCampaign> campaigns,
        Guid organizationId,
        Guid productId,
        string paymentMethod) =>
        campaigns.FirstOrDefault(c => c.Matches(organizationId, productId, paymentMethod));
}
