using System;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceCheckoutQuantityTests
{
    [Test]
    public void NormalizeOrThrow_NullOrOmitted_ReturnsOne()
    {
        var product = FixedOneTime();
        CommerceCheckoutQuantity.NormalizeOrThrow(null, product).Should().Be(1);
    }

    [Test]
    public void NormalizeOrThrow_OneOnFixedOneTime_ReturnsOne()
    {
        CommerceCheckoutQuantity.NormalizeOrThrow(1, FixedOneTime()).Should().Be(1);
    }

    [Test]
    public void NormalizeOrThrow_ThreeOnFixedOneTime_ReturnsThree()
    {
        CommerceCheckoutQuantity.NormalizeOrThrow(3, FixedOneTime()).Should().Be(3);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(100)]
    public void NormalizeOrThrow_OutOfRange_Throws(int quantity)
    {
        var act = () => CommerceCheckoutQuantity.NormalizeOrThrow(quantity, FixedOneTime());
        act.Should().Throw<InvalidOperationException>().WithMessage("*between 1 and 99*");
    }

    [Test]
    public void NormalizeOrThrow_ThreeOnMonthly_Throws()
    {
        var act = () => CommerceCheckoutQuantity.NormalizeOrThrow(3, Product("FIXED", "mo"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*one-time*");
    }

    [Test]
    public void NormalizeOrThrow_ThreeOnYearly_Throws()
    {
        var act = () => CommerceCheckoutQuantity.NormalizeOrThrow(3, Product("FIXED", "yr"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*one-time*");
    }

    [Test]
    public void NormalizeOrThrow_ThreeOnPwywOneTime_Throws()
    {
        var act = () => CommerceCheckoutQuantity.NormalizeOrThrow(3, Product("PWYW", "one_time"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*one-time*");
    }

    [TestCase("FIXED", "mo", 2)]
    [TestCase("FIXED", "mo", 99)]
    [TestCase("FIXED", "yr", 2)]
    [TestCase("FIXED", "yr", 99)]
    [TestCase("PWYW", "one_time", 2)]
    [TestCase("PWYW", "one_time", 99)]
    public void NormalizeOrThrow_NonOneOnRecurringOrPwyw_Throws(string pricingModel, string interval, int quantity)
    {
        var act = () => CommerceCheckoutQuantity.NormalizeOrThrow(quantity, Product(pricingModel, interval));
        act.Should().Throw<InvalidOperationException>().WithMessage("*one-time*");
    }

    [Test]
    public void NormalizeOrThrow_MaxOnFixedOneTime_ReturnsMax()
    {
        CommerceCheckoutQuantity.NormalizeOrThrow(CommerceCheckoutQuantity.Max, FixedOneTime())
            .Should().Be(99);
    }

    [Test]
    public void NormalizeOrThrow_OneOnMonthly_ReturnsOne()
    {
        CommerceCheckoutQuantity.NormalizeOrThrow(1, Product("FIXED", "mo")).Should().Be(1);
    }

    [Test]
    public void NormalizeOrThrow_OneOnPwyw_ReturnsOne()
    {
        CommerceCheckoutQuantity.NormalizeOrThrow(1, Product("PWYW", "one_time")).Should().Be(1);
    }

    private static Product FixedOneTime() => Product("FIXED", "one_time");

    private static Product Product(string pricingModel, string interval) =>
        new(
            Guid.CreateVersion7(),
            "Widget",
            "widget",
            10m,
            pricingModel,
            0m,
            "MYR",
            interval,
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
}
