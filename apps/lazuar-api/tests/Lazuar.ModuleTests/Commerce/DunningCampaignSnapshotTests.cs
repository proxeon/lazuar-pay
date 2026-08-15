using System;
using System.Linq;
using FluentAssertions;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class DunningCampaignSnapshotTests
{
    [Test]
    public void From_CopiesFinalGraceAndEveryStepField()
    {
        var orgId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(orgId, "Standard Recovery Strategy", "CANCEL", gracePeriodDays: 7);
        campaign.AddStep(-3, "EMAIL", "Soon", "Renews soon", null);
        campaign.AddStep(0, "EMAIL", "Due", "Please pay", "wa-0");
        campaign.AddStep(7, "AUTO_CHARGE", null, null, null);

        var capturedAt = new DateTime(2026, 8, 16, 4, 0, 0, DateTimeKind.Utc);
        var snapshot = DunningCampaignSnapshot.From(campaign, capturedAt);

        snapshot.Version.Should().Be(1);
        snapshot.CampaignId.Should().Be(campaign.Id);
        snapshot.CapturedAt.Should().Be(capturedAt);
        snapshot.Name.Should().Be("Standard Recovery Strategy");
        snapshot.FinalAction.Should().Be("CANCEL");
        snapshot.GracePeriodDays.Should().Be(7);
        snapshot.Steps.Select(s => s.DayOffset).Should().Equal(-3, 0, 7);

        var live = campaign.Steps.OrderBy(s => s.DayOffset).ToList();
        for (var i = 0; i < live.Count; i++)
        {
            snapshot.Steps[i].Id.Should().Be(live[i].Id);
            snapshot.Steps[i].DayOffset.Should().Be(live[i].DayOffset);
            snapshot.Steps[i].ActionType.Should().Be(live[i].ActionType);
            snapshot.Steps[i].Subject.Should().Be(live[i].Subject);
            snapshot.Steps[i].EmailBody.Should().Be(live[i].EmailBody);
            snapshot.Steps[i].WhatsAppBody.Should().Be(live[i].WhatsAppBody);
        }
    }

    [Test]
    public void Serialize_ThenTryParse_RoundTripsV1()
    {
        var campaign = new DunningCampaign(Guid.CreateVersion7(), "Round trip", "SUSPEND", gracePeriodDays: 14);
        campaign.AddStep(0, "EMAIL", "Subj", "Body", "wa");
        var snapshot = DunningCampaignSnapshot.From(
            campaign,
            new DateTime(2026, 8, 16, 4, 0, 0, DateTimeKind.Utc));

        var json = snapshot.Serialize();
        json.Should().Contain("\"v\":1");
        json.Should().Contain("\"campaign_id\"");
        json.Should().Contain("\"grace_period_days\"");
        json.Should().Contain("\"day_offset\"");

        var parsed = DunningCampaignSnapshot.TryParse(json);
        parsed.Should().NotBeNull();
        parsed!.Version.Should().Be(1);
        parsed.CampaignId.Should().Be(snapshot.CampaignId);
        parsed.CapturedAt.Should().Be(snapshot.CapturedAt);
        parsed.Name.Should().Be(snapshot.Name);
        parsed.FinalAction.Should().Be(snapshot.FinalAction);
        parsed.GracePeriodDays.Should().Be(snapshot.GracePeriodDays);
        parsed.Steps.Should().HaveCount(1);
        parsed.Steps[0].Should().Be(snapshot.Steps[0]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-json")]
    [TestCase("{\"v\":2,\"campaign_id\":\"018f0000-0000-7000-8000-000000000001\"}")]
    [TestCase("{\"v\":1}")]
    public void TryParse_EmptyGarbageOrUnknownVersion_ReturnsNull(string? json)
    {
        var act = () => DunningCampaignSnapshot.TryParse(json);
        act.Should().NotThrow();
        DunningCampaignSnapshot.TryParse(json).Should().BeNull();
    }
}
