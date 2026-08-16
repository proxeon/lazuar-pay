using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class CommerceTransactionLog : Entity, IMustHaveTenant
{
    public const string StatusConfirmed = "CONFIRMED";
    public const string StatusRefundPending = "REFUND_PENDING";
    public const string StatusPartiallyRefunded = "PARTIALLY_REFUNDED";
    public const string StatusRefunded = "REFUNDED";
    public const string StatusRefundFailed = "REFUND_FAILED";

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
    public Guid? SubscriptionId { get; private set; }
    public string? GatewayName { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string? RefundReason { get; private set; }

    public decimal RemainingAmount => Amount > RefundedAmount ? Amount - RefundedAmount : 0m;

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
        string? externalReference,
        string? gatewayName = null,
        Guid? subscriptionId = null)
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
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? Id.ToString() : externalReference;
        GatewayName = NormalizeGatewayName(gatewayName);
        RefundedAmount = 0m;
        SubscriptionId = subscriptionId;
    }

    public void SetGatewayName(string gatewayName)
    {
        var normalized = NormalizeGatewayName(gatewayName);
        if (normalized is null)
        {
            throw new InvalidOperationException("Gateway name is required.");
        }

        GatewayName = normalized;
    }

    public void SetRefundReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var trimmed = reason.Trim();
        RefundReason = trimmed.Length > 255 ? trimmed[..255] : trimmed;
    }

    public void MarkRefundPending()
    {
        Status = StatusRefundPending;
    }

    public void MarkRefundFailed()
    {
        if (!string.Equals(Status, StatusRefundPending, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Status = StatusRefundFailed;
    }

    public void ApplyRefund(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        }

        RefundedAmount += amount;
        if (RefundedAmount > Amount)
        {
            RefundedAmount = Amount;
        }

        Status = RefundedAmount >= Amount ? StatusRefunded : StatusPartiallyRefunded;
    }

    public void TransitionToRefunded()
    {
        if (RemainingAmount > 0)
        {
            ApplyRefund(RemainingAmount);
            return;
        }

        Status = StatusRefunded;
    }

    public void Anonymize(Guid clientProfileId)
    {
        CustomerName = "Anonymized User";
        CustomerEmail = $"deleted_{clientProfileId}@localhost".ToLowerInvariant();
    }

    internal static string? NormalizeGatewayName(string? gatewayName)
    {
        if (string.IsNullOrWhiteSpace(gatewayName))
        {
            return null;
        }

        var normalized = gatewayName.Trim().ToUpperInvariant();
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }
}
