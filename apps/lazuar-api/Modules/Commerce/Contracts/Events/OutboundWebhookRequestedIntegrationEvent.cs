using System;
using System.Text.Json;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Events;

/// <summary>
/// Requests durable outbound delivery to customer webhook endpoints for an organization.
/// When <see cref="TargetUrl"/> is null/empty, One fans out to all active workspace endpoints
/// (filtered by each endpoint's enabled_events). A non-empty TargetUrl is reserved for
/// optional future per-URL routing and is not used as a silent equality gate.
/// </summary>
public record OutboundWebhookRequestedIntegrationEvent(
    Guid OrganizationId,
    string? TargetUrl,
    string EventType,
    JsonElement Payload) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
