using System;
using System.Collections.Generic;

namespace Modules.Commerce.Application;

/// <summary>
/// Capture campaign id before <c>ClearDunning</c>. Metadata wins; Billplz-stripped payloads fall back to the live assignment.
/// </summary>
public static class DunningRecoveryAttribution
{
    public static Guid? ResolveCampaignId(
        bool wasInArrears,
        Guid? currentCampaignId,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (!wasInArrears)
        {
            return null;
        }

        if (metadata is not null
            && metadata.TryGetValue("dunning_campaign_id", out var raw)
            && Guid.TryParse(raw, out var fromMetadata))
        {
            return fromMetadata;
        }

        return currentCampaignId;
    }
}
