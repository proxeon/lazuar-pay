using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Aggregates;

public class CheckoutSession : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ClientProfileId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? CouponId { get; private set; }
    public string Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private CheckoutSession() { }
#pragma warning restore CS8618

    public CheckoutSession(Guid organizationId, Guid clientProfileId, Guid productId, Guid? couponId, DateTime expiresAt)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ClientProfileId = clientProfileId;
        ProductId = productId;
        CouponId = couponId;
        Status = "OPEN";
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = "COMPLETED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        Status = "EXPIRED";
        UpdatedAt = DateTime.UtcNow;
    }
}
