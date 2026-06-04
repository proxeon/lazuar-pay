using System;
using BuildingBlocks.Domain;

namespace Modules.Messaging.Domain;

public class AutomationQueue : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public Guid? AutomationRuleId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string? TriggerType { get; private set; }

    public Guid? ClientProfileId { get; private set; }
    public Guid? BookingId { get; private set; }
    public string? StepName { get; private set; }

    public DateTime ScheduledAt { get; private set; }
    public string Status { get; set; } = "PENDING";

    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private AutomationQueue() { }
#pragma warning restore CS8618

    public AutomationQueue(
        Guid organizationId, Guid? automationRuleId, Guid? templateId, 
        string? triggerType, Guid? clientProfileId, Guid? bookingId, 
        string? stepName, DateTime scheduledAt)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        AutomationRuleId = automationRuleId;
        TemplateId = templateId;
        TriggerType = triggerType;
        ClientProfileId = clientProfileId;
        BookingId = bookingId;
        StepName = stepName;
        ScheduledAt = scheduledAt;
        CreatedAt = DateTime.UtcNow;
    }
}
