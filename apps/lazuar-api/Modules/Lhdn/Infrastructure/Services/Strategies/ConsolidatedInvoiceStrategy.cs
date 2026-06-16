using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class ConsolidatedInvoiceStrategy : IUblDocumentStrategy
{
    public string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var dateStr = issueDate.ToString("yyyy-MM-dd");
        var timeStr = issueDate.ToString("HH:mm:ssZ");

        var startDate = request.Billing_period_start?.UtcDateTime ?? issueDate.AddDays(-30);
        var endDate = request.Billing_period_end?.UtcDateTime ?? issueDate;
        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>{request.Internal_id}</cbc:ID>
  <cbc:IssueDate>{dateStr}</cbc:IssueDate>
  <cbc:IssueTime>{timeStr}</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""{documentVersion}"">01</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
  <cac:InvoicePeriod>
    <cbc:StartDate>{startStr}</cbc:StartDate>
    <cbc:EndDate>{endStr}</cbc:EndDate>
    <cbc:Description>Consolidated Invoice</cbc:Description>
  </cac:InvoicePeriod>
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
        <cbc:Telephone>+60123456789</cbc:Telephone>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID=""MYR"">3000.00</cbc:TaxableAmount>
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
    <cbc:LineExtensionAmount currencyID=""MYR"">3000.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""MYR"">3000.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""MYR"">3000.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""MYR"">3000.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""MYR"">3000.00</cbc:LineExtensionAmount>
    <cac:TaxTotal>
        <cbc:TaxAmount currencyID=""MYR"">0.00</cbc:TaxAmount>
        <cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID=""MYR"">3000.00</cbc:TaxableAmount>
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
      <cbc:Description>Consolidated Receipts</cbc:Description>
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
      <cbc:PriceAmount currencyID=""MYR"">3000.00</cbc:PriceAmount>
    </cac:Price>
    <cac:ItemPriceExtension>
        <cbc:Amount currencyID=""MYR"">3000.00</cbc:Amount>
    </cac:ItemPriceExtension>
  </cac:InvoiceLine>
</Invoice>";
    }
}
