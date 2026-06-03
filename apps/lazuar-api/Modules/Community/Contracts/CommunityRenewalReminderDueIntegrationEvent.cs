using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunityRenewalReminderDueIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid TemplateId,
    string Channel,
    string PlanName,
    string RenewalLink) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
