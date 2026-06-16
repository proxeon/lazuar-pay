using System;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn.Strategies;

[TestFixture]
public class UblStrategyTests
{
    private LhdnTenantConfig _dummyConfig;
    private readonly DateTimeOffset _testDate = DateTimeOffset.Parse("2026-06-16T10:23:09Z");

    [SetUp]
    public void Setup()
    {
        _dummyConfig = new LhdnTenantConfig(Guid.NewGuid(), true, "IG56848407100", "BRN", "201901234567");
    }

    // Normalizes line endings to prevent cross-platform (Mac vs Windows) test flakiness
    private static string NormalizeXml(string xml) => xml.Replace("\r\n", "\n").Trim();

    [Test]
    public void StandardInvoiceStrategy_ShouldGenerate_ExactGoldenPayload()
    {
        var strategy = new StandardInvoiceStrategy();
        var request = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-B2B-TEST",
            Issue_date = _testDate
        };

        var result = strategy.Generate(request, _dummyConfig, "1.0");

        var expectedXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>INV-B2B-TEST</cbc:ID>
  <cbc:IssueDate>2026-06-16</cbc:IssueDate>
  <cbc:IssueTime>10:23:09Z</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""1.0"">01</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
  <cac:BillingReference>
    <cac:AdditionalDocumentReference>
      <cbc:ID>E12345678912</cbc:ID>
    </cac:AdditionalDocumentReference>
  </cac:BillingReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>CustomsImportForm</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>ASEAN-Australia-New Zealand FTA (AANZFTA)</cbc:ID>
    <cbc:DocumentType>FreeTradeAgreement</cbc:DocumentType>
    <cbc:DocumentDescription>Sample Description</cbc:DocumentDescription>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>K2</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>CIF</cbc:ID>
  </cac:AdditionalDocumentReference>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cbc:IndustryClassificationCode>62010</cbc:IndustryClassificationCode>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">IG56848407100</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""NRIC"">990806086487</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>CHEMOR</cbc:CityName>
        <cbc:PostalZone>31200</cbc:PostalZone>
        <cbc:CountrySubentityCode>08</cbc:CountrySubentityCode>
        <cac:AddressLine>
            <cbc:Line>NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>AXXX_XXXXRI</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
        <cbc:ElectronicMail>akmal.fir010@gmail.com</cbc:ElectronicMail>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">IG56848407100</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""NRIC"">990806086487</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>CHEMOR</cbc:CityName>
        <cbc:PostalZone>31200</cbc:PostalZone>
        <cbc:CountrySubentityCode>08</cbc:CountrySubentityCode>
        <cac:AddressLine>
          <cbc:Line>NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>AXXX_XXXXRI</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxCategory>
            <cbc:ID>06</cbc:ID>
            <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
            <cac:TaxScheme>
                <cbc:ID>OTH</cbc:ID>
            </cac:TaxScheme>
        </cac:TaxCategory>
    </cac:TaxSubtotal>
  </cac:TaxTotal>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""MYR"">1000.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cac:TaxTotal>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
            <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
            <cac:TaxCategory>
                <cbc:ID>06</cbc:ID>
                <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
                <cac:TaxScheme>
                    <cbc:ID>OTH</cbc:ID>
                </cac:TaxScheme>
            </cac:TaxCategory>
        </cac:TaxSubtotal>
    </cac:TaxTotal>
    <cac:Item>
      <cbc:Description>Software Development Service</cbc:Description>
      <cac:CommodityClassification>
          <cbc:ItemClassificationCode listID=""CLASS"">022</cbc:ItemClassificationCode>
      </cac:CommodityClassification>
      <cac:ClassifiedTaxCategory>
          <cbc:ID>06</cbc:ID>
          <cac:TaxScheme>
              <cbc:ID>OTH</cbc:ID>
          </cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""MYR"">1000.00</cbc:PriceAmount>
    </cac:Price>
    <cac:ItemPriceExtension>
        <cbc:Amount currencyID=""MYR"">1000.00</cbc:Amount>
    </cac:ItemPriceExtension>
  </cac:InvoiceLine>
</Invoice>";

        NormalizeXml(result).Should().Be(NormalizeXml(expectedXml));
    }

    [Test]
    public void CreditNoteStrategy_ShouldGenerate_ExactGoldenPayload()
    {
        var strategy = new CreditNoteStrategy();
        var request = new SubmitDocumentRequestDto
        {
            Internal_id = "CN-TEST",
            Issue_date = _testDate,
            Original_lhdn_uuid = "ABC-123-UUID"
        };

        var result = strategy.Generate(request, _dummyConfig, "1.0");

        var expectedXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>CN-TEST</cbc:ID>
  <cbc:IssueDate>2026-06-16</cbc:IssueDate>
  <cbc:IssueTime>10:23:09Z</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""1.0"">02</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
  <cac:BillingReference>
    <cac:InvoiceDocumentReference>
        <cbc:ID>NA</cbc:ID>
        <cbc:UUID>ABC-123-UUID</cbc:UUID>
    </cac:InvoiceDocumentReference>
  </cac:BillingReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>CustomsImportForm</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>ASEAN-Australia-New Zealand FTA (AANZFTA)</cbc:ID>
    <cbc:DocumentType>FreeTradeAgreement</cbc:DocumentType>
    <cbc:DocumentDescription>Sample Description</cbc:DocumentDescription>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>K2</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>CIF</cbc:ID>
  </cac:AdditionalDocumentReference>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cbc:IndustryClassificationCode>62010</cbc:IndustryClassificationCode>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">IG56848407100</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""NRIC"">990806086487</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>CHEMOR</cbc:CityName>
        <cbc:PostalZone>31200</cbc:PostalZone>
        <cbc:CountrySubentityCode>08</cbc:CountrySubentityCode>
        <cac:AddressLine>
            <cbc:Line>NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>AXXX_XXXXRI</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
        <cbc:ElectronicMail>akmal.fir010@gmail.com</cbc:ElectronicMail>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">IG56848407100</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""NRIC"">990806086487</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>CHEMOR</cbc:CityName>
        <cbc:PostalZone>31200</cbc:PostalZone>
        <cbc:CountrySubentityCode>08</cbc:CountrySubentityCode>
        <cac:AddressLine>
          <cbc:Line>NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>AXXX_XXXXRI</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxCategory>
            <cbc:ID>06</cbc:ID>
            <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
            <cac:TaxScheme>
                <cbc:ID>OTH</cbc:ID>
            </cac:TaxScheme>
        </cac:TaxCategory>
    </cac:TaxSubtotal>
  </cac:TaxTotal>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""MYR"">1000.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cac:TaxTotal>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
            <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
            <cac:TaxCategory>
                <cbc:ID>06</cbc:ID>
                <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
                <cac:TaxScheme>
                    <cbc:ID>OTH</cbc:ID>
                </cac:TaxScheme>
            </cac:TaxCategory>
        </cac:TaxSubtotal>
    </cac:TaxTotal>
    <cac:Item>
      <cbc:Description>Refund Adjustment</cbc:Description>
      <cac:CommodityClassification>
          <cbc:ItemClassificationCode listID=""CLASS"">022</cbc:ItemClassificationCode>
      </cac:CommodityClassification>
      <cac:ClassifiedTaxCategory>
          <cbc:ID>06</cbc:ID>
          <cac:TaxScheme>
              <cbc:ID>OTH</cbc:ID>
          </cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""MYR"">1000.00</cbc:PriceAmount>
    </cac:Price>
    <cac:ItemPriceExtension>
        <cbc:Amount currencyID=""MYR"">1000.00</cbc:Amount>
    </cac:ItemPriceExtension>
  </cac:InvoiceLine>
</Invoice>";

        NormalizeXml(result).Should().Be(NormalizeXml(expectedXml));
    }

    [Test]
    public void ConsolidatedInvoiceStrategy_ShouldGenerate_ExactGoldenPayload()
    {
        var strategy = new ConsolidatedInvoiceStrategy();
        var request = new SubmitDocumentRequestDto
        {
            Internal_id = "B2C-TEST",
            Issue_date = _testDate,
            Billing_period_start = _testDate.AddDays(-30),
            Billing_period_end = _testDate
        };

        var result = strategy.Generate(request, _dummyConfig, "1.0");

        var expectedXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>B2C-TEST</cbc:ID>
  <cbc:IssueDate>2026-06-16</cbc:IssueDate>
  <cbc:IssueTime>10:23:09Z</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""1.0"">01</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
  <cac:InvoicePeriod>
    <cbc:StartDate>2026-05-17</cbc:StartDate>
    <cbc:EndDate>2026-06-16</cbc:EndDate>
    <cbc:Description>Monthly</cbc:Description>
  </cac:InvoicePeriod>
  <cac:BillingReference>
    <cac:AdditionalDocumentReference>
      <cbc:ID>E12345678912</cbc:ID>
    </cac:AdditionalDocumentReference>
  </cac:BillingReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>CustomsImportForm</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>ASEAN-Australia-New Zealand FTA (AANZFTA)</cbc:ID>
    <cbc:DocumentType>FreeTradeAgreement</cbc:DocumentType>
    <cbc:DocumentDescription>Sample Description</cbc:DocumentDescription>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>E12345678912</cbc:ID>
    <cbc:DocumentType>K2</cbc:DocumentType>
  </cac:AdditionalDocumentReference>
  <cac:AdditionalDocumentReference>
    <cbc:ID>CIF</cbc:ID>
  </cac:AdditionalDocumentReference>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cbc:IndustryClassificationCode>62010</cbc:IndustryClassificationCode>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">IG56848407100</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""NRIC"">990806086487</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>CHEMOR</cbc:CityName>
        <cbc:PostalZone>31200</cbc:PostalZone>
        <cbc:CountrySubentityCode>08</cbc:CountrySubentityCode>
        <cac:AddressLine>
            <cbc:Line>NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>AXXX_XXXXRI</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
        <cbc:ElectronicMail>akmal.fir010@gmail.com</cbc:ElectronicMail>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">EI00000000010</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""BRN"">NA</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>NA</cbc:CityName>
        <cbc:PostalZone>00000</cbc:PostalZone>
        <cbc:CountrySubentityCode>17</cbc:CountrySubentityCode>
        <cac:AddressLine>
          <cbc:Line>NA</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>General Public</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>01160714390</cbc:Telephone>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxCategory>
            <cbc:ID>06</cbc:ID>
            <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
            <cac:TaxScheme>
                <cbc:ID>OTH</cbc:ID>
            </cac:TaxScheme>
        </cac:TaxCategory>
    </cac:TaxSubtotal>
  </cac:TaxTotal>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""MYR"">1000.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""MYR"">1000.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""MYR"">1000.00</cbc:LineExtensionAmount>
    <cac:TaxTotal>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID=""MYR"">1000.00</cbc:TaxableAmount>
            <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
            <cac:TaxCategory>
                <cbc:ID>06</cbc:ID>
                <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
                <cac:TaxScheme>
                    <cbc:ID>OTH</cbc:ID>
                </cac:TaxScheme>
            </cac:TaxCategory>
        </cac:TaxSubtotal>
    </cac:TaxTotal>
    <cac:Item>
      <cbc:Description>Software Development Service</cbc:Description>
      <cac:CommodityClassification>
          <cbc:ItemClassificationCode listID=""CLASS"">022</cbc:ItemClassificationCode>
      </cac:CommodityClassification>
      <cac:ClassifiedTaxCategory>
          <cbc:ID>06</cbc:ID>
          <cac:TaxScheme>
              <cbc:ID>OTH</cbc:ID>
          </cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""MYR"">1000.00</cbc:PriceAmount>
    </cac:Price>
    <cac:ItemPriceExtension>
        <cbc:Amount currencyID=""MYR"">1000.00</cbc:Amount>
    </cac:ItemPriceExtension>
  </cac:InvoiceLine>
</Invoice>";

        NormalizeXml(result).Should().Be(NormalizeXml(expectedXml));
    }
}
