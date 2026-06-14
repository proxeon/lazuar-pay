using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Aggregates;

public class CommunityReminderSchedule : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; } // Settable via EF / PlatformDbContext

    public Guid? PlanId { get; private set; }

    // Points to MessageTemplateEntity.Id in the Messaging module.
    // Stored as a raw Guid, as cross-schema foreign keys are forbidden.
    public Guid TemplateId { get; private set; }

    public string Channel { get; private set; }

    // Negative = days before due, 0 = on due date, Positive = days after due.
    public int DaysRelativeToDue { get; private set; }

    // Format "HH:mm" (UTC)
    public string TimeOfDay { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private CommunityReminderSchedule() { } // For EF Core
#pragma warning restore CS8618

    public CommunityReminderSchedule(
        Guid organizationId,
        Guid? planId,
        Guid templateId,
        string channel,
        int daysRelativeToDue,
        string timeOfDay,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel cannot be empty.", nameof(channel));
        if (string.IsNullOrWhiteSpace(timeOfDay))
            throw new ArgumentException("Time of day cannot be empty.", nameof(timeOfDay));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        PlanId = planId;
        TemplateId = templateId;
        Channel = channel.ToUpperInvariant();
        DaysRelativeToDue = daysRelativeToDue;
        TimeOfDay = timeOfDay;
        IsEnabled = isEnabled;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        Guid? planId,
        Guid templateId,
        string channel,
        int daysRelativeToDue,
        string timeOfDay,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel cannot be empty.", nameof(channel));
        if (string.IsNullOrWhiteSpace(timeOfDay))
            throw new ArgumentException("Time of day cannot be empty.", nameof(timeOfDay));

        PlanId = planId;
        TemplateId = templateId;
        Channel = channel.ToUpperInvariant();
        DaysRelativeToDue = daysRelativeToDue;
        TimeOfDay = timeOfDay;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
