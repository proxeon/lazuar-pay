using System;
using BuildingBlocks.Application;

namespace Modules.CRM.Contracts;

public record ClientProfileAnonymizedIntegrationEvent(
    Guid OrganizationId,
    Guid ClientProfileId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
