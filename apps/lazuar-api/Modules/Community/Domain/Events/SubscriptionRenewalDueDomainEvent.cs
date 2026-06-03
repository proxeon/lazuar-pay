using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record SubscriptionRenewalDueDomainEvent(
    Guid SubscriptionId, 
    Guid OrganizationId, 
    Guid ClientProfileId, 
    DateTime RenewalDate) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
