using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing;

[TestFixture]
public class FinancialSummaryHonestyTests
{
    [Test]
    public void OpsDashboard_LabelsNetRevenue_NotCashInBank()
    {
        var dashboard = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..",
            "lazuar-ops", "src", "modules", "commerce", "pages", "DashboardPage.tsx"));
        File.Exists(dashboard).Should().BeTrue(dashboard);
        var dash = File.ReadAllText(dashboard);
        dash.Should().Contain("Net revenue (after fees & tax)");
        dash.Should().NotContain("Net Cash in Bank");

        var agent = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Billing", "Application", "Queries", "Agent", "GetFinancialHealthAgentQuery.cs"));
        File.Exists(agent).Should().BeTrue(agent);
        var agentText = File.ReadAllText(agent);
        agentText.Should().Contain("not bank cash");
        agentText.Should().NotContain("Net Cash in Bank");
    }
}
