using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Entities;

/// <summary>
/// Used to guarantee idempotency for incoming Stripe/Billplz webhooks.
/// </summary>
public class PaymentWebhookLog : Entity
{
    public Guid Id { get; private set; }

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

    public DateTime ProcessedAt { get; private set; }

#pragma warning disable CS8618 
    private PaymentWebhookLog() { }
#pragma warning restore CS8618

    public PaymentWebhookLog(string eventId, string provider, string? businessKey = null)
    {
        Id = Guid.CreateVersion7();
        EventId = eventId;
        Provider = provider;
        BusinessKey = businessKey;
        ProcessedAt = DateTime.UtcNow;
    }
}
