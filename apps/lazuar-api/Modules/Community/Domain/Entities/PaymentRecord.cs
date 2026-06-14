using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Entities;

/// <summary>
/// Acts purely as an AccessGrantLog. Proves the user paid for this specific cycle 
/// to grant Telegram/Zoom access. Financial summation and MRR calculations are 
/// strictly handled by the centralized Billing module.
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

#pragma warning disable CS8618
    private PaymentRecord() { }
#pragma warning restore CS8618

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
        Status = amount < 0 ? "REFUNDED" : "CONFIRMED";
        Notes = notes;
        ReceiptUrl = receiptUrl;
        CreatedAt = DateTime.UtcNow;
    }
}
