using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class DunningStep : Entity
{
    public Guid Id { get; private set; }
    public Guid DunningCampaignId { get; private set; }
    public int DayOffset { get; private set; }
    public Guid TemplateId { get; private set; }
    public string Channel { get; private set; }

#pragma warning disable CS8618
    private DunningStep() { }
#pragma warning restore CS8618

    internal DunningStep(Guid dunningCampaignId, int dayOffset, Guid templateId, string channel)
    {
        Id = Guid.CreateVersion7();
        DunningCampaignId = dunningCampaignId;
        DayOffset = dayOffset;
        TemplateId = templateId;
        Channel = channel.ToUpperInvariant();
    }
}
