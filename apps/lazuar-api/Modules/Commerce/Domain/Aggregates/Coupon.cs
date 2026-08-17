using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.Events;

namespace Modules.Commerce.Domain.Aggregates;

public class Coupon : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; private set; }
    public string DiscountType { get; private set; }
    public decimal Amount { get; private set; }
    public int MaxUses { get; private set; }
    public int UsedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public decimal MinimumOriginalPrice { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    
    private readonly List<Guid> _applicableProductIds = new();
    public IReadOnlyCollection<Guid> ApplicableProductIds => _applicableProductIds.AsReadOnly();
    
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private Coupon() { }
#pragma warning restore CS8618

    public Coupon(
        Guid organizationId, 
        string code, 
        string discountType, 
        decimal amount, 
        int maxUses, 
        DateTime? expiresAt, 
        decimal minimumOriginalPrice = 0,
        IEnumerable<Guid>? applicableProductIds = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.");
        if (discountType != "PERCENTAGE" && discountType != "FIXED") throw new ArgumentException("Invalid discount type.");
        if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.");

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Code = code.ToUpperInvariant().Trim();
        DiscountType = discountType;
        Amount = amount;
        MaxUses = maxUses;
        UsedCount = 0;
        ReservedCount = 0;
        MinimumOriginalPrice = minimumOriginalPrice;
        ExpiresAt = expiresAt;
        IsActive = true;

        if (applicableProductIds != null && applicableProductIds.Any())
        {
            _applicableProductIds.AddRange(applicableProductIds);
        }

        CreatedAt = DateTime.UtcNow;
    }

    public decimal CalculateDiscount(decimal originalPrice)
    {
        if (DiscountType == "PERCENTAGE")
        {
            var discount = originalPrice * (Amount / 100m);
            return Math.Min(discount, originalPrice);
        }
        return Math.Min(Amount, originalPrice);
    }

    public void Validate(decimal originalPrice, Guid targetProductId)
    {
        if (!IsActive)
        {
            CheckRule(new GenericBusinessRule("This coupon has been archived and is no longer valid."));
        }

        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
        {
            CheckRule(new GenericBusinessRule("This coupon has expired."));
        }

        if (MaxUses > 0 && (UsedCount + ReservedCount) >= MaxUses)
        {
            CheckRule(new GenericBusinessRule("This coupon has reached its maximum usage limit."));
        }

        if (MinimumOriginalPrice > 0 && originalPrice < MinimumOriginalPrice)
        {
            CheckRule(new GenericBusinessRule($"This coupon requires a minimum original price of {MinimumOriginalPrice:F2}."));
        }

        if (_applicableProductIds.Any() && !_applicableProductIds.Contains(targetProductId))
        {
            CheckRule(new GenericBusinessRule("This coupon is not valid for the selected product."));
        }
    }

    public void Reserve()
    {
        ReservedCount++;
        AddDomainEvent(new CouponReservedDomainEvent(Id, OrganizationId, Code));
    }

    public void ConfirmReservation()
    {
        if (ReservedCount <= 0)
        {
            CheckRule(new GenericBusinessRule("No active reservation to confirm."));
        }

        ReservedCount--;
        UsedCount++;
        AddDomainEvent(new CouponConfirmedDomainEvent(Id, OrganizationId, Code));
    }

    /// <summary>
    /// Payment landed after expiry released the reservation — still consume one use.
    /// </summary>
    public void ConfirmPaidRedemption()
    {
        if (ReservedCount > 0)
        {
            ConfirmReservation();
            return;
        }

        UsedCount++;
        AddDomainEvent(new CouponConfirmedDomainEvent(Id, OrganizationId, Code));
    }

    public void ReleaseReservation()
    {
        if (ReservedCount > 0)
        {
            ReservedCount--;
            AddDomainEvent(new CouponReleasedDomainEvent(Id, OrganizationId, Code));
        }
    }

    public void UpdateDetails(string code, string discountType, decimal amount, int maxUses, decimal minimumOriginalPrice, DateTime? expiresAt, IEnumerable<Guid>? applicableProductIds)
    {
        var codeChanged = !string.Equals(Code, code, StringComparison.OrdinalIgnoreCase);
        var typeChanged = !string.Equals(DiscountType, discountType, StringComparison.OrdinalIgnoreCase);
        var amountChanged = Amount != amount;

        if ((codeChanged || typeChanged || amountChanged) && UsedCount > 0)
        {
            CheckRule(new GenericBusinessRule("Core coupon details (Code, Type, Amount) cannot be modified after the coupon has been redeemed."));
        }

        if (codeChanged) Code = code.ToUpperInvariant().Trim();
        if (typeChanged) DiscountType = discountType;
        if (amountChanged) Amount = amount;

        MaxUses = maxUses;
        MinimumOriginalPrice = minimumOriginalPrice;
        ExpiresAt = expiresAt;

        _applicableProductIds.Clear();
        if (applicableProductIds != null && applicableProductIds.Any())
        {
            _applicableProductIds.AddRange(applicableProductIds);
        }
    }

    public void Archive()
    {
        IsActive = false;
    }

    public void Restore()
    {
        IsActive = true;
    }
}
