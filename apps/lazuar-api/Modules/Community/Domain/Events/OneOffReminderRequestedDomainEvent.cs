using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Events;

public record OneOffReminderRequestedDomainEvent(
    Guid SubscriptionId,
    Guid OrganizationId,
    Guid ClientProfileId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel,
    DateTime? ScheduledAt = null) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    // Allow future scheduling for the Outbox Job Queue
    public DateTime OccurredOn { get; init; } = ScheduledAt ?? DateTime.UtcNow;
}
