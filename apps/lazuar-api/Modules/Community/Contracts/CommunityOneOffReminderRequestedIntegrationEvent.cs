using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

/// <summary>
/// Published when an admin triggers a manual/one-off reminder for a subscriber.
/// The Messaging module listens to this to send the email/WhatsApp message.
/// </summary>
public record CommunityOneOffReminderRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
