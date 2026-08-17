using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionCollectionPauseTests
{
    [Test]
    public void PauseCollection_ActiveFuture_SetsFlagKeepsActive()
    {
        var sub = Active();
        var until = DateTime.UtcNow.AddDays(20);

        sub.PauseCollection(until);

        sub.Status.Should().Be("ACTIVE");
        sub.IsCollectionPaused(DateTime.UtcNow).Should().BeTrue();
        sub.CollectionPausedUntil.Should().Be(until);
    }

    [Test]
    public void PauseCollection_PastDue_Throws()
    {
        var sub = Active();
        sub.MarkAsPastDue();

        var act = () => sub.PauseCollection(DateTime.UtcNow.AddDays(5));
        act.Should().Throw<InvalidOperationException>().WithMessage("*PAST_DUE*");
    }

    [Test]
    public void PauseCollection_PastResume_Throws()
    {
        var sub = Active();
        var act = () => sub.PauseCollection(DateTime.UtcNow.AddMinutes(-1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*future*");
    }

    [Test]
    public void ResumeCollection_ClearsFlag_AndPushesNextBill()
    {
        var sub = Active();
        sub.PauseCollection(DateTime.UtcNow.AddDays(5));
        var next = DateTime.UtcNow.AddMonths(1);

        sub.ResumeCollection(next);

        sub.CollectionPausedUntil.Should().BeNull();
        sub.IsCollectionPaused(DateTime.UtcNow).Should().BeFalse();
    }

    [Test]
    public void TryCompleteExpiredCollectionPause_RollsLikeManualResume()
    {
        var org = Guid.CreateVersion7();
        var sub = new Subscription(org, Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        sub.PauseCollection(DateTime.UtcNow.AddDays(5));

        sub.TryCompleteExpiredCollectionPause(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1))
            .Should().BeFalse();

        typeof(Subscription).GetProperty(nameof(Subscription.CollectionPausedUntil))!
            .SetValue(sub, DateTime.UtcNow.AddHours(-1));

        var next = DateTime.UtcNow.AddMonths(1);
        sub.TryCompleteExpiredCollectionPause(DateTime.UtcNow, next).Should().BeTrue();
        sub.CollectionPausedUntil.Should().BeNull();
        sub.NextBillingDate.Should().BeCloseTo(next, TimeSpan.FromSeconds(2));
    }

    private static Subscription Active()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20));
        return sub;
    }
}
