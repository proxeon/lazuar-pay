using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class CreditNoteStrategy : IUblDocumentStrategy
{
    public string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var dateStr = issueDate.ToString("yyyy-MM-dd");
        var timeStr = issueDate.ToString("HH:mm:ssZ");

        // Dynamically resolves to "02" (Credit), "03" (Debit), or "04" (Refund)
        var docTypeCode = request.Document_type.ToString().TrimStart('_');

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>{request.Internal_id}</cbc:ID>
  <cbc:IssueDate>{dateStr}</cbc:IssueDate>
  <cbc:IssueTime>{timeStr}</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""{documentVersion}"">{docTypeCode}</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
  <cac:BillingReference>
    <cac:InvoiceDocumentReference>
        <cbc:ID>NA</cbc:ID>
        <cbc:UUID>{request.Original_lhdn_uuid}</cbc:UUID>
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
    }
}
