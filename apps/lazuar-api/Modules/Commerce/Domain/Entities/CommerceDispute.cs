using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class CommerceDispute : Entity, IMustHaveTenant
{
    public const string StatusOpen = "OPEN";
    public const string StatusWon = "WON";
    public const string StatusLost = "LOST";
    public const string StatusClosed = "CLOSED";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string GatewayTransactionId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "MYR";
    public string Status { get; private set; } = StatusOpen;
    public Guid? SubscriptionId { get; private set; }
    public Guid? CheckoutSessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private CommerceDispute() { }
#pragma warning restore CS8618

    public CommerceDispute(
        Guid organizationId,
        string gatewayTransactionId,
        decimal amount,
        string currency,
        Guid? subscriptionId = null,
        Guid? checkoutSessionId = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        GatewayTransactionId = gatewayTransactionId.Trim();
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "MYR" : currency.Trim().ToUpperInvariant();
        Status = StatusOpen;
        SubscriptionId = subscriptionId;
        CheckoutSessionId = checkoutSessionId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Resolve(string outcome)
    {
        if (!string.Equals(Status, StatusOpen, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var normalized = (outcome ?? string.Empty).Trim().ToUpperInvariant();
        Status = normalized switch
        {
            "WON" or "WON_SELLER" => StatusWon,
            "LOST" or "LOST_CUSTOMER" => StatusLost,
            _ => StatusClosed
        };
    }

    public bool IsOpen => string.Equals(Status, StatusOpen, StringComparison.OrdinalIgnoreCase);
}
