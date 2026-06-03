using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

/// <summary>
/// Published when a subscription is explicitly cancelled.
/// The Messaging module listens to this to send a cancellation confirmation
/// to the customer and a Telegram alert to the tenant admin.
/// </summary>
public record CommunitySubscriptionCancelledIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
