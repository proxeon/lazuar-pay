using System;
using System.Reflection;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionApplyPendingPlanChangeTests
{
    [Test]
    public void ApplyPending_WhenPendingEqualsCurrentProduct_ReturnsFalseAndClears()
    {
        var productId = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), false, 1, 40m);
        StickPendingProductId(sub, productId);

        var applied = sub.ApplyPendingPlanChange();

        applied.Should().BeFalse();
        sub.ProductId.Should().Be(productId);
        sub.PendingProductId.Should().BeNull();
        sub.UnitAmount.Should().Be(40m);
    }

    [Test]
    public void ApplyPending_WhenPendingIsDifferentProduct_ReturnsTrueAndSwitches()
    {
        var current = Guid.CreateVersion7();
        var next = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), current);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), false, 1, 40m);
        sub.SchedulePlanChange(next);

        var applied = sub.ApplyPendingPlanChange();

        applied.Should().BeTrue();
        sub.ProductId.Should().Be(next);
        sub.PendingProductId.Should().BeNull();
    }

    [Test]
    public void SchedulePlanChange_WhenSameProduct_ClearsPending()
    {
        var productId = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        sub.SchedulePlanChange(other);

        sub.SchedulePlanChange(productId);

        sub.PendingProductId.Should().BeNull();
        sub.ProductId.Should().Be(productId);
    }

    // Domain Schedule refuses this row; SQL can still leave PendingProductId = ProductId.
    private static void StickPendingProductId(Subscription sub, Guid productId)
    {
        typeof(Subscription).GetProperty(nameof(Subscription.PendingProductId))!
            .SetValue(sub, productId);
    }
}
