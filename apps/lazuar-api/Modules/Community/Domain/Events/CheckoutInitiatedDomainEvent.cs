using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record CheckoutInitiatedDomainEvent(
    Guid SubscriptionId, 
    Guid OrganizationId, 
    Guid ClientProfileId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
