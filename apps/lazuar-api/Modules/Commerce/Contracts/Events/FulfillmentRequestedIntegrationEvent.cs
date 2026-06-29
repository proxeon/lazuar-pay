using System;
using System.Text.Json;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Events;

public record FulfillmentRequestedIntegrationEvent(
    Guid OrganizationId,
    string InternalTargetApp,
    string EventType,
    JsonElement Payload) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
