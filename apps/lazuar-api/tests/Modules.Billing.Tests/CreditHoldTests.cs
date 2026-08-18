using System;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Billing.Domain.Aggregates;
using NUnit.Framework;

namespace Modules.Billing.Tests;

[TestFixture]
public class CreditHoldTests
{
    [Test]
    public void Constructor_SetsTotalAndRemaining()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.TotalAmount.Should().Be(100);
        hold.RemainingAmount.Should().Be(100);
        hold.Status.Should().Be("HELD");
    }

    [Test]
    public void Consume_DecreasesRemaining()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.Consume(30);
        hold.RemainingAmount.Should().Be(70);
        hold.TotalAmount.Should().Be(100);
    }

    [Test]
    public void Consume_ExactlyExhaustingHold_Succeeds()
    {
        var hold = new CreditHold(Guid.NewGuid(), 50, "broadcast-1", "broadcast");
        hold.Consume(50);
        hold.RemainingAmount.Should().Be(0);
        hold.Status.Should().Be("SETTLED");
    }

    [Test]
    public void Consume_ThrowsOnInsufficientHeld()
    {
        var hold = new CreditHold(Guid.NewGuid(), 10, "broadcast-1", "broadcast");
        var act = () => hold.Consume(11);
        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Test]
    public void ReleaseRemaining_ReturnsRemainderAndSettles()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.Consume(40);
        var released = hold.ReleaseRemaining();
        released.Should().Be(60);
        hold.RemainingAmount.Should().Be(0);
        hold.Status.Should().Be("RELEASED");
    }

    [Test]
    public void ReleaseRemaining_OnFullHold_ReturnsTotal()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.ReleaseRemaining().Should().Be(100);
        hold.Status.Should().Be("RELEASED");
    }

    [Test]
    public void Consume_AfterRelease_Throws()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.ReleaseRemaining();
        var act = () => hold.Consume(1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ReleaseRemaining_Twice_Throws()
    {
        var hold = new CreditHold(Guid.NewGuid(), 100, "broadcast-1", "broadcast");
        hold.ReleaseRemaining();
        var act = () => hold.ReleaseRemaining();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Constructor_ThrowsOnNonPositiveAmount()
    {
        var act = () => new CreditHold(Guid.NewGuid(), 0, "broadcast-1", "broadcast");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_ThrowsOnEmptyCorrelationId()
    {
        var act = () => new CreditHold(Guid.NewGuid(), 10, "", "broadcast");
        act.Should().Throw<ArgumentException>();
    }
}
