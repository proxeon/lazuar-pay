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
    string? HtmlEmailBody,
    string? PlainTextPhoneBody,
    string Channel = "EMAIL", // EMAIL, WHATSAPP, or ALL
    Guid? CreditHoldId = null, // When set (broadcast fan-out), the sender already reserved credits in a hold; the dispatch handler must not deduct from the wallet.
    string? UnsubscribeUrl = null // Marketing/broadcast: List-Unsubscribe + footer link when set
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
