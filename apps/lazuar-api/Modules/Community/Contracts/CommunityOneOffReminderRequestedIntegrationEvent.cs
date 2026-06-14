using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunityOneOffReminderRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel,
    DateTime? ScheduledAt,
    string PlanName,
    decimal PlanPrice,
    string RenewalLink) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    // Maintain outbox scheduling capability
    public DateTime OccurredOn { get; init; } = ScheduledAt ?? DateTime.UtcNow;
}
