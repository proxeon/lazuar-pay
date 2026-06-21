using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;
using Modules.Community.Domain.Events;

namespace Modules.Community.Domain.Aggregates;

public class CommunityCoupon : Entity, IAggregateRoot, IMustHaveTenant
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
    
    private readonly List<Guid> _applicablePlanIds = new();
    public IReadOnlyCollection<Guid> ApplicablePlanIds => _applicablePlanIds.AsReadOnly();
    
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private CommunityCoupon() { }
#pragma warning restore CS8618

    public CommunityCoupon(
        Guid organizationId, 
        string code, 
        string discountType, 
        decimal amount, 
        int maxUses, 
        DateTime? expiresAt, 
        decimal minimumOriginalPrice = 0,
        IEnumerable<Guid>? applicablePlanIds = null)
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

        if (applicablePlanIds != null && applicablePlanIds.Any())
        {
            _applicablePlanIds.AddRange(applicablePlanIds);
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

    public void Validate(decimal originalPrice, Guid targetPlanId)
    {
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

        if (_applicablePlanIds.Any() && !_applicablePlanIds.Contains(targetPlanId))
        {
            CheckRule(new GenericBusinessRule("This coupon is not valid for the selected plan."));
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

    public void ReleaseReservation()
    {
        if (ReservedCount > 0)
        {
            ReservedCount--;
            AddDomainEvent(new CouponReleasedDomainEvent(Id, OrganizationId, Code));
        }
    }

    public void UpdateLimits(int maxUses, decimal minimumOriginalPrice, DateTime? expiresAt, IEnumerable<Guid>? applicablePlanIds)
    {
        MaxUses = maxUses;
        MinimumOriginalPrice = minimumOriginalPrice;
        ExpiresAt = expiresAt;

        _applicablePlanIds.Clear();
        if (applicablePlanIds != null && applicablePlanIds.Any())
        {
            _applicablePlanIds.AddRange(applicablePlanIds);
        }
    }

    public void Archive()
    {
        ExpiresAt = DateTime.UtcNow;
    }
}
