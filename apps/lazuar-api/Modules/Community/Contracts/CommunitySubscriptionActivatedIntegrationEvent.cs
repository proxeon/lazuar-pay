using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

/// <summary>
/// Published when a subscription is paid and becomes ACTIVE.
/// The Messaging module listens to this to send either a "Welcome" or "Payment Success" template.
/// </summary>
public record CommunitySubscriptionActivatedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    bool IsFirstPayment) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
