using System.Linq;
using System.Security;
using System.Text;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class StandardInvoiceStrategy : IUblDocumentStrategy
{
    public string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var dateStr = issueDate.ToString("yyyy-MM-dd");
        var timeStr = issueDate.ToString("HH:mm:ssZ");

        var isSelfBilledImportation = request.Buyer_tin == config.SupplierTin;
        var additionalDocRefs = isSelfBilledImportation ? $@"
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
  </cac:AdditionalDocumentReference>" : "";

        var invoiceLines = new StringBuilder();
        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items.ElementAt(i);
            var taxCode = item.Tax_type_code.ToString().TrimStart('_');
            var classCode = item.Classification_code ?? "022";

            invoiceLines.Append($@"
  <cac:InvoiceLine>
    <cbc:ID>{i + 1}</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">{item.Quantity:0.00}</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""MYR"">{item.Subtotal:0.00}</cbc:LineExtensionAmount>
    <cac:TaxTotal>
        <cbc:TaxAmount currencyID=""MYR"">{item.Tax_amount:0.00}</cbc:TaxAmount>
        <cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID=""MYR"">{item.Subtotal:0.00}</cbc:TaxableAmount>
            <cbc:TaxAmount currencyID=""MYR"">{item.Tax_amount:0.00}</cbc:TaxAmount>
            <cac:TaxCategory>
                <cbc:ID>{taxCode}</cbc:ID>
                <cbc:TaxExemptionReason>Not subject to tax</cbc:TaxExemptionReason>
                <cac:TaxScheme>
                    <cbc:ID>OTH</cbc:ID>
                </cac:TaxScheme>
            </cac:TaxCategory>
        </cac:TaxSubtotal>
    </cac:TaxTotal>
    <cac:Item>
      <cbc:Description>{SecurityElement.Escape(item.Description ?? "Item")}</cbc:Description>
      <cac:CommodityClassification>
          <cbc:ItemClassificationCode listID=""CLASS"">{classCode}</cbc:ItemClassificationCode>
      </cac:CommodityClassification>
      <cac:ClassifiedTaxCategory>
          <cbc:ID>{taxCode}</cbc:ID>
          <cac:TaxScheme>
              <cbc:ID>OTH</cbc:ID>
          </cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""MYR"">{item.Unit_price:0.00}</cbc:PriceAmount>
    </cac:Price>
    <cac:ItemPriceExtension>
        <cbc:Amount currencyID=""MYR"">{item.Subtotal:0.00}</cbc:Amount>
    </cac:ItemPriceExtension>
  </cac:InvoiceLine>");
        }

        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
  xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
  xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
  <cbc:ID>{request.Internal_id}</cbc:ID>
  <cbc:IssueDate>{dateStr}</cbc:IssueDate>
  <cbc:IssueTime>{timeStr}</cbc:IssueTime>
  <cbc:InvoiceTypeCode listVersionID=""{documentVersion}"">01</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>{additionalDocRefs}
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cbc:IndustryClassificationCode>{config.MsicCode ?? "00000"}</cbc:IndustryClassificationCode>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">{config.SupplierTin ?? "NA"}</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""{config.IdType ?? "BRN"}"">{config.IdValue ?? "NA"}</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>Kuala Lumpur</cbc:CityName>
        <cbc:PostalZone>50480</cbc:PostalZone>
        <cbc:CountrySubentityCode>14</cbc:CountrySubentityCode>
        <cac:AddressLine>
            <cbc:Line>Lot 66</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>MYS</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>System Supplier</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>+60123456789</cbc:Telephone>
        <cbc:ElectronicMail>admin@lazuar.com</cbc:ElectronicMail>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""TIN"">{request.Buyer_tin ?? "NA"}</cbc:ID>
      </cac:PartyIdentification>
      <cac:PartyIdentification>
        <cbc:ID schemeID=""{request.Buyer_id_type}"">{request.Buyer_id_value ?? "NA"}</cbc:ID>
      </cac:PartyIdentification>
      <cac:PostalAddress>
        <cbc:CityName>{SecurityElement.Escape(request.Buyer_address.City ?? "NA")}</cbc:CityName>
        <cbc:PostalZone>{request.Buyer_address.Postal_code ?? "00000"}</cbc:PostalZone>
        <cbc:CountrySubentityCode>{request.Buyer_address.State_code.ToString().TrimStart('_')}</cbc:CountrySubentityCode>
        <cac:AddressLine>
          <cbc:Line>{SecurityElement.Escape(request.Buyer_address.Line1 ?? "NA")}</cbc:Line>
        </cac:AddressLine>
        <cac:Country>
          <cbc:IdentificationCode>{request.Buyer_address.Country_code ?? "MYS"}</cbc:IdentificationCode>
        </cac:Country>
      </cac:PostalAddress>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>{SecurityElement.Escape(request.Buyer_name ?? "NA")}</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:Contact>
        <cbc:Telephone>{request.Buyer_phone ?? "+60123456789"}</cbc:Telephone>
      </cac:Contact>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""MYR"">{request.Total_tax:0.00}</cbc:TaxAmount>
    <cac:TaxSubtotal>
        <cbc:TaxableAmount currencyID=""MYR"">{request.Total_excluding_tax:0.00}</cbc:TaxableAmount>
        <cbc:TaxAmount currencyID=""MYR"">{request.Total_tax:0.00}</cbc:TaxAmount>
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
    <cbc:LineExtensionAmount currencyID=""MYR"">{request.Total_excluding_tax:0.00}</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""MYR"">{request.Total_excluding_tax:0.00}</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""MYR"">{request.Total_including_tax:0.00}</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""MYR"">{request.Total_including_tax:0.00}</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>{invoiceLines.ToString()}
</Invoice>";

        return xml;
    }
}
