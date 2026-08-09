using System;
using System.IO;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// L-05 / R14: CommerceDocumentLookup draft session load must not JOIN crm.
/// Customer name/email come from ICrmQueryService; commerce SQL is session-only.
/// </summary>
[TestFixture]
public class CommerceDocumentLookupBoundaryTests
{
    [Test]
    public void CommerceDocumentLookup_Has_No_Crm_Schema_Sql()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Commerce", "Infrastructure", "Services", "CommerceDocumentLookup.cs"));

        Assert.That(File.Exists(path), Is.True, $"Missing path: {path}");

        var text = File.ReadAllText(path);

        // Foreign schemas (verbatim SQL uses commerce.""Table"").
        Assert.That(text, Does.Not.Contain("crm.\""), "Must not embed crm.* SQL (L-05).");
        Assert.That(text, Does.Not.Contain("ClientProfiles"), "Must not reference ClientProfiles table.");
        Assert.That(text, Does.Not.Contain("JOIN crm"), "Must not JOIN crm schema.");

        // Still commerce-only for session + transaction log loads (verbatim "" inside @"" strings).
        Assert.That(text, Does.Contain("commerce.\"\"CheckoutSessions\"\""));
        Assert.That(text, Does.Contain("commerce.\"\"TransactionLogs\"\""));
        Assert.That(text, Does.Contain("ICrmQueryService"));
        Assert.That(text, Does.Contain("GetClientProfileAsync"));
        Assert.That(text, Does.Contain("ClientProfileId"));
    }
}
