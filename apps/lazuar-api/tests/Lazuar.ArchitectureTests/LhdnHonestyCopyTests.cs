using System.IO;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class LhdnHonestyCopyTests
{
    [Test]
    public void CreditNotesPage_TitleDoesNotClaimDebitNotes()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..",
            "lazuar-ops", "src", "modules", "invoicing", "pages", "CreditNotesPage.tsx"));
        Assert.That(File.Exists(path), Is.True, path);
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("title=\"Credit Notes\""));
        Assert.That(text, Does.Not.Contain("Credit & Debit Notes"));
    }

    [Test]
    public void LhdnReadme_MarksUnusedTypesAsStrategyOnly()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Lhdn", "README.md"));
        Assert.That(File.Exists(path), Is.True, path);
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("Strategy-only"));
        Assert.That(text, Does.Contain("no production publisher"));
        Assert.That(text, Does.Not.Contain("✅ **Debit Note (03):** Supported"));
        Assert.That(text, Does.Not.Contain("✅ **Self-Billed Invoice (11):** Supported"));
    }

    [Test]
    public void InvoiceIssuedHandler_NamesLiveB2bHook_NotMissingTypes()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Lhdn", "Infrastructure", "EventHandlers",
            "InvoiceIssuedIntegrationEventHandler.cs"));
        Assert.That(File.Exists(path), Is.True, path);
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("B2bTaxInvoiceRequested"));
        Assert.That(text, Does.Not.Contain("B2bSaleSubmitHandler"));
        Assert.That(text, Does.Not.Contain("B2bSaleReadyForEinvoice"));
    }

    [Test]
    public void LhdnReadme_NamesJsonSigner_AndDoesNotClaimXades()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Lhdn", "README.md"));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("JsonUblDocumentSigner"));
        Assert.That(text, Does.Contain("unsigned XML UBL **1.0**"));
        Assert.That(text, Does.Contain("XML XAdES / XML-DSig is not used"));
        Assert.That(text, Does.Not.Contain("Signatures (XMLDSig/XAdES):** Unimplemented"));
    }

    [Test]
    public void Wave2StationeryDoneFile_DoesNotClaimTinNotOnFile()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..", "..",
            "plans", "007-feats", "impl", "W2-LP-107-done.md"));
        Assert.That(File.Exists(path), Is.True, path);
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("omits** the TIN line"));
        Assert.That(text, Does.Contain("never “TIN not on file”"));
    }

    [Test]
    public void QuotesPage_DoesNotTrackAdHocInvoices()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..",
            "lazuar-ops", "src", "modules", "invoicing", "pages", "QuotesPage.tsx"));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("Tracking quotes"));
        Assert.That(text, Does.Not.Contain("Tracking ad-hoc invoices"));
    }

    [Test]
    public void LhdnSandboxValidHonesty_StillNotCaptured()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..", "..",
            "docs", "honesty", "lhdn-sandbox-valid.md"));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("**Status: not captured.**"));
        Assert.That(text, Does.Contain("not** MyInvois ACCEPT"));
        Assert.That(text, Does.Not.Contain("overallStatus=Valid captured"));
    }
}

[TestFixture]
public class OneRoleHonestyTests
{
    [Test]
    public void RegisterResponse_UsesJwtRole_NotHardcodedAdmin()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "One", "Infrastructure", "Endpoints", "AuthEndpoints.cs"));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("user!.IsSystemAdmin ? \"SUPER_ADMIN\" : \"CLIENT\""));
        Assert.That(text, Does.Not.Contain("Role = \"ADMIN\""));
    }

    [Test]
    public void OneReadme_StaffRolesAreNotClient()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "One", "README.md"));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("ADMIN`, `MEMBER`, `VIEWER`"));
        Assert.That(text, Does.Not.Contain("may grant a `TenantMembership` with the `CLIENT` role"));
    }
}
