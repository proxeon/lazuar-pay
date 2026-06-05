using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

/// <summary>
/// Published when a user attempts to subscribe but hasn't paid yet.
/// The Messaging module listens to this to start an "Abandoned Cart" timer.
/// If no Activation event follows within 12/24 hours, it fires a recovery message.
/// </summary>
public record CommunityCheckoutInitiatedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
