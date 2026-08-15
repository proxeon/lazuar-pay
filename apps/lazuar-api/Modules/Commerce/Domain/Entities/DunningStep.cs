using System;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain;

namespace Modules.Commerce.Domain.Entities;

public class DunningStep : Entity, IDunningStepCopy
{
    public Guid Id { get; private set; }
    public Guid DunningCampaignId { get; private set; }
    public int DayOffset { get; private set; }
    
    /// <summary>EMAIL, WHATSAPP, or AUTO_CHARGE</summary>
    public string ActionType { get; private set; }
    
    public string? Subject { get; private set; }
    public string? EmailBody { get; private set; }
    public string? WhatsAppBody { get; private set; }

#pragma warning disable CS8618
    private DunningStep() { }
#pragma warning restore CS8618

    internal DunningStep(Guid dunningCampaignId, int dayOffset, string actionType, string? subject, string? emailBody, string? whatsAppBody)
    {
        Id = Guid.CreateVersion7();
        DunningCampaignId = dunningCampaignId;
        DayOffset = dayOffset;
        ActionType = actionType.ToUpperInvariant();
        Subject = subject;
        EmailBody = emailBody;
        WhatsAppBody = whatsAppBody;
    }
}
