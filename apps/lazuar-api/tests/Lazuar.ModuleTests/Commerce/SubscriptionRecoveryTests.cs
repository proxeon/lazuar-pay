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
}
