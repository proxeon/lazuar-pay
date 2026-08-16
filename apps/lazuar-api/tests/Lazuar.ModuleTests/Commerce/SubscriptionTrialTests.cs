using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionTrialTests
{
    [Test]
    public void ActivateTrial_SetsTrialingClocksAndSnapshot()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var ends = DateTime.UtcNow.AddDays(14);

        sub.ActivateTrial(ends, reminderOnly: true, quantity: 3, unitAmount: 49m);

        sub.Status.Should().Be("TRIALING");
        sub.TrialEndsAt.Should().Be(ends);
        sub.NextBillingDate.Should().Be(ends);
        sub.CurrentPeriodEnd.Should().Be(ends);
        sub.IsReminderOnly.Should().BeTrue();
        sub.Quantity.Should().Be(3);
        sub.UnitAmount.Should().Be(49m);
    }

    [Test]
    public void ActivateTrial_PastEnd_Throws()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var act = () => sub.ActivateTrial(DateTime.UtcNow.AddMinutes(-1), reminderOnly: false);
        act.Should().Throw<InvalidOperationException>().WithMessage("*future*");
    }

    [Test]
    public void ScheduleCancelAtPeriodEnd_AllowsTrialing()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.ActivateTrial(DateTime.UtcNow.AddDays(7), reminderOnly: false);

        sub.ScheduleCancelAtPeriodEnd();

        sub.CancelAtPeriodEnd.Should().BeTrue();
        sub.Status.Should().Be("TRIALING");
    }

    [Test]
    public void Product_SetTrialDays_RejectsOneTime()
    {
        var product = new Product(
            Guid.CreateVersion7(), "Once", "once", 10m, "FIXED", 0m, "MYR", "one_time", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());

        var act = () => product.SetTrialDays(14);
        act.Should().Throw<InvalidOperationException>().WithMessage("*one-time*");
    }

    [Test]
    public void Product_SetTrialDays_AcceptsMonthly()
    {
        var product = new Product(
            Guid.CreateVersion7(), "Mo", "mo", 10m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());

        product.SetTrialDays(14);
        product.TrialDays.Should().Be(14);
        product.Prices.Should().ContainSingle(p => p.IsDefault && p.Interval == "mo");
    }
}
