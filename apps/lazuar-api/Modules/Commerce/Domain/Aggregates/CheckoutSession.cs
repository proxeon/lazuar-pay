using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Domain.Aggregates;

public class CheckoutSession : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ClientProfileId { get; private set; }
    
    public Guid? ProductId { get; private set; }
    public Guid? CouponId { get; private set; }

    /// <summary>
    /// Preferred payment gateway for custom (ad-hoc) checkout links.
    /// Product checkouts resolve gateway from Product.GatewayName instead.
    /// </summary>
    public string? GatewayName { get; private set; }
    
    public string Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsB2bRequired { get; private set; }

    private readonly List<AdHocLineItem> _adHocLineItems = new();
    public IReadOnlyCollection<AdHocLineItem> AdHocLineItems => _adHocLineItems.AsReadOnly();

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
        GatewayName = null;
        Status = "OPEN";
        ExpiresAt = expiresAt;
        IsB2bRequired = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public CheckoutSession(
        Guid organizationId,
        Guid clientProfileId,
        IEnumerable<AdHocLineItem> lineItems,
        DateTime expiresAt,
        bool isB2bRequired,
        string? gatewayName = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ClientProfileId = clientProfileId;
        ProductId = null;
        CouponId = null;
        GatewayName = string.IsNullOrWhiteSpace(gatewayName)
            ? null
            : gatewayName.Trim().ToUpperInvariant();
        Status = "OPEN";
        ExpiresAt = expiresAt;
        IsB2bRequired = isB2bRequired;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (lineItems != null)
        {
            _adHocLineItems.AddRange(lineItems);
        }

        if (_adHocLineItems.Count == 0)
        {
            CheckRule(new GenericBusinessRule("Custom checkout sessions must have at least one line item."));
        }
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
