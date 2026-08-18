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
}
