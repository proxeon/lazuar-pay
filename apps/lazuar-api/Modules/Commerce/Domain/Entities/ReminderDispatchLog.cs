using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class ReminderDispatchLog : Entity
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public DateTime TargetBillingDate { get; private set; }
    public DateTime DispatchedAt { get; private set; }

#pragma warning disable CS8618
    private ReminderDispatchLog() { }
#pragma warning restore CS8618

    internal ReminderDispatchLog(Guid subscriptionId, Guid scheduleId, DateTime targetBillingDate)
    {
        Id = Guid.CreateVersion7();
        SubscriptionId = subscriptionId;
        ScheduleId = scheduleId;
        TargetBillingDate = targetBillingDate;
        DispatchedAt = DateTime.UtcNow;
    }
}
