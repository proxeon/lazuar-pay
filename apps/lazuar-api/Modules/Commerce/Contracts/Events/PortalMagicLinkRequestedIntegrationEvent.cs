using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Events;

/// <summary>
/// Public portal "email me a link" matched a subscription. Communications mints a 24h token.
/// </summary>
public record PortalMagicLinkRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
