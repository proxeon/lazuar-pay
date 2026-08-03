using System;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class DunningCampaignDomainTests
{
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
        var productId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(
            Guid.CreateVersion7(),
            "Org default",
            "NONE",
            gracePeriodDays: 7);

        // Mirrors GatewayPaymentFailedIntegrationEventHandler / DunningEngineJob matching:
        // empty TargetProductIds and TargetPaymentMethods → match any product/method.
        var productOk = campaign.TargetProductIds.Count == 0 || campaign.TargetProductIds.Contains(productId);
        var methodOk = campaign.TargetPaymentMethods.Count == 0
                       || campaign.TargetPaymentMethods.Contains("ONLINE_GATEWAY");

        productOk.Should().BeTrue();
        methodOk.Should().BeTrue();
    }

    [Test]
    public void MatchesProductAndPaymentMethod_RespectsTargetFilters()
    {
        var productA = Guid.CreateVersion7();
        var productB = Guid.CreateVersion7();
        var campaign = new DunningCampaign(
            Guid.CreateVersion7(),
            "Gateway only",
            "SUSPEND",
            gracePeriodDays: 5,
            priorityOrder: 1,
            targetProductIds: new[] { productA },
            targetPaymentMethods: new[] { "ONLINE_GATEWAY" });

        var matchesAOnline =
            (campaign.TargetProductIds.Count == 0 || campaign.TargetProductIds.Contains(productA))
            && (campaign.TargetPaymentMethods.Count == 0 || campaign.TargetPaymentMethods.Contains("ONLINE_GATEWAY"));

        var matchesBOnline =
            (campaign.TargetProductIds.Count == 0 || campaign.TargetProductIds.Contains(productB))
            && (campaign.TargetPaymentMethods.Count == 0 || campaign.TargetPaymentMethods.Contains("ONLINE_GATEWAY"));

        var matchesAManual =
            (campaign.TargetProductIds.Count == 0 || campaign.TargetProductIds.Contains(productA))
            && (campaign.TargetPaymentMethods.Count == 0 || campaign.TargetPaymentMethods.Contains("MANUAL"));

        matchesAOnline.Should().BeTrue();
        matchesBOnline.Should().BeFalse();
        matchesAManual.Should().BeFalse();
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
