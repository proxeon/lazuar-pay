using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class CommerceTransactionLog : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public decimal Amount { get; private set; }
    public decimal FeeAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public string? ProductName { get; private set; }
    public string RecordedByName { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }

    #pragma warning disable CS8618
    private CommerceTransactionLog() { }
    #pragma warning restore CS8618

    public CommerceTransactionLog(
        Guid organizationId,
        decimal amount,
        decimal feeAmount,
        string currency,
        string status,
        string customerName,
        string customerEmail,
        string? productName,
        string recordedByName,
        string? externalReference)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Amount = amount;
        FeeAmount = feeAmount;
        NetAmount = amount - feeAmount;
        Currency = currency.ToUpperInvariant();
        Status = status.ToUpperInvariant();
        CreatedAt = DateTime.UtcNow;
        CustomerName = customerName;
        CustomerEmail = customerEmail.ToLowerInvariant();
        ProductName = productName;
        RecordedByName = recordedByName;
        ExternalReference = externalReference;
    }

    public void TransitionToRefunded()
    {
        Status = "REFUNDED";
    }
}
