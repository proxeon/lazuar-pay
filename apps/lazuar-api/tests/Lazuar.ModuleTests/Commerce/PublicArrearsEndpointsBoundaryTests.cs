using System;
using System.IO;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// L-03 / R13: public arrears update-payment must not JOIN crm/one in Commerce SQL.
/// Email and tenant slug come from ICrmQueryService + IOneQueryService.
/// </summary>
[TestFixture]
public class PublicArrearsEndpointsBoundaryTests
{
    [Test]
    public void PublicArrearsEndpoints_Has_No_Crm_Or_One_Schema_Sql()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Commerce", "Infrastructure", "Endpoints", "PublicArrearsEndpoints.cs"));

        Assert.That(File.Exists(path), Is.True, $"Missing path: {path}");

        var text = File.ReadAllText(path);

        // Foreign schemas (verbatim SQL uses commerce.""Table"" → still contains schema.")
        Assert.That(text, Does.Not.Contain("crm.\""), "Must not embed crm.* SQL (L-03).");
        Assert.That(text, Does.Not.Contain("one.\""), "Must not embed one.* SQL (L-03).");
        Assert.That(text, Does.Not.Contain("ClientProfiles"), "Must not reference ClientProfiles table.");
        Assert.That(text, Does.Not.Contain("\"Organizations\""), "Must not reference Organizations table.");

        // Still commerce-only for subscription + product load (verbatim "" inside @"" strings).
        Assert.That(text, Does.Contain("commerce.\"\"Subscriptions\"\""));
        Assert.That(text, Does.Contain("commerce.\"\"Products\"\""));
        Assert.That(text, Does.Contain("ICrmQueryService"));
        Assert.That(text, Does.Contain("IOneQueryService"));
        Assert.That(text, Does.Contain("GetClientProfileAsync"));
        Assert.That(text, Does.Contain("GetWorkspaceByIdAsync"));
    }
}
