using System;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record PlanUpdatedDomainEvent(
    Guid PlanId,
    Guid OrganizationId,
    string Slug,
    string Name,
    decimal Price) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
