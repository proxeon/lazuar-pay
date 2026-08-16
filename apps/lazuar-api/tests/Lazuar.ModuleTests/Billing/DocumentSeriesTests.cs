using System;
using Modules.Billing.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing;

[TestFixture]
public class DocumentSeriesTests
{
    [Test]
    public void Prefix_BakesYearIntoSeries()
    {
        var utc = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(DocumentSeries.ReceiptPrefix(utc), Is.EqualTo("RCPT-2026"));
        Assert.That(DocumentSeries.QuotePrefix(utc), Is.EqualTo("QT-2026"));
        Assert.That(DocumentSeries.InvoicePrefix(utc), Is.EqualTo("INV-2026"));
        Assert.That(DocumentSeries.CreditNotePrefix(utc), Is.EqualTo("CN-2026"));
    }

    [Test]
    public void CustomerFacingNumber_NeverUsesRawUuid()
    {
        var uuid = Guid.CreateVersion7().ToString();
        Assert.That(DocumentSeries.CustomerFacingNumber("RCPT-2026-00001", uuid), Is.EqualTo("RCPT-2026-00001"));
        Assert.That(DocumentSeries.CustomerFacingNumber(null, uuid), Is.EqualTo("PENDING"));
        Assert.That(DocumentSeries.CustomerFacingNumber(null, "INV-2026-00003"), Is.EqualTo("INV-2026-00003"));
    }
}
