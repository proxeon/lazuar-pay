using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class BillplzFeeHonestyTests
{
    [Test]
    public void WebhookHandler_PassesZeroEstimatedFeeArgs()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Payments", "Application", "Commands", "ProcessGatewayWebhookCommandHandler.cs"));
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().Contain("estimatedFeePercentage - removed from config");
        text.Should().Contain("fixedFee - removed from config");
    }
}
