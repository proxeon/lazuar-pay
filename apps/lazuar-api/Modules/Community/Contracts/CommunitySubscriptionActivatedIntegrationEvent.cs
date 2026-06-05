using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunitySubscriptionActivatedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    bool IsFirstPayment,
    string PlanName,
    string GroupLink,
    string MeetingLink,
    decimal AmountPaid) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
