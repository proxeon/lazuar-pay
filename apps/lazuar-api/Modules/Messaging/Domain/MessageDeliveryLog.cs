using System;
using BuildingBlocks.Domain;

namespace Modules.Messaging.Domain;

/// <summary>
/// Support-facing record of a dispatch attempt (email/WhatsApp).
/// Status vocabulary: SENT | FAILED | SKIPPED.
/// </summary>
public class MessageDeliveryLog : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Channel { get; private set; } = "";
    public string Recipient { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string? ProviderMessageId { get; private set; }
    public string? Error { get; private set; }
    public Guid? CorrelationEventId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MessageDeliveryLog()
    {
    }

    public MessageDeliveryLog(
        Guid organizationId,
        string channel,
        string recipient,
        string status,
        string? providerMessageId = null,
        string? error = null,
        Guid? correlationEventId = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Channel = channel;
        Recipient = recipient ?? "";
        Status = status;
        ProviderMessageId = providerMessageId;
        Error = error;
        CorrelationEventId = correlationEventId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Anonymize(Guid clientProfileId)
    {
        Recipient = $"deleted_{clientProfileId}@localhost";
    }
}
