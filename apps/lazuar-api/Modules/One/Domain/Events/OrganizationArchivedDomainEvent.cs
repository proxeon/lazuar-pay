using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain.Events;

public record OrganizationArchivedDomainEvent(Guid OrganizationId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
