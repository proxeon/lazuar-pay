using System;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Aggregates;

public class BroadcastCampaign : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Subject { get; private set; }
    public string EmailBody { get; private set; }
    public string WhatsAppBody { get; private set; }
    public string Channel { get; private set; }
    public Guid? TargetPlanId { get; private set; }
    public string? TargetStatus { get; private set; }
    public bool? TargetIsReminderOnly { get; private set; }
    public string Status { get; private set; }
    public int TotalRecipients { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private BroadcastCampaign() { }
#pragma warning restore CS8618

    public BroadcastCampaign(
        Guid organizationId, 
        string subject, 
        string emailBody, 
        string whatsappBody, 
        string channel,
        Guid? targetPlanId = null,
        string? targetStatus = null,
        bool? targetIsReminderOnly = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Subject = subject;
        EmailBody = emailBody;
        WhatsAppBody = whatsappBody;
        Channel = string.IsNullOrWhiteSpace(channel) ? "ALL" : channel.ToUpperInvariant();
        TargetPlanId = targetPlanId;
        TargetStatus = targetStatus?.ToUpperInvariant();
        TargetIsReminderOnly = targetIsReminderOnly;
        Status = "PENDING";
        TotalRecipients = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        Status = "PROCESSING";
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted(int totalRecipients)
    {
        Status = "COMPLETED";
        TotalRecipients = totalRecipients;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        Status = "FAILED";
        ErrorMessage = error;
        UpdatedAt = DateTime.UtcNow;
    }
}
