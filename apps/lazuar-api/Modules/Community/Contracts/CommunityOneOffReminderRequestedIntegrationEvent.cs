using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunityOneOffReminderRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel,
    DateTime? ScheduledAt = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = ScheduledAt ?? DateTime.UtcNow;
}
