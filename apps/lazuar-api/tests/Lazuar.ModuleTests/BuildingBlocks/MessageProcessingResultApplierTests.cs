using System;
using BuildingBlocks.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class MessageProcessingResultApplierTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ApplySuccess_Sets_ProcessedAt_Clears_Error_And_NextAttemptAt()
    {
        var msg = new OutboxMessage
        {
            AttemptCount = 2,
            Error = "previous",
            NextAttemptAt = UtcNow.AddMinutes(4),
            Status = MessageProcessingStatus.Pending
        };

        MessageProcessingResultApplier.ApplySuccess(msg, UtcNow);

        Assert.That(msg.ProcessedAt, Is.EqualTo(UtcNow));
        Assert.That(msg.Error, Is.Null);
        Assert.That(msg.NextAttemptAt, Is.Null);
        Assert.That(msg.AttemptCount, Is.EqualTo(2));
        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Pending));
    }

    [Test]
    public void ApplyFailure_First_Attempt_Schedules_Backoff_Without_ProcessedAt()
    {
        var msg = new OutboxMessage { AttemptCount = 0 };
        var ex = new InvalidOperationException("boom");

        MessageProcessingResultApplier.ApplyFailure(msg, ex, UtcNow);

        Assert.That(msg.AttemptCount, Is.EqualTo(1));
        Assert.That(msg.Error, Is.EqualTo(ex.ToString()));
        Assert.That(msg.ProcessedAt, Is.Null);
        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Pending));
        Assert.That(msg.NextAttemptAt, Is.EqualTo(UtcNow + MessageRetryPolicy.GetBackoff(1)));
    }

    [Test]
    public void ApplyFailure_Intermediate_Attempt_Increments_And_Reschedules()
    {
        var msg = new InboxMessage { AttemptCount = 2 };
        var ex = new Exception("retry me");

        MessageProcessingResultApplier.ApplyFailure(msg, ex, UtcNow);

        Assert.That(msg.AttemptCount, Is.EqualTo(3));
        Assert.That(msg.ProcessedAt, Is.Null);
        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Pending));
        Assert.That(msg.NextAttemptAt, Is.EqualTo(UtcNow + MessageRetryPolicy.GetBackoff(3)));
        Assert.That(msg.Error, Does.Contain("retry me"));
    }

    [Test]
    public void ApplyFailure_At_MaxAttempts_Marks_Dead_And_Processed()
    {
        var msg = new OutboxMessage { AttemptCount = MessageRetryPolicy.MaxAttempts - 1 };
        var ex = new InvalidOperationException("poison");

        MessageProcessingResultApplier.ApplyFailure(msg, ex, UtcNow);

        Assert.That(msg.AttemptCount, Is.EqualTo(MessageRetryPolicy.MaxAttempts));
        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Dead));
        Assert.That(msg.ProcessedAt, Is.EqualTo(UtcNow));
        Assert.That(msg.NextAttemptAt, Is.Null);
        Assert.That(msg.Error, Is.EqualTo(ex.ToString()));
    }

    [Test]
    public void ApplyFailure_Works_On_InboxMessage_Same_As_Outbox()
    {
        var msg = new InboxMessage { AttemptCount = MessageRetryPolicy.MaxAttempts - 1 };

        MessageProcessingResultApplier.ApplyFailure(msg, new Exception("x"), UtcNow);

        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Dead));
        Assert.That(msg.ProcessedAt, Is.EqualTo(UtcNow));
        Assert.That(msg.NextAttemptAt, Is.Null);
    }
}
