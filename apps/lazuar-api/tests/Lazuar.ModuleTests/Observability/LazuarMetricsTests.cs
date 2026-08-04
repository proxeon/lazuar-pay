using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

[TestFixture]
public class LazuarMetricsTests
{
    [Test]
    public void RecordDeadLetter_Increments_When_Message_Marked_Dead()
    {
        var before = LazuarMetrics.DeadLettersTotal;
        var msg = new OutboxMessage { AttemptCount = MessageRetryPolicy.MaxAttempts - 1 };

        MessageProcessingResultApplier.ApplyFailure(msg, new System.Exception("poison"), System.DateTime.UtcNow);

        Assert.That(msg.Status, Is.EqualTo(MessageProcessingStatus.Dead));
        Assert.That(LazuarMetrics.DeadLettersTotal, Is.EqualTo(before + 1));
    }

    [Test]
    public void RecordWebhookFailed_And_DunningCancel_Increment()
    {
        var webhookBefore = LazuarMetrics.WebhookFailedTotal;
        var dunningBefore = LazuarMetrics.DunningCancelsTotal;

        LazuarMetrics.RecordWebhookFailed("outbound");
        LazuarMetrics.RecordDunningCancel();

        Assert.That(LazuarMetrics.WebhookFailedTotal, Is.EqualTo(webhookBefore + 1));
        Assert.That(LazuarMetrics.DunningCancelsTotal, Is.EqualTo(dunningBefore + 1));
    }
}
