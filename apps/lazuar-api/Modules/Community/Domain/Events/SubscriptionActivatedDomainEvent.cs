using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record SubscriptionActivatedDomainEvent(
    Guid SubscriptionId,
    Guid OrganizationId,
    Guid ClientProfileId,
    bool IsFirstPayment,
    bool IsSilent) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
