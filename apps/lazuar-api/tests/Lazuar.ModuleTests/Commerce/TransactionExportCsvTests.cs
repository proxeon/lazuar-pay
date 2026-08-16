using System;
using System.Text;
using FluentAssertions;
using Modules.Commerce.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class TransactionExportCsvTests
{
    [Test]
    public void Empty_Is_Header_Only()
    {
        var csv = TransactionExportCsv.Build([]);
        csv.Should().StartWith(TransactionExportCsv.Header);
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
    }

    [Test]
    public void Quotes_Email_With_Comma_And_Writes_Iso_Utc()
    {
        var created = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var csv = TransactionExportCsv.Build(
        [
            new TransactionExportCsv.Row(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                created,
                "CONFIRMED",
                10.5m,
                0m,
                10.5m,
                "MYR",
                "Ada",
                "ada,pay@example.com",
                "Plan",
                "STRIPE",
                "pi_abc")
        ]);

        csv.Should().Contain("\"ada,pay@example.com\"");
        csv.Should().Contain("2026-08-01T12:00:00.0000000Z");
        csv.Should().Contain("pi_abc");
    }

    [Test]
    public void Utf8_Bom_Is_Present()
    {
        var bytes = TransactionExportCsv.ToUtf8Bom(TransactionExportCsv.Header + "\n");
        bytes.Should().StartWith(Encoding.UTF8.GetPreamble());
    }
}
