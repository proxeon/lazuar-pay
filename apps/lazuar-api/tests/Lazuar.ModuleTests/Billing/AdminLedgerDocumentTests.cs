using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing;

[TestFixture]
public class AdminLedgerDocumentTests
{
    [Test]
    public void DocumentDownload_RequiresTenantHeader()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Billing", "Infrastructure", "Endpoints", "AdminLedgerEndpoints.cs"));
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().Contain("TypedResults.NotFound()");
        text.Should().Contain("e.OrganizationId == ctx.TenantId");
    }
}
