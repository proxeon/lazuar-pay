using System;
using FluentAssertions;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class DunningCampaignDomainTests
{
    [Test]
    public void AddStep_DuplicateDayOffset_Throws()
    {
        var campaign = new DunningCampaign(Guid.CreateVersion7(), "Dup", "NONE", 7);
        campaign.AddStep(0, "EMAIL", "Pay", "Please pay", null);

        var act = () => campaign.AddStep(0, "AUTO_CHARGE", null, null, null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*day offset 0*");
    }

    [Test]
    public void RecordRecovery_IncrementsRevenueAndSavedCount()
    {
        var campaign = new DunningCampaign(
            Guid.CreateVersion7(),
            "Default recovery",
            finalAction: "SUSPEND",
            gracePeriodDays: 7,
            priorityOrder: 10);

        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);

        campaign.RecordRecovery(49.90m);
        campaign.RecordRecovery(10m);

        campaign.RecoveredRevenue.Should().Be(59.90m);
        campaign.SavedSubscriptions.Should().Be(2);
        campaign.ChurnedSubscriptions.Should().Be(0);
    }

    [Test]
    public void RecordChurn_IncrementsChurnedCount()
    {
        var campaign = new DunningCampaign(
            Guid.CreateVersion7(),
            "Churn path",
            finalAction: "CANCEL",
            gracePeriodDays: 14);

        campaign.RecordChurn();
        campaign.RecordChurn();

        campaign.ChurnedSubscriptions.Should().Be(2);
        campaign.SavedSubscriptions.Should().Be(0);
    }

    [Test]
    public void MatchesProductAndPaymentMethod_EmptyTargetsMatchAll()
    {
        var orgId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(
            orgId,
            "Org default",
            "NONE",
            gracePeriodDays: 7);

        campaign.Matches(orgId, productId, DunningCampaignMatcher.OnlineGateway).Should().BeTrue();
        campaign.Matches(orgId, productId, DunningCampaignMatcher.Manual).Should().BeTrue();
        campaign.Matches(Guid.CreateVersion7(), productId, DunningCampaignMatcher.OnlineGateway).Should().BeFalse();
    }

    [Test]
    public void MatchesProductAndPaymentMethod_RespectsTargetFilters()
    {
        var orgId = Guid.CreateVersion7();
        var productA = Guid.CreateVersion7();
        var productB = Guid.CreateVersion7();
        var campaign = new DunningCampaign(
            orgId,
            "Gateway only",
            "SUSPEND",
            gracePeriodDays: 5,
            priorityOrder: 1,
            targetProductIds: new[] { productA },
            targetPaymentMethods: new[] { DunningCampaignMatcher.OnlineGateway });

        campaign.Matches(orgId, productA, DunningCampaignMatcher.OnlineGateway).Should().BeTrue();
        campaign.Matches(orgId, productB, DunningCampaignMatcher.OnlineGateway).Should().BeFalse();
        campaign.Matches(orgId, productA, DunningCampaignMatcher.Manual).Should().BeFalse();
    }

    [Test]
    public void InferPaymentMethod_UnvaultedOnlineGateway_IsOnline()
    {
        DunningCampaignMatcher.InferPaymentMethod(null, "STRIPE").Should().Be(DunningCampaignMatcher.OnlineGateway);
        DunningCampaignMatcher.InferPaymentMethod("", "BILLPLZ").Should().Be(DunningCampaignMatcher.OnlineGateway);
        DunningCampaignMatcher.InferPaymentMethod(null, "CASH").Should().Be(DunningCampaignMatcher.Manual);
        DunningCampaignMatcher.InferPaymentMethod("pm_1", "CASH").Should().Be(DunningCampaignMatcher.OnlineGateway);
        DunningCampaignMatcher.InferPaymentMethod(null, null).Should().Be(DunningCampaignMatcher.Manual);
    }

    [Test]
    public void Archive_DeactivatesCampaign()
    {
        var campaign = new DunningCampaign(Guid.CreateVersion7(), "To archive", "NONE", 7);
        campaign.IsActive.Should().BeTrue();

        campaign.Archive();

        campaign.IsActive.Should().BeFalse();
    }
}
