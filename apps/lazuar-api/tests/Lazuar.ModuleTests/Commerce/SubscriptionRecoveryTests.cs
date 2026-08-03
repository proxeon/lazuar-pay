using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionRecoveryTests
{
    [Test]
    public void RecoverFromPayment_FromPastDue_SetsActiveAdvancesDatesAndClearsDunning()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var campaignId = Guid.CreateVersion7();

        var sub = new Subscription(orgId, clientId, productId);
        var originalNext = DateTime.UtcNow.AddDays(-5);
        sub.Activate(DateTime.UtcNow.AddDays(-35), originalNext);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaignId);

        sub.Status.Should().Be("PAST_DUE");
        sub.CurrentDunningCampaignId.Should().Be(campaignId);
        sub.NextBillingDate.Should().Be(originalNext);

        var periodEnd = DateTime.UtcNow;
        var nextBilling = DateTime.UtcNow.AddMonths(1);

        sub.RecoverFromPayment(periodEnd, nextBilling);

        sub.Status.Should().Be("ACTIVE");
        sub.CurrentPeriodEnd.Should().BeCloseTo(periodEnd, TimeSpan.FromSeconds(1));
        sub.NextBillingDate.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));
        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.CurrentDunningStepIndex.Should().Be(0);
        sub.LastCompletedDayOffset.Should().BeNull();
        sub.DunningPausedUntil.Should().BeNull();
        sub.SuspendedAt.Should().BeNull();
    }

    [Test]
    public void Activate_FromPastDue_DoesNotAdvanceBillingDates()
    {
        // Documents intentional Activate semantics: arrears config updates must not skip a cycle.
        // Recovery must use RecoverFromPayment (or Resume for SUSPENDED) instead.
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var originalNext = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var originalPeriodEnd = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        sub.Activate(originalPeriodEnd, originalNext);
        sub.MarkAsPastDue();

        var newNext = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var newPeriodEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        sub.Activate(newPeriodEnd, newNext);

        sub.Status.Should().Be("ACTIVE");
        sub.NextBillingDate.Should().Be(originalNext);
        sub.CurrentPeriodEnd.Should().Be(originalPeriodEnd);
    }

    [Test]
    public void Resume_FromSuspended_AdvancesNextBillingAndClearsDunning()
    {
        var campaignId = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-30));
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaignId);
        sub.Suspend();

        var nextBilling = DateTime.UtcNow.AddMonths(1);
        sub.Resume(nextBilling);

        sub.Status.Should().Be("ACTIVE");
        sub.NextBillingDate.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));
        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.SuspendedAt.Should().BeNull();
    }

    [Test]
    public void MarkAsPastDue_FromActive_SetsPastDueStatus()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));

        sub.MarkAsPastDue();

        sub.Status.Should().Be("PAST_DUE");
        sub.CurrentDunningCampaignId.Should().BeNull();
    }

    [Test]
    public void AssignDunningCampaign_ResetsStepProgress()
    {
        var campaignA = Guid.CreateVersion7();
        var campaignB = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaignA);
        sub.MarkDunningStepCompleted(3);
        sub.PauseDunning(DateTime.UtcNow.AddDays(2));

        sub.LastCompletedDayOffset.Should().Be(3);
        sub.CurrentDunningStepIndex.Should().Be(3);
        sub.DunningPausedUntil.Should().NotBeNull();

        sub.AssignDunningCampaign(campaignB);

        sub.CurrentDunningCampaignId.Should().Be(campaignB);
        sub.LastCompletedDayOffset.Should().BeNull();
        sub.CurrentDunningStepIndex.Should().Be(0);
        // Pause is independent of reassignment (ClearDunning clears it; Assign does not).
        sub.DunningPausedUntil.Should().NotBeNull();
    }

    [Test]
    public void ClearDunning_RemovesCampaignProgressAndPause()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(Guid.CreateVersion7());
        sub.MarkDunningStepCompleted(7);
        sub.PauseDunning(DateTime.UtcNow.AddDays(1));

        sub.ClearDunning();

        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.CurrentDunningStepIndex.Should().Be(0);
        sub.LastCompletedDayOffset.Should().BeNull();
        sub.DunningPausedUntil.Should().BeNull();
        // ClearDunning does not change arrears status — recovery methods do.
        sub.Status.Should().Be("PAST_DUE");
    }

    [Test]
    public void MarkDunningStepCompleted_TracksHighestDayOffset()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.AssignDunningCampaign(Guid.CreateVersion7());

        sub.MarkDunningStepCompleted(0);
        sub.MarkDunningStepCompleted(3);
        sub.MarkDunningStepCompleted(1); // lower than 3 — ignored for highest

        sub.LastCompletedDayOffset.Should().Be(3);
        sub.CurrentDunningStepIndex.Should().Be(3);
    }

    [Test]
    public void RecoverFromPayment_ClearsDunningAndRecoversMetricsPathReady()
    {
        // Domain half of "recovery payment clears dunning + recovery metrics":
        // RecoverFromPayment clears assignment; DunningCampaign.RecordRecovery is tested separately.
        var campaignId = Guid.CreateVersion7();
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaignId);
        sub.MarkDunningStepCompleted(0);

        sub.RecoverFromPayment(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        sub.Status.Should().Be("ACTIVE");
        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.LastCompletedDayOffset.Should().BeNull();
    }
}
