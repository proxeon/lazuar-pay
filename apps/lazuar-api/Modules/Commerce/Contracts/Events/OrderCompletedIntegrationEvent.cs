using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Events;

public record OrderCompletedIntegrationEvent(
    Guid OrganizationId,
    Guid OrderId,
    Guid ClientProfileId,
    Guid ProductId,
    List<string> FulfillmentTargets) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
