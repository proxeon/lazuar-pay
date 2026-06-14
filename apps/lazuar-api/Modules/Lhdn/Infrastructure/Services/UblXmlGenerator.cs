using System;
using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services;

public class UblXmlGenerator : IUblXmlGenerator
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string CacNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const string GeneralPublicTin = "EI00000000010";

    public XmlDocument GenerateInvoiceXml(SubmitDocumentRequestDto request, LhdnTenantConfig tenantConfig, string? originalUuid = null)
    {
        var doc = new XmlDocument();
        var root = doc.CreateElement("Invoice", InvoiceNamespace);
        root.SetAttribute("xmlns:cac", CacNamespace);
        root.SetAttribute("xmlns:cbc", CbcNamespace);
        doc.AppendChild(root);

        root.AppendChild(CreateCbcElement(doc, "ID", request.Internal_id));
        root.AppendChild(CreateCbcElement(doc, "IssueDate", request.Issue_date.ToString("yyyy-MM-dd")));
        root.AppendChild(CreateCbcElement(doc, "IssueTime", request.Issue_date.ToString("HH:mm:ssZ")));
        
        var cleanDocTypeCode = request.Document_type switch
        {
            SubmitDocumentRequestDtoDocument_type._01 => "01",
            SubmitDocumentRequestDtoDocument_type._02 => "02",
            SubmitDocumentRequestDtoDocument_type._03 => "03",
            SubmitDocumentRequestDtoDocument_type._04 => "04",
            SubmitDocumentRequestDtoDocument_type._11 => "11",
            SubmitDocumentRequestDtoDocument_type._12 => "12",
            SubmitDocumentRequestDtoDocument_type._13 => "13",
            SubmitDocumentRequestDtoDocument_type._14 => "14",
            _ => "01"
        };

        var invoiceTypeCode = CreateCbcElement(doc, "InvoiceTypeCode", cleanDocTypeCode);
        invoiceTypeCode.SetAttribute("listVersionID", "1.0"); 
        root.AppendChild(invoiceTypeCode);
        
        root.AppendChild(CreateCbcElement(doc, "DocumentCurrencyCode", "MYR"));

        var isB2c = string.IsNullOrWhiteSpace(request.Buyer_tin) || request.Buyer_tin == GeneralPublicTin;

        // 1. InvoicePeriod (Mandatory for B2C Consolidated Invoices)
        if (isB2c)
        {
            var invoicePeriod = doc.CreateElement("cac", "InvoicePeriod", CacNamespace);
            invoicePeriod.AppendChild(CreateCbcElement(doc, "StartDate", request.Issue_date.ToString("yyyy-MM-dd")));
            invoicePeriod.AppendChild(CreateCbcElement(doc, "EndDate", request.Issue_date.ToString("yyyy-MM-dd")));
            invoicePeriod.AppendChild(CreateCbcElement(doc, "Description", "Consolidated Invoice"));
            root.AppendChild(invoicePeriod);
        }

        // 2. BillingReference (Required for Original UUIDs AND B2C Receipts)
        if (!string.IsNullOrEmpty(originalUuid))
        {
            var billingRef = doc.CreateElement("cac", "BillingReference", CacNamespace);
            var addDocRef = doc.CreateElement("cac", "AdditionalDocumentReference", CacNamespace);
            addDocRef.AppendChild(CreateCbcElement(doc, "ID", originalUuid));
            billingRef.AppendChild(addDocRef);
            root.AppendChild(billingRef);
        }
        else if (isB2c)
        {
            // FIX: For B2C Consolidated Invoices, LHDN strictly expects the receipt numbers inside BillingReference.
            // If the item classification is '004', LHDN will NOT try to look this up as an e-Invoice UUID.
            var billingRef = doc.CreateElement("cac", "BillingReference", CacNamespace);
            var addDocRef = doc.CreateElement("cac", "AdditionalDocumentReference", CacNamespace);
            addDocRef.AppendChild(CreateCbcElement(doc, "ID", request.Internal_id)); // Internal Receipt Number
            billingRef.AppendChild(addDocRef);
            root.AppendChild(billingRef);
        }

        root.AppendChild(BuildSupplierParty(doc, tenantConfig));
        root.AppendChild(BuildCustomerParty(doc, request, isB2c));
        
        root.AppendChild(BuildTaxTotal(doc, request.Total_excluding_tax, request.Total_tax, "06"));
        root.AppendChild(BuildLegalMonetaryTotal(doc, request));

        for (int i = 0; i < request.Items.Count; i++)
        {
            root.AppendChild(BuildInvoiceLine(doc, request.Items[i], i + 1, isB2c));
        }

        return doc;
    }

    private XmlElement BuildSupplierParty(XmlDocument doc, LhdnTenantConfig tenantConfig)
    {
        var party = doc.CreateElement("cac", "AccountingSupplierParty", CacNamespace);
        var cacParty = doc.CreateElement("cac", "Party", CacNamespace);

        var industryCode = CreateCbcElement(doc, "IndustryClassificationCode", tenantConfig.MsicCode ?? "00000");
        cacParty.AppendChild(industryCode);

        var partyId1 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id1 = CreateCbcElement(doc, "ID", tenantConfig.SupplierTin); 
        id1.SetAttribute("schemeID", "TIN");
        partyId1.AppendChild(id1);
        cacParty.AppendChild(partyId1);

        var partyId2 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id2 = CreateCbcElement(doc, "ID", tenantConfig.IdValue); 
        id2.SetAttribute("schemeID", tenantConfig.IdType);
        partyId2.AppendChild(id2);
        cacParty.AppendChild(partyId2);

        var postalAddress = doc.CreateElement("cac", "PostalAddress", CacNamespace);
        postalAddress.AppendChild(CreateCbcElement(doc, "CityName", "NA"));
        postalAddress.AppendChild(CreateCbcElement(doc, "PostalZone", "00000"));
        postalAddress.AppendChild(CreateCbcElement(doc, "CountrySubentityCode", "14"));

        var addressLine = doc.CreateElement("cac", "AddressLine", CacNamespace);
        addressLine.AppendChild(CreateCbcElement(doc, "Line", "NA"));
        postalAddress.AppendChild(addressLine);

        var country = doc.CreateElement("cac", "Country", CacNamespace);
        var countryCode = CreateCbcElement(doc, "IdentificationCode", "MYS");
        countryCode.SetAttribute("listID", "ISO3166-1");
        countryCode.SetAttribute("listAgencyID", "6");
        country.AppendChild(countryCode);
        postalAddress.AppendChild(country);
        
        cacParty.AppendChild(postalAddress);

        var legalEntity = doc.CreateElement("cac", "PartyLegalEntity", CacNamespace);
        legalEntity.AppendChild(CreateCbcElement(doc, "RegistrationName", "System Supplier")); 
        cacParty.AppendChild(legalEntity);

        var contact = doc.CreateElement("cac", "Contact", CacNamespace);
        contact.AppendChild(CreateCbcElement(doc, "Telephone", "+60123456789"));
        contact.AppendChild(CreateCbcElement(doc, "ElectronicMail", "admin@lazuar.com"));
        cacParty.AppendChild(contact);

        party.AppendChild(cacParty);
        return party;
    }

    private XmlElement BuildCustomerParty(XmlDocument doc, SubmitDocumentRequestDto request, bool isB2c)
    {
        var party = doc.CreateElement("cac", "AccountingCustomerParty", CacNamespace);
        var cacParty = doc.CreateElement("cac", "Party", CacNamespace);

        var partyId1 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id1 = CreateCbcElement(doc, "ID", isB2c ? GeneralPublicTin : request.Buyer_tin);
        id1.SetAttribute("schemeID", "TIN");
        partyId1.AppendChild(id1);
        cacParty.AppendChild(partyId1);

        var cleanBuyerIdType = request.Buyer_id_type switch
        {
            SubmitDocumentRequestDtoBuyer_id_type.BRN => "BRN",
            SubmitDocumentRequestDtoBuyer_id_type.NRIC => "NRIC",
            SubmitDocumentRequestDtoBuyer_id_type.PASSPORT => "PASSPORT",
            SubmitDocumentRequestDtoBuyer_id_type.ARMY => "ARMY",
            _ => "BRN"
        };

        var partyId2 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id2 = CreateCbcElement(doc, "ID", isB2c ? "NA" : request.Buyer_id_value);
        id2.SetAttribute("schemeID", isB2c ? "BRN" : cleanBuyerIdType);
        partyId2.AppendChild(id2);
        cacParty.AppendChild(partyId2);

        var cleanStateCode = request.Buyer_address.State_code switch
        {
            LhdnAddressDtoState_code._01 => "01",
            LhdnAddressDtoState_code._02 => "02",
            LhdnAddressDtoState_code._03 => "03",
            LhdnAddressDtoState_code._04 => "04",
            LhdnAddressDtoState_code._05 => "05",
            LhdnAddressDtoState_code._06 => "06",
            LhdnAddressDtoState_code._07 => "07",
            LhdnAddressDtoState_code._08 => "08",
            LhdnAddressDtoState_code._09 => "09",
            LhdnAddressDtoState_code._10 => "10",
            LhdnAddressDtoState_code._11 => "11",
            LhdnAddressDtoState_code._12 => "12",
            LhdnAddressDtoState_code._13 => "13",
            LhdnAddressDtoState_code._14 => "14",
            LhdnAddressDtoState_code._15 => "15",
            LhdnAddressDtoState_code._16 => "16",
            LhdnAddressDtoState_code._17 => "17",
            _ => "17"
        };

        var postalAddress = doc.CreateElement("cac", "PostalAddress", CacNamespace);
        postalAddress.AppendChild(CreateCbcElement(doc, "CityName", isB2c ? "NA" : request.Buyer_address.City));
        postalAddress.AppendChild(CreateCbcElement(doc, "PostalZone", isB2c ? "00000" : request.Buyer_address.Postal_code));
        postalAddress.AppendChild(CreateCbcElement(doc, "CountrySubentityCode", isB2c ? "17" : cleanStateCode));

        var addressLine = doc.CreateElement("cac", "AddressLine", CacNamespace);
        addressLine.AppendChild(CreateCbcElement(doc, "Line", isB2c ? "NA" : request.Buyer_address.Line1));
        postalAddress.AppendChild(addressLine);

        var country = doc.CreateElement("cac", "Country", CacNamespace);
        var countryCode = CreateCbcElement(doc, "IdentificationCode", isB2c ? "MYS" : request.Buyer_address.Country_code);
        countryCode.SetAttribute("listID", "ISO3166-1");
        countryCode.SetAttribute("listAgencyID", "6");
        country.AppendChild(countryCode);
        postalAddress.AppendChild(country);
        
        cacParty.AppendChild(postalAddress);

        var legalEntity = doc.CreateElement("cac", "PartyLegalEntity", CacNamespace);
        legalEntity.AppendChild(CreateCbcElement(doc, "RegistrationName", isB2c ? "General Public" : request.Buyer_name));
        cacParty.AppendChild(legalEntity);

        var contact = doc.CreateElement("cac", "Contact", CacNamespace);
        contact.AppendChild(CreateCbcElement(doc, "Telephone", isB2c ? "+60123456789" : (request.Buyer_phone ?? "+60123456789")));
        contact.AppendChild(CreateCbcElement(doc, "ElectronicMail", isB2c ? "na@example.com" : (request.Buyer_email ?? "na@example.com")));
        cacParty.AppendChild(contact);

        party.AppendChild(cacParty);
        return party;
    }

    private XmlElement BuildTaxTotal(XmlDocument doc, double taxableAmount, double taxAmount, string taxTypeCode)
    {
        var taxTotal = doc.CreateElement("cac", "TaxTotal", CacNamespace);
        
        var cbcTaxAmount = CreateCbcElement(doc, "TaxAmount", taxAmount.ToString("F2"));
        cbcTaxAmount.SetAttribute("currencyID", "MYR");
        taxTotal.AppendChild(cbcTaxAmount);

        var taxSubtotal = doc.CreateElement("cac", "TaxSubtotal", CacNamespace);
        
        var cbcTaxableAmount = CreateCbcElement(doc, "TaxableAmount", taxableAmount.ToString("F2"));
        cbcTaxableAmount.SetAttribute("currencyID", "MYR");
        taxSubtotal.AppendChild(cbcTaxableAmount);

        var subTaxAmount = CreateCbcElement(doc, "TaxAmount", taxAmount.ToString("F2"));
        subTaxAmount.SetAttribute("currencyID", "MYR");
        taxSubtotal.AppendChild(subTaxAmount);

        var taxCategory = doc.CreateElement("cac", "TaxCategory", CacNamespace);
        taxCategory.AppendChild(CreateCbcElement(doc, "ID", taxTypeCode));
        
        if (taxTypeCode == "E" || taxTypeCode == "06")
        {
            taxCategory.AppendChild(CreateCbcElement(doc, "TaxExemptionReason", "Not subject to tax"));
        }
        
        var taxScheme = doc.CreateElement("cac", "TaxScheme", CacNamespace);
        var taxSchemeId = CreateCbcElement(doc, "ID", "OTH");
        taxSchemeId.SetAttribute("schemeID", "UN/ECE 5153");
        taxSchemeId.SetAttribute("schemeAgencyID", "6");
        taxScheme.AppendChild(taxSchemeId);
        taxCategory.AppendChild(taxScheme);
        
        taxSubtotal.AppendChild(taxCategory);
        taxTotal.AppendChild(taxSubtotal);

        return taxTotal;
    }

    private XmlElement BuildLegalMonetaryTotal(XmlDocument doc, SubmitDocumentRequestDto request)
    {
        var total = doc.CreateElement("cac", "LegalMonetaryTotal", CacNamespace);
        
        var extAmount = CreateCbcElement(doc, "LineExtensionAmount", request.Total_excluding_tax.ToString("F2"));
        extAmount.SetAttribute("currencyID", "MYR");
        total.AppendChild(extAmount);
        
        var exclusive = CreateCbcElement(doc, "TaxExclusiveAmount", request.Total_excluding_tax.ToString("F2"));
        exclusive.SetAttribute("currencyID", "MYR");
        total.AppendChild(exclusive);

        var inclusive = CreateCbcElement(doc, "TaxInclusiveAmount", request.Total_including_tax.ToString("F2"));
        inclusive.SetAttribute("currencyID", "MYR");
        total.AppendChild(inclusive);

        var payable = CreateCbcElement(doc, "PayableAmount", request.Total_including_tax.ToString("F2"));
        payable.SetAttribute("currencyID", "MYR");
        total.AppendChild(payable);

        return total;
    }

    private XmlElement BuildInvoiceLine(XmlDocument doc, LhdnItemDto item, int index, bool isB2c)
    {
        var line = doc.CreateElement("cac", "InvoiceLine", CacNamespace);
        line.AppendChild(CreateCbcElement(doc, "ID", index.ToString()));

        var quantity = CreateCbcElement(doc, "InvoicedQuantity", item.Quantity.ToString("F2"));
        quantity.SetAttribute("unitCode", "C62");
        line.AppendChild(quantity);

        var extAmount = CreateCbcElement(doc, "LineExtensionAmount", item.Subtotal.ToString("F2"));
        extAmount.SetAttribute("currencyID", "MYR");
        line.AppendChild(extAmount);

        var cleanTaxCode = item.Tax_type_code switch
        {
            LhdnItemDtoTax_type_code._01 => "01",
            LhdnItemDtoTax_type_code._02 => "02",
            LhdnItemDtoTax_type_code._03 => "03",
            LhdnItemDtoTax_type_code._04 => "04",
            LhdnItemDtoTax_type_code._05 => "05",
            LhdnItemDtoTax_type_code._06 => "06",
            LhdnItemDtoTax_type_code.E => "E",
            _ => "06"
        };

        line.AppendChild(BuildTaxTotal(doc, item.Subtotal, item.Tax_amount, cleanTaxCode));

        var cacItem = doc.CreateElement("cac", "Item", CacNamespace);
        cacItem.AppendChild(CreateCbcElement(doc, "Description", item.Description));

        var commodity = doc.CreateElement("cac", "CommodityClassification", CacNamespace);
        
        // FIX: Force '004' (Consolidated e-Invoice) if Buyer TIN is General Public.
        // This is strictly required by LHDN when using State Code 17 and EI00000000010.
        var classificationCode = CreateCbcElement(doc, "ItemClassificationCode", isB2c ? "004" : item.Classification_code);
        classificationCode.SetAttribute("listID", "CLASS");
        commodity.AppendChild(classificationCode);
        cacItem.AppendChild(commodity);

        var classifiedTax = doc.CreateElement("cac", "ClassifiedTaxCategory", CacNamespace);
        classifiedTax.AppendChild(CreateCbcElement(doc, "ID", cleanTaxCode));
        var taxScheme = doc.CreateElement("cac", "TaxScheme", CacNamespace);
        var taxSchemeId = CreateCbcElement(doc, "ID", "OTH");
        taxSchemeId.SetAttribute("schemeID", "UN/ECE 5153");
        taxSchemeId.SetAttribute("schemeAgencyID", "6");
        taxScheme.AppendChild(taxSchemeId);
        classifiedTax.AppendChild(taxScheme);
        cacItem.AppendChild(classifiedTax);

        line.AppendChild(cacItem);

        var price = doc.CreateElement("cac", "Price", CacNamespace);
        var priceAmount = CreateCbcElement(doc, "PriceAmount", item.Unit_price.ToString("F2"));
        priceAmount.SetAttribute("currencyID", "MYR");
        price.AppendChild(priceAmount);
        
        line.AppendChild(price);

        var itemPriceExtension = doc.CreateElement("cac", "ItemPriceExtension", CacNamespace);
        var itemExtAmount = CreateCbcElement(doc, "Amount", item.Subtotal.ToString("F2"));
        itemExtAmount.SetAttribute("currencyID", "MYR");
        itemPriceExtension.AppendChild(itemExtAmount);
        line.AppendChild(itemPriceExtension);

        return line;
    }

    private XmlElement CreateCbcElement(XmlDocument doc, string name, string value)
    {
        var element = doc.CreateElement("cbc", name, CbcNamespace);
        element.InnerText = value;
        return element;
    }
}
