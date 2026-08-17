using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Entities;

/// <summary>
/// Used to guarantee idempotency for incoming Stripe/Billplz webhooks.
/// </summary>
public class PaymentWebhookLog : Entity
{
    public Guid Id { get; private set; }

    /// <summary>
    /// URL tenant that received this delivery. EventId uniqueness is per tenant.
    /// </summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>
    /// The unique ID assigned to the webhook event by the provider (e.g., Stripe Event ID).
    /// </summary>
    public string EventId { get; private set; }

    /// <summary>
    /// STRIPE, BILLPLZ, etc.
    /// </summary>
    public string Provider { get; private set; }

    /// <summary>
    /// Payment-level business key (e.g. PAYMENT_COMPLETED:pi_xxx) used to dedupe dual events
    /// for the same money movement. Null when no stable gateway transaction id is available.
    /// </summary>
    public string? BusinessKey { get; private set; }

    /// <summary>
    /// Payments outbox row that carries the integration event. Null on pre-ticket backfill
    /// rows — do not invent work on redelivery.
    /// </summary>
    public Guid? OutboxMessageId { get; private set; }

    /// <summary>
    /// UTC time this webhook was received and domain work was queued (outbox insert).
    /// Not Commerce / Billing / session fulfillment.
    /// </summary>
    public DateTime ProcessedAt { get; private set; }

#pragma warning disable CS8618 
    private PaymentWebhookLog() { }
#pragma warning restore CS8618

    public PaymentWebhookLog(
        string eventId,
        string provider,
        string? businessKey = null,
        Guid? outboxMessageId = null,
        Guid organizationId = default)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        EventId = eventId;
        Provider = provider;
        BusinessKey = businessKey;
        OutboxMessageId = outboxMessageId;
        ProcessedAt = DateTime.UtcNow;
    }

    public void AssignOutboxMessageId(Guid outboxMessageId)
    {
        OutboxMessageId = outboxMessageId;
    }
}
