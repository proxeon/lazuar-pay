using BuildingBlocks.Domain;

namespace Modules.One.Domain.Events;

public record OrganizationCreatedDomainEvent(Guid OrganizationId, string Name, string Slug) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
