using System;
using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services;

public static class UblNodeBuilder
{
    public const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string CacNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string GeneralPublicTin = "EI00000000010";

    public static XmlElement CreateCbcElement(XmlDocument doc, string name, string value)
    {
        var element = doc.CreateElement("cbc", name, CbcNamespace);
        element.InnerText = value;
        return element;
    }

    public static XmlElement BuildEmptySignatureNode(XmlDocument doc)
    {
        var signature = doc.CreateElement("cac", "Signature", CacNamespace);
        signature.AppendChild(CreateCbcElement(doc, "ID", "urn:oasis:names:specification:ubl:signature:Invoice"));
        signature.AppendChild(CreateCbcElement(doc, "SignatureMethod", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"));
        return signature;
    }

    public static XmlElement BuildInvoiceDocumentReference(XmlDocument doc, string internalId, string? lhdnUuid)
    {
        var billingRef = doc.CreateElement("cac", "BillingReference", CacNamespace);
        var invoiceDocRef = doc.CreateElement("cac", "InvoiceDocumentReference", CacNamespace);

        invoiceDocRef.AppendChild(CreateCbcElement(doc, "ID", string.IsNullOrWhiteSpace(internalId) ? "NA" : internalId));
        
        if (!string.IsNullOrWhiteSpace(lhdnUuid))
        {
            invoiceDocRef.AppendChild(CreateCbcElement(doc, "UUID", lhdnUuid));
        }

        billingRef.AppendChild(invoiceDocRef);
        return billingRef;
    }

    public static XmlElement BuildSupplierParty(XmlDocument doc, LhdnTenantConfig tenantConfig)
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

        var partyId3 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id3 = CreateCbcElement(doc, "ID", "NA");
        id3.SetAttribute("schemeID", "SST");
        partyId3.AppendChild(id3);
        cacParty.AppendChild(partyId3);

        var partyId4 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id4 = CreateCbcElement(doc, "ID", "NA");
        id4.SetAttribute("schemeID", "TTX");
        partyId4.AppendChild(id4);
        cacParty.AppendChild(partyId4);

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

    public static XmlElement BuildCustomerParty(XmlDocument doc, SubmitDocumentRequestDto request, bool isB2c)
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

        var partyId3 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id3 = CreateCbcElement(doc, "ID", "NA");
        id3.SetAttribute("schemeID", "SST");
        partyId3.AppendChild(id3);
        cacParty.AppendChild(partyId3);

        var partyId4 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id4 = CreateCbcElement(doc, "ID", "NA");
        id4.SetAttribute("schemeID", "TTX");
        partyId4.AppendChild(id4);
        cacParty.AppendChild(partyId4);

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

    public static XmlElement BuildTaxTotal(XmlDocument doc, double taxableAmount, double taxAmount, string taxTypeCode)
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

    public static XmlElement BuildLegalMonetaryTotal(XmlDocument doc, SubmitDocumentRequestDto request)
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

    public static XmlElement BuildInvoiceLine(XmlDocument doc, LhdnItemDto item, int index, bool isB2c)
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

        var originCountry = doc.CreateElement("cac", "OriginCountry", CacNamespace);
        originCountry.AppendChild(CreateCbcElement(doc, "IdentificationCode", "MYS"));
        cacItem.AppendChild(originCountry);

        var commodityPtc = doc.CreateElement("cac", "CommodityClassification", CacNamespace);
        var ptcCode = CreateCbcElement(doc, "ItemClassificationCode", "NA");
        ptcCode.SetAttribute("listID", "PTC");
        commodityPtc.AppendChild(ptcCode);
        cacItem.AppendChild(commodityPtc);

        var commodityClass = doc.CreateElement("cac", "CommodityClassification", CacNamespace);
        var classificationCode = CreateCbcElement(doc, "ItemClassificationCode", isB2c ? "004" : item.Classification_code);
        classificationCode.SetAttribute("listID", "CLASS");
        commodityClass.AppendChild(classificationCode);
        cacItem.AppendChild(commodityClass);

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
}
