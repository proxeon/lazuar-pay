using System;
using FluentAssertions;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Entities;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ChargeAttemptLogTests
{
    [Test]
    public void Constructor_CreatesPendingAttemptWithNumberAndSource()
    {
        var subId = Guid.CreateVersion7();
        var target = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var log = new ChargeAttemptLog(subId, target, attemptNumber: 1, ChargeAttemptLog.SourceBilling);

        log.Id.Should().NotBeEmpty();
        log.SubscriptionId.Should().Be(subId);
        log.TargetBillingDate.Should().Be(target);
        log.AttemptNumber.Should().Be(1);
        log.Status.Should().Be(ChargeAttemptLog.StatusPending);
        log.Source.Should().Be(ChargeAttemptLog.SourceBilling);
        log.CompletedAt.Should().BeNull();
        log.FailureReason.Should().BeNull();
    }

    [Test]
    public void MultiRow_SameSubscriptionAndDate_AllowsDistinctAttemptNumbers()
    {
        // Documents multi-attempt schema: uniqueness is (SubscriptionId, TargetBillingDate, AttemptNumber),
        // so attempts 1 and 2 on the same cycle date are valid concurrent domain objects.
        var subId = Guid.CreateVersion7();
        var target = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        var first = new ChargeAttemptLog(subId, target, 1, ChargeAttemptLog.SourceBilling);
        var second = new ChargeAttemptLog(
            subId,
            target,
            2,
            ChargeAttemptLog.SourceDunning,
            dunningCampaignId: Guid.CreateVersion7(),
            dunningStepId: Guid.CreateVersion7());

        first.AttemptNumber.Should().Be(1);
        second.AttemptNumber.Should().Be(2);
        first.TargetBillingDate.Should().Be(second.TargetBillingDate);
        first.SubscriptionId.Should().Be(second.SubscriptionId);
        first.Source.Should().Be(ChargeAttemptLog.SourceBilling);
        second.Source.Should().Be(ChargeAttemptLog.SourceDunning);
        second.DunningCampaignId.Should().NotBeNull();
        second.DunningStepId.Should().NotBeNull();
    }

    [Test]
    public void MultiRow_UpToMaxAttemptsPerBillingCycle_IsSupported()
    {
        var subId = Guid.CreateVersion7();
        var target = DateTime.UtcNow.Date;

        var logs = new ChargeAttemptLog[ChargeAttemptLimits.MaxAttemptsPerBillingCycle];
        for (var i = 0; i < ChargeAttemptLimits.MaxAttemptsPerBillingCycle; i++)
        {
            var source = i == 0 ? ChargeAttemptLog.SourceBilling : ChargeAttemptLog.SourceDunning;
            logs[i] = new ChargeAttemptLog(subId, target, attemptNumber: i + 1, source);
        }

        logs.Should().HaveCount(4);
        logs.Select(l => l.AttemptNumber).Should().Equal(1, 2, 3, 4);
        logs.Select(l => l.Status).Should().OnlyContain(s => s == ChargeAttemptLog.StatusPending);
    }

    [Test]
    public void Constructor_AttemptNumberLessThanOne_Throws()
    {
        var act = () => new ChargeAttemptLog(
            Guid.CreateVersion7(),
            DateTime.UtcNow.Date,
            attemptNumber: 0,
            ChargeAttemptLog.SourceBilling);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("attemptNumber");
    }

    [Test]
    public void MarkFailed_FromPending_SetsFailedStatusReasonAndCompletedAt()
    {
        var log = new ChargeAttemptLog(
            Guid.CreateVersion7(),
            DateTime.UtcNow.Date,
            1,
            ChargeAttemptLog.SourceBilling);

        log.MarkFailed("card_declined", gatewayName: "STRIPE", gatewayResponseCode: "card_declined");

        log.Status.Should().Be(ChargeAttemptLog.StatusFailed);
        log.FailureReason.Should().Be("card_declined");
        log.GatewayName.Should().Be("STRIPE");
        log.GatewayResponseCode.Should().Be("card_declined");
        log.CompletedAt.Should().NotBeNull();
        log.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public void MarkSucceeded_FromPending_SetsSucceededAndClearsFailure()
    {
        var log = new ChargeAttemptLog(
            Guid.CreateVersion7(),
            DateTime.UtcNow.Date,
            2,
            ChargeAttemptLog.SourceDunning);

        log.MarkSucceeded(gatewayName: "STRIPE", gatewayResponseCode: "succeeded");

        log.Status.Should().Be(ChargeAttemptLog.StatusSucceeded);
        log.FailureReason.Should().BeNull();
        log.GatewayName.Should().Be("STRIPE");
        log.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void MarkFailed_AfterSucceeded_IsNoOp()
    {
        var log = new ChargeAttemptLog(
            Guid.CreateVersion7(),
            DateTime.UtcNow.Date,
            1,
            ChargeAttemptLog.SourceBilling);
        log.MarkSucceeded("STRIPE");

        log.MarkFailed("should_not_apply");

        log.Status.Should().Be(ChargeAttemptLog.StatusSucceeded);
        log.FailureReason.Should().BeNull();
    }

    [Test]
    public void MarkSucceeded_AfterSucceeded_IsNoOp()
    {
        var log = new ChargeAttemptLog(
            Guid.CreateVersion7(),
            DateTime.UtcNow.Date,
            1,
            ChargeAttemptLog.SourceBilling);
        log.MarkSucceeded("STRIPE", "ok");
        var completedAt = log.CompletedAt;

        log.MarkSucceeded("CHIP", "again");

        log.Status.Should().Be(ChargeAttemptLog.StatusSucceeded);
        log.GatewayName.Should().Be("STRIPE");
        log.CompletedAt.Should().Be(completedAt);
    }
}
