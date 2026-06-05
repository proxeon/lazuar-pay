using System;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record SubscriptionGracePeriodExtendedDomainEvent(
    Guid SubscriptionId,
    Guid OrganizationId,
    Guid ClientProfileId,
    int ExtendedDays,
    DateTime NewRenewalDate) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
