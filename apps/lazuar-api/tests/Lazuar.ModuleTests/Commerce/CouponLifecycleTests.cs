using System;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// C.9 — Coupon reserve / confirm / release (expire path covered with session job in CommerceProductCompletenessTests).
/// </summary>
[TestFixture]
public class CouponLifecycleTests
{
    private static Coupon NewCoupon(int maxUses = 2) =>
        new(Guid.CreateVersion7(), "SAVE10", "PERCENTAGE", 10m, maxUses, expiresAt: null);

    [Test]
    public void Reserve_IncrementsReservedCount()
    {
        var coupon = NewCoupon();
        coupon.Reserve();
        coupon.ReservedCount.Should().Be(1);
        coupon.UsedCount.Should().Be(0);
    }

    [Test]
    public void ConfirmReservation_MovesReservedToUsed()
    {
        var coupon = NewCoupon();
        coupon.Reserve();
        coupon.ConfirmReservation();
        coupon.ReservedCount.Should().Be(0);
        coupon.UsedCount.Should().Be(1);
    }

    [Test]
    public void ReleaseReservation_DropsReservedWithoutUse()
    {
        var coupon = NewCoupon();
        coupon.Reserve();
        coupon.ReleaseReservation();
        coupon.ReservedCount.Should().Be(0);
        coupon.UsedCount.Should().Be(0);
    }

    [Test]
    public void Confirm_WithoutReserve_Throws()
    {
        var coupon = NewCoupon();
        var act = () => coupon.ConfirmReservation();
        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Test]
    public void Release_WhenNoneReserved_IsNoOp()
    {
        var coupon = NewCoupon();
        coupon.ReleaseReservation();
        coupon.ReservedCount.Should().Be(0);
    }

    [Test]
    public void Validate_BlocksWhenMaxUsesReached_IncludingReservations()
    {
        var orgId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var coupon = new Coupon(orgId, "ONCE", "FIXED", 5m, maxUses: 1, expiresAt: null);
        coupon.Reserve();

        var act = () => coupon.Validate(originalPrice: 50m, targetProductId: productId);
        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*maximum usage*");
    }

    [Test]
    public void Validate_BlocksExpiredCoupon()
    {
        var orgId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var coupon = new Coupon(orgId, "OLD", "FIXED", 5m, maxUses: 10, expiresAt: DateTime.UtcNow.AddHours(-1));

        var act = () => coupon.Validate(100m, productId);
        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*expired*");
    }

    [Test]
    public void ConfirmPaidRedemption_AfterRelease_IncrementsUsedWithoutReserve()
    {
        var coupon = NewCoupon();
        coupon.Reserve();
        coupon.ReleaseReservation();

        coupon.ConfirmPaidRedemption();

        coupon.ReservedCount.Should().Be(0);
        coupon.UsedCount.Should().Be(1);
    }

    [Test]
    public void CalculateDiscount_PercentageAndFixed()
    {
        var pct = new Coupon(Guid.CreateVersion7(), "P", "PERCENTAGE", 10m, 100, null);
        pct.CalculateDiscount(200m).Should().Be(20m);

        var fixedAmt = new Coupon(Guid.CreateVersion7(), "F", "FIXED", 30m, 100, null);
        fixedAmt.CalculateDiscount(20m).Should().Be(20m); // capped at original
    }

    [Test]
    public void ReserveConfirmRelease_FullLifecycle_DoesNotExceedMaxUses()
    {
        var coupon = NewCoupon(maxUses: 2);
        coupon.Reserve();
        coupon.ConfirmReservation(); // used=1
        coupon.Reserve();
        coupon.ReleaseReservation(); // back to used=1 reserved=0
        coupon.Reserve();
        coupon.ConfirmReservation(); // used=2

        coupon.UsedCount.Should().Be(2);
        coupon.ReservedCount.Should().Be(0);

        var act = () => coupon.Validate(100m, Guid.CreateVersion7());
        act.Should().Throw<BusinessRuleValidationException>();
    }
}
