using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CheckoutSessionCompleteTests
{
    [Test]
    public void TryComplete_OnlyWinsOnce()
    {
        var session = ProductSession();

        session.TryComplete().Should().BeTrue();
        session.Status.Should().Be("COMPLETED");
        session.TryComplete().Should().BeFalse();
        session.Status.Should().Be("COMPLETED");
    }

    [Test]
    public void TryExpire_DoesNotClobberCompleted()
    {
        var session = ProductSession();
        session.TryComplete().Should().BeTrue();

        session.TryExpire().Should().BeFalse();
        session.Status.Should().Be("COMPLETED");
    }

    [Test]
    public void TryComplete_DoesNotClobberExpired()
    {
        var session = ProductSession();
        session.TryExpire().Should().BeTrue();

        session.TryComplete().Should().BeFalse();
        session.Status.Should().Be("EXPIRED");
    }

    [Test]
    public void TryCompleteFromPayment_RevivesExpired()
    {
        var session = ProductSession();
        session.TryExpire().Should().BeTrue();

        session.TryCompleteFromPayment().Should().BeTrue();
        session.Status.Should().Be("COMPLETED");
        session.TryCompleteFromPayment().Should().BeFalse();
    }

    [Test]
    public void Complete_FromCompleted_Throws()
    {
        var session = ProductSession();
        session.Complete();

        var act = () => session.Complete();

        act.Should().Throw<InvalidOperationException>().WithMessage("*COMPLETED*");
        session.Status.Should().Be("COMPLETED");
    }

    [Test]
    public void Expire_FromCompleted_Throws()
    {
        var session = ProductSession();
        session.Complete();

        var act = () => session.Expire();

        act.Should().Throw<InvalidOperationException>().WithMessage("*COMPLETED*");
        session.Status.Should().Be("COMPLETED");
    }

    [Test]
    public void Complete_FromExpired_Throws()
    {
        var session = ProductSession();
        session.Expire();

        var act = () => session.Complete();

        act.Should().Throw<InvalidOperationException>().WithMessage("*EXPIRED*");
        session.Status.Should().Be("EXPIRED");
    }

    [Test]
    public void CustomSession_TryComplete_SameGuard()
    {
        var session = new CheckoutSession(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Work", 1, 100m) },
            DateTime.UtcNow.AddDays(1),
            false);

        session.TryComplete().Should().BeTrue();
        session.TryComplete().Should().BeFalse();
    }

    private static CheckoutSession ProductSession() =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            couponId: null,
            DateTime.UtcNow.AddHours(1));
}
