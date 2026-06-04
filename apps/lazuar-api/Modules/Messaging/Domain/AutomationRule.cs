using System;
using BuildingBlocks.Domain;

namespace Modules.Messaging.Domain;

public class AutomationRule : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; } = "";
    public string TriggerType { get; private set; } = "";
    public string Channel { get; private set; } = "EMAIL";
    public Guid? TemplateId { get; private set; }
    public int DelayMinutes { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private AutomationRule() { }
#pragma warning restore CS8618

    public AutomationRule(
        Guid organizationId, string name, string triggerType, 
        string channel, Guid? templateId, int delayMinutes, bool isEnabled)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        TriggerType = triggerType;
        Channel = channel;
        TemplateId = templateId;
        DelayMinutes = delayMinutes;
        IsEnabled = isEnabled;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
