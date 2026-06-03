using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Entities;

/// <summary>
/// Child entity of CommunitySubscription.
/// Used to guarantee idempotency for background reminder jobs.
/// </summary>
public class ReminderDispatchLog : Entity
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public DateTime TargetRenewalDate { get; private set; }
    public DateTime DispatchedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private ReminderDispatchLog() { } // For EF Core
#pragma warning restore CS8618

    internal ReminderDispatchLog(Guid subscriptionId, Guid scheduleId, DateTime targetRenewalDate)
    {
        Id = Guid.CreateVersion7();
        SubscriptionId = subscriptionId;
        ScheduleId = scheduleId;
        TargetRenewalDate = targetRenewalDate;
        DispatchedAt = DateTime.UtcNow;
    }
}
