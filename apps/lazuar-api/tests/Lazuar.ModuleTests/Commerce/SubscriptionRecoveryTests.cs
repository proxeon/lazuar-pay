using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
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
        sub.DunningCampaignSnapshotJson.Should().BeNull();
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
        sub.CurrentPeriodEnd.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));
        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.DunningCampaignSnapshotJson.Should().BeNull();
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
        var snapshotA = DunningCampaignSnapshot.Empty(campaignA);
        var snapshotB = DunningCampaignSnapshot.Empty(campaignB);
        sub.AssignDunningCampaign(campaignA, snapshotA);
        sub.MarkDunningStepCompleted(3);
        sub.PauseDunning(DateTime.UtcNow.AddDays(2));

        sub.LastCompletedDayOffset.Should().Be(3);
        sub.CurrentDunningStepIndex.Should().Be(3);
        sub.DunningPausedUntil.Should().NotBeNull();

        sub.AssignDunningCampaign(campaignB, snapshotB);

        sub.CurrentDunningCampaignId.Should().Be(campaignB);
        sub.LastCompletedDayOffset.Should().BeNull();
        sub.CurrentDunningStepIndex.Should().Be(0);
        sub.TryGetDunningCampaignSnapshot()!.CampaignId.Should().Be(campaignB);
        // Pause is independent of reassignment (ClearDunning clears it; Assign does not).
        sub.DunningPausedUntil.Should().NotBeNull();
    }

    [Test]
    public void ClearDunning_RemovesCampaignProgressAndPause()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.MarkAsPastDue();
        var campaignId = Guid.CreateVersion7();
        sub.AssignDunningCampaign(campaignId, DunningCampaignSnapshot.Empty(campaignId));
        sub.MarkDunningStepCompleted(7);
        sub.PauseDunning(DateTime.UtcNow.AddDays(1));

        sub.ClearDunning();

        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.DunningCampaignSnapshotJson.Should().BeNull();
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
        sub.DunningCampaignSnapshotJson.Should().BeNull();
        sub.LastCompletedDayOffset.Should().BeNull();
    }

    [Test]
    public void RecoverFromPayment_PreservesIsReminderOnly()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10), isReminderOnly: true);
        sub.MarkAsPastDue();

        sub.IsReminderOnly.Should().BeTrue();

        sub.RecoverFromPayment(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        sub.Status.Should().Be("ACTIVE");
        sub.IsReminderOnly.Should().BeTrue();
    }

    [Test]
    public void Activate_OneTime_LeavesNextBillingDateNull()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, nextBillingDate: null, isReminderOnly: true);

        sub.Status.Should().Be("ACTIVE");
        sub.NextBillingDate.Should().BeNull();
        sub.IsReminderOnly.Should().BeTrue();
    }

    [Test]
    public void Suspend_SetsSuspendedAtAndStatus()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        sub.MarkAsPastDue();

        sub.Suspend();

        sub.Status.Should().Be("SUSPENDED");
        sub.SuspendedAt.Should().NotBeNull();
        sub.SuspendedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        sub.CurrentDunningCampaignId.Should().BeNull();
    }

    [Test]
    public void Cancel_SetsCanceledStatus_DoesNotClearSuspendedAtUnlessWasSuspended()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        sub.MarkAsPastDue();
        sub.Suspend();
        var suspendedAt = sub.SuspendedAt;

        sub.Cancel();

        sub.Status.Should().Be("CANCELED");
        sub.SuspendedAt.Should().Be(suspendedAt);
    }

    [Test]
    public void RecoverFromPayment_AndResume_ClearRenewalCheckout()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var originalNext = DateTime.UtcNow.AddDays(-5);
        sub.Activate(DateTime.UtcNow.AddDays(-35), originalNext);
        sub.SetCurrentRenewalCheckout("https://pay.test/bill/1", originalNext);
        sub.MarkAsPastDue();

        sub.RecoverFromPayment(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        sub.CurrentRenewalCheckoutUrl.Should().BeNull();
        sub.CurrentRenewalCheckoutForDate.Should().BeNull();

        sub.SetCurrentRenewalCheckout("https://pay.test/bill/2", DateTime.UtcNow);
        sub.Suspend();
        sub.Resume(DateTime.UtcNow.AddMonths(1));

        sub.CurrentRenewalCheckoutUrl.Should().BeNull();
        sub.CurrentRenewalCheckoutForDate.Should().BeNull();
    }

    [Test]
    public void AssignDunningCampaign_StoresParsedSnapshot()
    {
        var campaign = new DunningCampaign(
            Guid.CreateVersion7(),
            "Standard Recovery Strategy",
            "CANCEL",
            gracePeriodDays: 14);
        campaign.AddStep(0, "EMAIL", "Day 0", "Please pay", null);
        campaign.AddStep(3, "EMAIL", "Day 3", "Still unpaid", "wa");
        var snapshot = DunningCampaignSnapshot.From(campaign);

        var sub = new Subscription(campaign.OrganizationId, Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaign.Id, snapshot);

        sub.DunningCampaignSnapshotJson.Should().NotBeNullOrWhiteSpace();
        var parsed = sub.TryGetDunningCampaignSnapshot();
        parsed.Should().NotBeNull();
        parsed!.CampaignId.Should().Be(campaign.Id);
        parsed.GracePeriodDays.Should().Be(14);
        parsed.FinalAction.Should().Be("CANCEL");
        parsed.Steps.Should().HaveCount(2);
        parsed.Steps[1].DayOffset.Should().Be(3);
        parsed.Steps[1].EmailBody.Should().Be("Still unpaid");
    }

    [Test]
    public void AssignDunningCampaign_RejectsMismatchedSnapshotCampaignId()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var snapshot = DunningCampaignSnapshot.Empty(Guid.CreateVersion7());

        var act = () => sub.AssignDunningCampaign(Guid.CreateVersion7(), snapshot);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("snapshot");
        sub.CurrentDunningCampaignId.Should().BeNull();
        sub.DunningCampaignSnapshotJson.Should().BeNull();
    }
}
