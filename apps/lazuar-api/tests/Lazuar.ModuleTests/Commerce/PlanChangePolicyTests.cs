using System;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class PlanChangePolicyTests
{
    [Test]
    public void Preview_MidCycle_AmountDueNowZero_EffectiveAtNextBill()
    {
        var next = DateTime.UtcNow.AddDays(12);
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, next, isReminderOnly: false, quantity: 2, unitAmount: 40m);
        var current = Product(sub.OrganizationId, "Basic", 40m);
        var target = Product(sub.OrganizationId, "Pro", 90m);

        var preview = PlanChangePolicy.Preview(sub, current, target, 2);

        preview.AmountDueNow.Should().Be(0m);
        preview.Policy.Should().Be("next_renewal");
        preview.EffectiveAt.Should().Be(next);
        preview.CurrentAmount.Should().Be(80m);
        preview.NextAmount.Should().Be(180m);
    }

    [Test]
    public void RejectImmediateOrProrate_ProrateTrue_Throws()
    {
        var act = () => PlanChangePolicy.RejectImmediateOrProrate(true, null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Proration*");
    }

    [Test]
    public void RejectImmediateOrProrate_ApplyImmediate_Throws()
    {
        var act = () => PlanChangePolicy.RejectImmediateOrProrate(null, "immediate");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Immediate*");
    }

    [Test]
    public void GuardTarget_OtherGateway_Throws()
    {
        var org = Guid.CreateVersion7();
        var sub = new Subscription(org, Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(10));
        var current = Product(org, "A", 10m, "STRIPE");
        var target = Product(org, "B", 20m, "BILLPLZ");

        var act = () => PlanChangePolicy.GuardTargetProduct(sub, current, target);
        act.Should().Throw<InvalidOperationException>().WithMessage("*gateway*");
    }

    private static Product Product(Guid org, string name, decimal price, string gateway = "STRIPE") =>
        new(org, name, name.ToLowerInvariant(), price, "FIXED", 0m, "MYR", "mo", gateway,
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
}
