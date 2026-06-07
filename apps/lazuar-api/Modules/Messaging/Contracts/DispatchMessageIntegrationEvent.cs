using System;
using BuildingBlocks.Application;

namespace Modules.Messaging.Contracts;

/// <summary>
/// A generic, context-free integration event. 
/// Any module can publish this to the EventBus, and the Messaging module will deliver it.
/// </summary>
public record DispatchMessageIntegrationEvent(
    Guid OrganizationId,
    string ToEmail,
    string? ToPhone,
    string Subject,
    string HtmlBody,
    string Channel = "EMAIL" // EMAIL, WHATSAPP, or ALL
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
