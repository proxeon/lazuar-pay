using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Entities;

/// <summary>
/// Child entity of CommunitySubscription.
/// Sanitized to only contain Domain facts, removing infrastructure details (like GatewaySessionId).
/// </summary>
public class PaymentRecord : Entity
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string PaymentMethod { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? ReceiptUrl { get; private set; }
    public string? Notes { get; private set; }
    public string RecordedBy { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PaymentRecord() { } // For EF Core

    internal PaymentRecord(
        Guid subscriptionId, decimal amount, string currency, 
        string paymentMethod, string? externalReference, string recordedBy, 
        DateTime periodStart, DateTime periodEnd, string? notes = null, string? receiptUrl = null)
    {
        Id = Guid.CreateVersion7();
        SubscriptionId = subscriptionId;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        ExternalReference = externalReference;
        RecordedBy = recordedBy;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Status = "CONFIRMED";
        Notes = notes;
        ReceiptUrl = receiptUrl;
        CreatedAt = DateTime.UtcNow;
    }
}
