using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Aggregates;

public class ReminderSchedule : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid? ProductId { get; private set; }
    public Guid TemplateId { get; private set; }
    public string Channel { get; private set; }
    public int DaysRelativeToDue { get; private set; }
    public string TimeOfDay { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private ReminderSchedule() { }
#pragma warning restore CS8618

    public ReminderSchedule(
        Guid organizationId,
        Guid? productId,
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
        ProductId = productId;
        TemplateId = templateId;
        Channel = channel.ToUpperInvariant();
        DaysRelativeToDue = daysRelativeToDue;
        TimeOfDay = timeOfDay;
        IsEnabled = isEnabled;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        Guid? productId,
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

        ProductId = productId;
        TemplateId = templateId;
        Channel = channel.ToUpperInvariant();
        DaysRelativeToDue = daysRelativeToDue;
        TimeOfDay = timeOfDay;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
