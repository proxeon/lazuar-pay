using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Models;
using Modules.Lhdn.Infrastructure.Serialization;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests.Lhdn;

[TestFixture]
public class UblXmlGenerationTests
{
    private LhdnTenantConfig _mockConfig = null!;
    private SubmitDocumentRequestDto _baseRequest = null!;

    [SetUp]
    public void Setup()
    {
        _mockConfig = new LhdnTenantConfig(
            organizationId: Guid.NewGuid(),
            intermediaryMode: true,
            supplierTin: "C1234567890",
            idType: "BRN",
            idValue: "202401234567",
            environment: "SANDBOX",
            msicCode: "62010"
        );

        _baseRequest = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-001",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = DateTimeOffset.UtcNow,
            Buyer_name = "Acme Corp",
            Buyer_tin = "C9876543210",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "202001234567",
            Buyer_address = new LhdnAddressDto
            {
                Line1 = "123 Buyer Street",
                City = "Kuala Lumpur",
                Postal_code = "50000",
                State_code = LhdnAddressDtoState_code._14,
                Country_code = "MYS"
            },
            Items = new List<LhdnItemDto>
            {
                new LhdnItemDto
                {
                    Description = "Software License",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = 1000,
                    Tax_rate = 0,
                    Tax_amount = 0,
                    Subtotal = 1000,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = 1000,
            Total_tax = 0,
            Total_including_tax = 1000
        };
    }

    [Test]
    public void StandardInvoiceStrategy_ShouldGenerateValidJson()
    {
        var strategy = new StandardInvoiceStrategy();
        
        var result = strategy.Generate(_baseRequest, _mockConfig, "1.1");
        var json = JsonSerializer.Serialize(result, LhdnJsonOptions.Instance);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("C1234567890"); // Supplier TIN
        json.Should().Contain("C9876543210"); // Buyer TIN
        json.Should().Contain("\"_\":\"01\",\"listVersionID\":\"1.1\""); // Invoice type code
    }

    [Test]
    public void ConsolidatedInvoiceStrategy_ShouldForceGeneralPublicDefaults()
    {
        var strategy = new ConsolidatedInvoiceStrategy();
        
        // Even if a buyer TIN is passed, B2C strategy should override it
        var result = strategy.Generate(_baseRequest, _mockConfig, "1.1");
        var json = JsonSerializer.Serialize(result, LhdnJsonOptions.Instance);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("EI00000000010"); // General Public TIN
        json.Should().Contain("General Public");
    }

    [Test]
    public void CreditNoteStrategy_ShouldSetCorrectTypeCode()
    {
        var strategy = new CreditNoteStrategy();
        
        _baseRequest.Document_type = SubmitDocumentRequestDtoDocument_type._02;
        _baseRequest.Original_lhdn_uuid = "PREVIOUS-UUID-1234";

        var result = strategy.Generate(_baseRequest, _mockConfig, "1.1");
        var json = JsonSerializer.Serialize(result, LhdnJsonOptions.Instance);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"_\":\"02\",\"listVersionID\":\"1.1\""); // Credit Note type code
        json.Should().Contain("PREVIOUS-UUID-1234");
    }
}
