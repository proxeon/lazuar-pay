using System;
using FluentAssertions;
using Modules.Communications.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class BroadcastTests
{
    [Test]
    public void Constructor_SetsDraftStatus()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Status.Should().Be("DRAFT");
        b.SentCount.Should().Be(0);
    }

    [Test]
    public void Queue_SetsRecipientsAndHold()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        var holdId = Guid.NewGuid();
        b.Queue(100, holdId, 100);
        b.Status.Should().Be("QUEUED");
        b.TotalRecipients.Should().Be(100);
        b.CreditHoldId.Should().Be(holdId);
        b.CreditsReserved.Should().Be(100);
    }

    [Test]
    public void Queue_Twice_Throws()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10, Guid.NewGuid(), 10);
        var act = () => b.Queue(10, Guid.NewGuid(), 10);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void MarkSending_RequiresQueued()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        var act = () => b.MarkSending();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RecordSent_AccumulatesCreditsUsed()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10, Guid.NewGuid(), 10);
        b.MarkSending();
        b.RecordSent(1);
        b.RecordSent(1);
        b.SentCount.Should().Be(2);
        b.CreditsUsed.Should().Be(2);
    }

    [Test]
    public void MarkCompleted_SetsStatusAndTimestamp()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10, Guid.NewGuid(), 10);
        b.MarkSending();
        b.MarkCompleted();
        b.Status.Should().Be("COMPLETED");
        b.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void MarkFailed_SetsReason()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10, Guid.NewGuid(), 10);
        b.MarkFailed("boom");
        b.Status.Should().Be("FAILED");
        b.FailureReason.Should().Be("boom");
    }

    [Test]
    public void Constructor_ThrowsOnEmptySubject()
    {
        var act = () => new Broadcast(Guid.NewGuid(), "", "<p>body</p>");
        act.Should().Throw<ArgumentException>();
    }
}
