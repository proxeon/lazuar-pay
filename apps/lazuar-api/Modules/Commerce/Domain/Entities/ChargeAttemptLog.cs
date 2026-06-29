using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class ChargeAttemptLog : Entity
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public DateTime TargetBillingDate { get; private set; }
    public DateTime AttemptedAt { get; private set; }

#pragma warning disable CS8618
    private ChargeAttemptLog() { }
#pragma warning restore CS8618

    public ChargeAttemptLog(Guid subscriptionId, DateTime targetBillingDate)
    {
        Id = Guid.CreateVersion7();
        SubscriptionId = subscriptionId;
        TargetBillingDate = targetBillingDate;
        AttemptedAt = DateTime.UtcNow;
    }
}
