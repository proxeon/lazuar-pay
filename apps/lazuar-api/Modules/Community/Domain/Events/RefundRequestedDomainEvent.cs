using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record RefundRequestedDomainEvent(Guid SubscriptionId, Guid PaymentRecordId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
