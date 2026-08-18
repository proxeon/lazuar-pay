using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class ViewModelMapperTests
{
    [Test]
    public void MissingAddressAndPhone_UsesState17_AndOmitsDummyPhone()
    {
        var org = System.Guid.CreateVersion7();
        var config = new LhdnTenantConfig(org, false, "C12345678901", "BRN", "20200101");
        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-1",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = System.DateTimeOffset.UtcNow,
            Buyer_name = "Buyer",
            Buyer_tin = "C55555555555",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "202001099999",
            Total_excluding_tax = 100,
            Total_tax = 0,
            Total_including_tax = 100
        };

        var model = ViewModelMapper.MapToViewModel(payload, config, "1.0");

        model.Buyer.StateCode.Should().Be("17");
        model.Supplier.StateCode.Should().Be("17");
        model.Buyer.Phone.Should().BeEmpty();
        model.Supplier.Phone.Should().BeEmpty();
        model.BillingPeriodDescription.Should().Be("One-time");
    }

    [Test]
    public void ConsolidatedGeneralPublic_PeriodIsMonthly()
    {
        var org = System.Guid.CreateVersion7();
        var config = new LhdnTenantConfig(org, false, "C12345678901", "BRN", "20200101");
        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = "B2C-CONS-202607-x",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = System.DateTimeOffset.UtcNow,
            Buyer_name = "General Public",
            Buyer_tin = "EI00000000010",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "NA",
            Total_excluding_tax = 100,
            Total_tax = 0,
            Total_including_tax = 100
        };

        var model = ViewModelMapper.MapToViewModel(payload, config, "1.0");
        model.BillingPeriodDescription.Should().Be("Monthly");
    }
}
