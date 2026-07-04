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
    public void Queue_SetsRecipients()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(100);
        b.Status.Should().Be("QUEUED");
        b.TotalRecipients.Should().Be(100);
    }

    [Test]
    public void Queue_Twice_Throws()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10);
        var act = () => b.Queue(10);
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
    public void RecordSent_AccumulatesSentCount()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10);
        b.MarkSending();
        b.RecordSent();
        b.RecordSent();
        b.SentCount.Should().Be(2);
    }

    [Test]
    public void MarkCompleted_SetsStatusAndTimestamp()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10);
        b.MarkSending();
        b.MarkCompleted();
        b.Status.Should().Be("COMPLETED");
        b.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void MarkFailed_SetsReason()
    {
        var b = new Broadcast(Guid.NewGuid(), "Subject", "<p>body</p>");
        b.Queue(10);
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
