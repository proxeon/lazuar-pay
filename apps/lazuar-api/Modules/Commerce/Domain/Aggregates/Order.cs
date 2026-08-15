using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Aggregates;

public class Order : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ClientProfileId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal AmountPaid { get; private set; }
    public string Currency { get; private set; }
    public int Quantity { get; private set; } = 1;
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private Order() { }
#pragma warning restore CS8618

    public Order(
        Guid organizationId,
        Guid clientProfileId,
        Guid productId,
        decimal amountPaid,
        string currency,
        int quantity = 1)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ClientProfileId = clientProfileId;
        ProductId = productId;
        AmountPaid = amountPaid;
        Currency = currency.ToUpperInvariant();
        Quantity = quantity < 1 ? 1 : quantity;
        Status = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = "COMPLETED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Refund()
    {
        Status = "REFUNDED";
        UpdatedAt = DateTime.UtcNow;
    }
}
