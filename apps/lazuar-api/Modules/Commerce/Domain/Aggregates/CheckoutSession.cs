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
    public DateTime? DueAt { get; private set; }
    public bool IsB2bRequired { get; private set; }

    /// <summary>Per-org sequential quote number (<c>QT-yyyy-#####</c>). Allocated once.</summary>
    public string? DocumentNumber { get; private set; }

    /// <summary>Buyer quantity for product checkout. Custom sessions leave this at 1; line qty lives in jsonb.</summary>
    public int Quantity { get; private set; } = 1;

    public Guid? PriceId { get; private set; }

    public string? IdempotencyKey { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public string? GatewayCheckoutUrl { get; private set; }

    private readonly List<AdHocLineItem> _adHocLineItems = new();
    public IReadOnlyCollection<AdHocLineItem> AdHocLineItems => _adHocLineItems.AsReadOnly();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Checkout metadata to copy onto the Subscription at first Activate (P09 / P10.22).</summary>
    public string? MetadataJson { get; private set; }

#pragma warning disable CS8618
    private CheckoutSession() { }
#pragma warning restore CS8618

    public CheckoutSession(
        Guid organizationId,
        Guid clientProfileId,
        Guid productId,
        Guid? couponId,
        DateTime expiresAt,
        int quantity = 1,
        Guid? priceId = null)
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
        Quantity = quantity < 1 ? 1 : quantity;
        PriceId = priceId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetIdempotency(string? key, string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        IdempotencyKey = key.Trim();
        RequestFingerprint = fingerprint;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGatewayCheckoutUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        GatewayCheckoutUrl = url.Trim();
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
        Quantity = 1;
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

    public void SetMetadataJson(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return;
        }

        MetadataJson = metadataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPriceId(Guid? priceId)
    {
        PriceId = priceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignDocumentNumber(string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber) || DocumentNumber != null)
        {
            return;
        }

        DocumentNumber = documentNumber.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDueAt(DateTime? dueAt)
    {
        DueAt = dueAt;
        if (dueAt.HasValue)
        {
            var linkFloor = dueAt.Value.AddDays(14);
            if (ExpiresAt < linkFloor)
            {
                ExpiresAt = linkFloor;
            }
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
