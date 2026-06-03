using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

/// <summary>
/// Published by the Community background job when a scheduled reminder (e.g., "3 days before due")
/// or a one-off reminder triggers. 
/// Tells the Messaging module exactly WHICH template to send and via WHICH channel.
/// </summary>
public record CommunityRenewalReminderDueIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid TemplateId,
    string Channel) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
