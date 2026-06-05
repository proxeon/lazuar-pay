using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunitySubscriptionCancelledIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    string PlanName,
    DateTime? CurrentPeriodEnd) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
