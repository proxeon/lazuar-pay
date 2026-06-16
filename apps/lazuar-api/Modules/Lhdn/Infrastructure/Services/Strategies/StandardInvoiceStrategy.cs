using System.Security;
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

        var buyerName = SecurityElement.Escape(request.Buyer_name ?? "NA");
        var buyerTin = request.Buyer_tin ?? "NA";
        var buyerIdType = request.Buyer_id_type.ToString();
        var buyerIdValue = request.Buyer_id_value ?? "NA";
        var buyerPhone = request.Buyer_phone ?? "+60123456789";

        // Stripped <UBLExtensions> and <cac:Signature> blocks for v1.0 testing.
        // Injected dynamic variables for IDs, Dates, and TINs.
        // Preserved all complex Delivery, Payment, and Allowance nodes exactly as per LHDN sample.
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
	xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
	xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"">
	<cbc:ID>{request.Internal_id}</cbc:ID>
	<cbc:IssueDate>{dateStr}</cbc:IssueDate>
	<cbc:IssueTime>{timeStr}</cbc:IssueTime>
	<cbc:InvoiceTypeCode listVersionID=""{documentVersion}"">01</cbc:InvoiceTypeCode>
	<cbc:DocumentCurrencyCode>MYR</cbc:DocumentCurrencyCode>
	<cbc:TaxCurrencyCode>MYR</cbc:TaxCurrencyCode>
	<cac:InvoicePeriod>
		<cbc:StartDate>2024-07-01</cbc:StartDate>
		<cbc:EndDate>2024-07-31</cbc:EndDate>
		<cbc:Description>Monthly</cbc:Description>
	</cac:InvoicePeriod>
	<cac:BillingReference>
		<cac:AdditionalDocumentReference>
			<cbc:ID>151891-1981</cbc:ID>
		</cac:AdditionalDocumentReference>
	</cac:BillingReference>
	<cac:AdditionalDocumentReference>
		<cbc:ID>L1</cbc:ID>
		<cbc:DocumentType>CustomsImportForm</cbc:DocumentType>
	</cac:AdditionalDocumentReference>
	<cac:AdditionalDocumentReference>
		<cbc:ID>FTA</cbc:ID>
		<cbc:DocumentType>FreeTradeAgreement</cbc:DocumentType>
		<cbc:DocumentDescription>Sample Description</cbc:DocumentDescription>
	</cac:AdditionalDocumentReference>
	<cac:AdditionalDocumentReference>
		<cbc:ID>L1</cbc:ID>
		<cbc:DocumentType>K2</cbc:DocumentType>
	</cac:AdditionalDocumentReference>
	<cac:AdditionalDocumentReference>
		<cbc:ID>L1</cbc:ID>
	</cac:AdditionalDocumentReference>
	<cac:AccountingSupplierParty>
		<cbc:AdditionalAccountID schemeAgencyName=""CertEX"">CPT-CCN-W-211111-KL-000002</cbc:AdditionalAccountID>
		<cac:Party>
			<cbc:IndustryClassificationCode name=""Wholesale of computer hardware, software and peripherals"">46510</cbc:IndustryClassificationCode>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""TIN"">{config.SupplierTin ?? "NA"}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""BRN"">{config.IdValue ?? "NA"}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""SST"">NA</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""TTX"">NA</cbc:ID>
			</cac:PartyIdentification>
			<cac:PostalAddress>
				<cbc:CityName>Kuala Lumpur</cbc:CityName>
				<cbc:PostalZone>50480</cbc:PostalZone>
				<cbc:CountrySubentityCode>14</cbc:CountrySubentityCode>
				<cac:AddressLine>
					<cbc:Line>Lot 66</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Bangunan Merdeka</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Persiaran Jaya</cbc:Line>
				</cac:AddressLine>
				<cac:Country>
					<cbc:IdentificationCode listID=""ISO3166-1"" listAgencyID=""6"">MYS</cbc:IdentificationCode>
				</cac:Country>
			</cac:PostalAddress>
			<cac:PartyLegalEntity>
				<cbc:RegistrationName>System Supplier</cbc:RegistrationName>
			</cac:PartyLegalEntity>
			<cac:Contact>
				<cbc:Telephone>+60123456789</cbc:Telephone>
				<cbc:ElectronicMail>supplier@email.com</cbc:ElectronicMail>
			</cac:Contact>
		</cac:Party>
	</cac:AccountingSupplierParty>
	<cac:AccountingCustomerParty>
		<cac:Party>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""TIN"">{buyerTin}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""{buyerIdType}"">{buyerIdValue}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""SST"">NA</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""TTX"">NA</cbc:ID>
			</cac:PartyIdentification>
			<cac:PostalAddress>
				<cbc:CityName>Kuala Lumpur</cbc:CityName>
				<cbc:PostalZone>50480</cbc:PostalZone>
				<cbc:CountrySubentityCode>14</cbc:CountrySubentityCode>
				<cac:AddressLine>
					<cbc:Line>Lot 66</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Bangunan Merdeka</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Persiaran Jaya</cbc:Line>
				</cac:AddressLine>
				<cac:Country>
					<cbc:IdentificationCode listID=""ISO3166-1"" listAgencyID=""6"">MYS</cbc:IdentificationCode>
				</cac:Country>
			</cac:PostalAddress>
			<cac:PartyLegalEntity>
				<cbc:RegistrationName>{buyerName}</cbc:RegistrationName>
			</cac:PartyLegalEntity>
			<cac:Contact>
				<cbc:Telephone>{buyerPhone}</cbc:Telephone>
				<cbc:ElectronicMail>buyer@email.com</cbc:ElectronicMail>
			</cac:Contact>
		</cac:Party>
	</cac:AccountingCustomerParty>
	<cac:Delivery>
		<cac:DeliveryParty>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""TIN"">{buyerTin}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PartyIdentification>
				<cbc:ID schemeID=""{buyerIdType}"">{buyerIdValue}</cbc:ID>
			</cac:PartyIdentification>
			<cac:PostalAddress>
				<cbc:CityName>Kuala Lumpur</cbc:CityName>
				<cbc:PostalZone>50480</cbc:PostalZone>
				<cbc:CountrySubentityCode>14</cbc:CountrySubentityCode>
				<cac:AddressLine>
					<cbc:Line>Lot 66</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Bangunan Merdeka</cbc:Line>
				</cac:AddressLine>
				<cac:AddressLine>
					<cbc:Line>Persiaran Jaya</cbc:Line>
				</cac:AddressLine>
				<cac:Country>
					<cbc:IdentificationCode listID=""ISO3166-1"" listAgencyID=""6"">MYS</cbc:IdentificationCode>
				</cac:Country>
			</cac:PostalAddress>
			<cac:PartyLegalEntity>
				<cbc:RegistrationName>{buyerName}</cbc:RegistrationName>
			</cac:PartyLegalEntity>
		</cac:DeliveryParty>
		<cac:Shipment>
			<cbc:ID>1234</cbc:ID>
			<cac:FreightAllowanceCharge>
				<cbc:ChargeIndicator>true</cbc:ChargeIndicator>
				<cbc:AllowanceChargeReason>Service charge</cbc:AllowanceChargeReason>
				<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
			</cac:FreightAllowanceCharge>
		</cac:Shipment>
	</cac:Delivery>
	<cac:PaymentMeans>
		<cbc:PaymentMeansCode>01</cbc:PaymentMeansCode>
		<cac:PayeeFinancialAccount>
			<cbc:ID>1234567890</cbc:ID>
		</cac:PayeeFinancialAccount>
	</cac:PaymentMeans>
	<cac:PaymentTerms>
		<cbc:Note>Payment method is cash</cbc:Note>
	</cac:PaymentTerms>
	<cac:PrepaidPayment>
		<cbc:ID>E12345678912</cbc:ID>
		<cbc:PaidAmount currencyID=""MYR"">1.00</cbc:PaidAmount>
		<cbc:PaidDate>2024-07-23</cbc:PaidDate>
		<cbc:PaidTime>00:30:00Z</cbc:PaidTime>
	</cac:PrepaidPayment>
	<cac:AllowanceCharge>
		<cbc:ChargeIndicator>false</cbc:ChargeIndicator>
		<cbc:AllowanceChargeReason>Sample Description</cbc:AllowanceChargeReason>
		<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
	</cac:AllowanceCharge>
	<cac:AllowanceCharge>
		<cbc:ChargeIndicator>true</cbc:ChargeIndicator>
		<cbc:AllowanceChargeReason>Service charge</cbc:AllowanceChargeReason>
		<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
	</cac:AllowanceCharge>
	<cac:TaxTotal>
		<cbc:TaxAmount currencyID=""MYR"">87.63</cbc:TaxAmount>
		<cac:TaxSubtotal>
			<cbc:TaxableAmount currencyID=""MYR"">87.63</cbc:TaxableAmount>
			<cbc:TaxAmount currencyID=""MYR"">87.63</cbc:TaxAmount>
			<cac:TaxCategory>
				<cbc:ID>01</cbc:ID>
				<cac:TaxScheme>
					<cbc:ID schemeID=""UN/ECE 5153"" schemeAgencyID=""6"">OTH</cbc:ID>
				</cac:TaxScheme>
			</cac:TaxCategory>
		</cac:TaxSubtotal>
	</cac:TaxTotal>
	<cac:LegalMonetaryTotal>
		<cbc:LineExtensionAmount currencyID=""MYR"">1436.50</cbc:LineExtensionAmount>
		<cbc:TaxExclusiveAmount currencyID=""MYR"">1436.50</cbc:TaxExclusiveAmount>
		<cbc:TaxInclusiveAmount currencyID=""MYR"">1436.50</cbc:TaxInclusiveAmount>
		<cbc:AllowanceTotalAmount currencyID=""MYR"">1436.50</cbc:AllowanceTotalAmount>
		<cbc:ChargeTotalAmount currencyID=""MYR"">1436.50</cbc:ChargeTotalAmount>
		<cbc:PayableRoundingAmount currencyID=""MYR"">0.30</cbc:PayableRoundingAmount>
		<cbc:PayableAmount currencyID=""MYR"">1436.50</cbc:PayableAmount>
	</cac:LegalMonetaryTotal>
	<cac:InvoiceLine>
		<cbc:ID>1234</cbc:ID>
		<cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
		<cbc:LineExtensionAmount currencyID=""MYR"">1436.50</cbc:LineExtensionAmount>
		<cac:AllowanceCharge>
			<cbc:ChargeIndicator>false</cbc:ChargeIndicator>
			<cbc:AllowanceChargeReason>Sample Description</cbc:AllowanceChargeReason>
			<cbc:MultiplierFactorNumeric>0.15</cbc:MultiplierFactorNumeric>
			<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
		</cac:AllowanceCharge>
		<cac:AllowanceCharge>
			<cbc:ChargeIndicator>true</cbc:ChargeIndicator>
			<cbc:AllowanceChargeReason>Sample Description</cbc:AllowanceChargeReason>
			<cbc:MultiplierFactorNumeric>0.10</cbc:MultiplierFactorNumeric>
			<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
		</cac:AllowanceCharge>
		<cac:TaxTotal>
			<cbc:TaxAmount currencyID=""MYR"">0</cbc:TaxAmount>
			<cac:TaxSubtotal>
				<cbc:TaxableAmount currencyID=""MYR"">1460.50</cbc:TaxableAmount>
				<cbc:TaxAmount currencyID=""MYR"">0</cbc:TaxAmount>
				<cbc:Percent>6.00</cbc:Percent>
				<cac:TaxCategory>
					<cbc:ID>E</cbc:ID>
					<cbc:TaxExemptionReason>Exempt New Means of Transport</cbc:TaxExemptionReason>
					<cac:TaxScheme>
						<cbc:ID schemeID=""UN/ECE 5153"" schemeAgencyID=""6"">OTH</cbc:ID>
					</cac:TaxScheme>
				</cac:TaxCategory>
			</cac:TaxSubtotal>
		</cac:TaxTotal>
		<cac:Item>
			<cbc:Description>Laptop Peripherals</cbc:Description>
			<cac:OriginCountry>
				<cbc:IdentificationCode>MYS</cbc:IdentificationCode>
			</cac:OriginCountry>
			<cac:CommodityClassification>
				<cbc:ItemClassificationCode listID=""PTC"">9800.00.0010</cbc:ItemClassificationCode>
			</cac:CommodityClassification>
			<cac:CommodityClassification>
				<cbc:ItemClassificationCode listID=""CLASS"">003</cbc:ItemClassificationCode>
			</cac:CommodityClassification>
		</cac:Item>
		<cac:Price>
			<cbc:PriceAmount currencyID=""MYR"">17</cbc:PriceAmount>
		</cac:Price>
		<cac:ItemPriceExtension>
			<cbc:Amount currencyID=""MYR"">100</cbc:Amount>
		</cac:ItemPriceExtension>
	</cac:InvoiceLine>
</Invoice>";
    }
}
