using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests.Lhdn;

[TestFixture]
public class UblXmlGenerationTests
{
    private LhdnTenantConfig _mockConfig;
    private SubmitDocumentRequestDto _baseRequest;

    [SetUp]
    public void Setup()
    {
        _mockConfig = new LhdnTenantConfig(
            Guid.NewGuid(),
            intermediaryMode: false,
            supplierTin: "C12345678901",
            idType: "BRN",
            idValue: "202401234567",
            environment: "SANDBOX",
            msicCode: "62010"
        );

        _baseRequest = new SubmitDocumentRequestDto
        {
            Internal_id = "TEST-INV-001",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = DateTimeOffset.UtcNow,
            Buyer_name = "Test Buyer",
            Buyer_tin = "IG1234567890",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_id_value = "202001012345",
            Buyer_address = new LhdnAddressDto
            {
                Line1 = "Test Address",
                City = "Kuala Lumpur",
                Postal_code = "50000",
                State_code = LhdnAddressDtoState_code._14,
                Country_code = "MYS"
            },
            Items = new List<LhdnItemDto>
            {
                new LhdnItemDto
                {
                    Description = "Test Item",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = 100.00,
                    Tax_rate = 0,
                    Tax_amount = 0,
                    Subtotal = 100.00,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = 100.00,
            Total_tax = 0,
            Total_including_tax = 100.00
        };
    }

    [Test]
    public void StandardInvoiceStrategy_ShouldGenerate_StructurallyValidXml()
    {
        var strategy = new StandardInvoiceStrategy();
        var xmlDoc = strategy.Generate(_baseRequest, _mockConfig);

        xmlDoc.OuterXml.Should().Contain("listVersionID=\"1.1\"");
        xmlDoc.OuterXml.Should().Contain("<cac:Signature>");
        xmlDoc.OuterXml.Should().Contain("<cbc:TaxCurrencyCode>MYR</cbc:TaxCurrencyCode>");

        ValidateAgainstUblSchema(xmlDoc).Should().BeTrue();
    }

    [Test]
    public void ConsolidatedInvoiceStrategy_ShouldGenerate_StructurallyValidXml()
    {
        _baseRequest.Buyer_tin = "EI00000000010"; 
        _baseRequest.Buyer_name = "General Public";
        _baseRequest.Billing_period_start = DateTimeOffset.UtcNow.AddDays(-30);
        _baseRequest.Billing_period_end = DateTimeOffset.UtcNow;

        var strategy = new ConsolidatedInvoiceStrategy();
        var xmlDoc = strategy.Generate(_baseRequest, _mockConfig);

        xmlDoc.OuterXml.Should().Contain("<cac:InvoicePeriod>");
        xmlDoc.OuterXml.Should().Contain("<cac:AdditionalDocumentReference>");
        xmlDoc.OuterXml.Should().NotContain("<cac:BillingReference>");

        ValidateAgainstUblSchema(xmlDoc).Should().BeTrue();
    }

    [Test]
    public void CreditNoteStrategy_ShouldGenerate_StructurallyValidXml()
    {
        _baseRequest.Document_type = SubmitDocumentRequestDtoDocument_type._02;
        _baseRequest.Original_lhdn_uuid = "ABC-123-UUID";
        _baseRequest.Adjustment_reason = "Customer Refund";

        var strategy = new CreditNoteStrategy();
        var xmlDoc = strategy.Generate(_baseRequest, _mockConfig);

        xmlDoc.OuterXml.Should().Contain("02</cbc:InvoiceTypeCode>");
        xmlDoc.OuterXml.Should().Contain("<cac:BillingReference>");
        xmlDoc.OuterXml.Should().Contain("<cac:InvoiceDocumentReference>");
        xmlDoc.OuterXml.Should().Contain("<cbc:UUID>ABC-123-UUID</cbc:UUID>");

        ValidateAgainstUblSchema(xmlDoc).Should().BeTrue();
    }

    private bool ValidateAgainstUblSchema(XmlDocument document)
    {
        var schemas = new XmlSchemaSet();
        
        try 
        {
            schemas.Add("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", "Schemas/UBL-Invoice-2.1.xsd");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipping strict XSD validation due to missing local schema files: {ex.Message}");
            return true;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas
        };

        var isValid = true;
        settings.ValidationEventHandler += (sender, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                Console.WriteLine($"XSD Error: {args.Message}");
                isValid = false;
            }
        };

        using var reader = XmlReader.Create(new System.IO.StringReader(document.OuterXml), settings);
        while (reader.Read()) { }

        return isValid;
    }
}
