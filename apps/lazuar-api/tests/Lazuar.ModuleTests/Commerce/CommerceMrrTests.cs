using System;
using FluentAssertions;
using Modules.Commerce.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceMrrTests
{
    [Test]
    public void TwoActiveMonthly_SumsLine()
    {
        var now = DateTime.UtcNow;
        var a = CommerceMrr.MonthlyEquivalent("ACTIVE", null, now, "mo", 100m, 1);
        var b = CommerceMrr.MonthlyEquivalent("ACTIVE", null, now, "mo", 100m, 1);
        (a + b).Should().Be(200m);
    }

    [Test]
    public void Yearly_DividesByTwelve()
    {
        CommerceMrr.MonthlyEquivalent("ACTIVE", null, DateTime.UtcNow, "yr", 1200m, 1)
            .Should().Be(100m);
    }

    [Test]
    public void CoalesceInterval_PrefersBillingInterval()
    {
        CommerceMrr.CoalesceInterval("yr", "mo").Should().Be("yr");
        CommerceMrr.CoalesceInterval("  yr  ", "mo").Should().Be("yr");
        CommerceMrr.CoalesceInterval("", "mo").Should().Be("mo");
        CommerceMrr.CoalesceInterval(null, "mo").Should().Be("mo");
    }

    [Test]
    public void YearlySeatOnMonthlyCatalog_IsOneHundredNotTwelveHundred()
    {
        var interval = CommerceMrr.CoalesceInterval("yr", "mo");
        CommerceMrr.MonthlyEquivalent("ACTIVE", null, DateTime.UtcNow, interval, 1200m, 1)
            .Should().Be(100m);
    }

    [Test]
    public void PastDue_IsZero()
    {
        CommerceMrr.MonthlyEquivalent("PAST_DUE", null, DateTime.UtcNow, "mo", 100m, 1)
            .Should().Be(0m);
    }

    [Test]
    public void Trialing_IsZero()
    {
        CommerceMrr.MonthlyEquivalent("TRIALING", null, DateTime.UtcNow, "mo", 100m, 1)
            .Should().Be(0m);
    }

    [Test]
    public void CollectionPaused_IsZero()
    {
        CommerceMrr.MonthlyEquivalent("ACTIVE", DateTime.UtcNow.AddDays(10), DateTime.UtcNow, "mo", 100m, 1)
            .Should().Be(0m);
    }

    [Test]
    public void SnapshotZero_FallsBackToCatalog()
    {
        CommerceMrr.MonthlyEquivalent("ACTIVE", null, DateTime.UtcNow, "mo", 0m, 2, fallbackUnit: 50m)
            .Should().Be(100m);
    }

    [Test]
    public void Arpu_ExcludesPastDueFromDenominator()
    {
        var now = DateTime.UtcNow;
        var mrr = 200m;
        var seats = new[]
        {
            ("ACTIVE", (DateTime?)null, "mo"),
            ("ACTIVE", (DateTime?)null, "mo"),
            ("PAST_DUE", (DateTime?)null, "mo"),
        }.Count(s => CommerceMrr.ContributesToMrr(s.Item1, s.Item2, now, s.Item3));

        seats.Should().Be(2);
        CommerceMrr.Arpu(mrr, seats).Should().Be(100d);
        CommerceMrr.Arpu(mrr, 3).Should().BeApproximately(66.666, 0.01);
    }

    [Test]
    public void CatalogEditDoesNotChangeSnapshotMath()
    {
        var snapshot = CommerceMrr.MonthlyEquivalent("ACTIVE", null, DateTime.UtcNow, "mo", 100m, 1, fallbackUnit: 200m);
        snapshot.Should().Be(100m);
    }
}
