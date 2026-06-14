using System;
using System.Xml;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class UblXmlGenerator : IUblXmlGenerator
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string CacNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public XmlDocument GenerateInvoiceXml(SubmitDocumentRequestDto request)
    {
        var doc = new XmlDocument();
        var root = doc.CreateElement("Invoice", InvoiceNamespace);
        root.SetAttribute("xmlns:cac", CacNamespace);
        root.SetAttribute("xmlns:cbc", CbcNamespace);
        doc.AppendChild(root);

        root.AppendChild(CreateCbcElement(doc, "ID", request.Internal_id));
        root.AppendChild(CreateCbcElement(doc, "IssueDate", request.Issue_date.ToString("yyyy-MM-dd")));
        root.AppendChild(CreateCbcElement(doc, "IssueTime", request.Issue_date.ToString("HH:mm:ssZ")));
        
        var invoiceTypeCode = CreateCbcElement(doc, "InvoiceTypeCode", request.Document_type);
        invoiceTypeCode.SetAttribute("listVersionID", "1.0"); 
        root.AppendChild(invoiceTypeCode);
        
        root.AppendChild(CreateCbcElement(doc, "DocumentCurrencyCode", "MYR"));

        root.AppendChild(BuildSupplierParty(doc));
        root.AppendChild(BuildCustomerParty(doc, request));
        
        // Add root level TaxTotal
        root.AppendChild(BuildTaxTotal(doc, request.Total_excluding_tax, request.Total_tax));

        root.AppendChild(BuildLegalMonetaryTotal(doc, request));

        for (int i = 0; i < request.Items.Count; i++)
        {
            root.AppendChild(BuildInvoiceLine(doc, request.Items[i], i + 1));
        }

        return doc;
    }

    private XmlElement BuildSupplierParty(XmlDocument doc)
    {
        var party = doc.CreateElement("cac", "AccountingSupplierParty", CacNamespace);
        var cacParty = doc.CreateElement("cac", "Party", CacNamespace);

        var industryCode = CreateCbcElement(doc, "IndustryClassificationCode", "62010");
        cacParty.AppendChild(industryCode);

        var partyId1 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id1 = CreateCbcElement(doc, "ID", "IG56848407100"); 
        id1.SetAttribute("schemeID", "TIN");
        partyId1.AppendChild(id1);
        cacParty.AppendChild(partyId1);

        var partyId2 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id2 = CreateCbcElement(doc, "ID", "990806086487"); 
        id2.SetAttribute("schemeID", "NRIC");
        partyId2.AppendChild(id2);
        cacParty.AppendChild(partyId2);

        var postalAddress = doc.CreateElement("cac", "PostalAddress", CacNamespace);
        postalAddress.AppendChild(CreateCbcElement(doc, "CityName", "CHEMOR"));
        postalAddress.AppendChild(CreateCbcElement(doc, "PostalZone", "31200"));
        postalAddress.AppendChild(CreateCbcElement(doc, "CountrySubentityCode", "08"));

        var addressLine = doc.CreateElement("cac", "AddressLine", CacNamespace);
        addressLine.AppendChild(CreateCbcElement(doc, "Line", "NO 16, HALA KLEBANG RESTU 18, MEDAN KLEBANG RESTU"));
        postalAddress.AppendChild(addressLine);

        var country = doc.CreateElement("cac", "Country", CacNamespace);
        var countryCode = CreateCbcElement(doc, "IdentificationCode", "MYS");
        country.AppendChild(countryCode);
        postalAddress.AppendChild(country);
        
        cacParty.AppendChild(postalAddress);

        var legalEntity = doc.CreateElement("cac", "PartyLegalEntity", CacNamespace);
        legalEntity.AppendChild(CreateCbcElement(doc, "RegistrationName", "AXXX_XXXXRI")); 
        cacParty.AppendChild(legalEntity);

        var contact = doc.CreateElement("cac", "Contact", CacNamespace);
        contact.AppendChild(CreateCbcElement(doc, "Telephone", "01160714390"));
        contact.AppendChild(CreateCbcElement(doc, "ElectronicMail", "akmal.fir010@gmail.com"));
        cacParty.AppendChild(contact);

        party.AppendChild(cacParty);
        return party;
    }

    private XmlElement BuildCustomerParty(XmlDocument doc, SubmitDocumentRequestDto request)
    {
        var party = doc.CreateElement("cac", "AccountingCustomerParty", CacNamespace);
        var cacParty = doc.CreateElement("cac", "Party", CacNamespace);

        var partyId1 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id1 = CreateCbcElement(doc, "ID", request.Buyer_tin);
        id1.SetAttribute("schemeID", "TIN");
        partyId1.AppendChild(id1);
        cacParty.AppendChild(partyId1);

        var partyId2 = doc.CreateElement("cac", "PartyIdentification", CacNamespace);
        var id2 = CreateCbcElement(doc, "ID", request.Buyer_id_value);
        id2.SetAttribute("schemeID", request.Buyer_id_type);
        partyId2.AppendChild(id2);
        cacParty.AppendChild(partyId2);

        var postalAddress = doc.CreateElement("cac", "PostalAddress", CacNamespace);
        postalAddress.AppendChild(CreateCbcElement(doc, "CityName", request.Buyer_address.City));
        postalAddress.AppendChild(CreateCbcElement(doc, "PostalZone", request.Buyer_address.Postal_code));
        postalAddress.AppendChild(CreateCbcElement(doc, "CountrySubentityCode", request.Buyer_address.State_code));

        var addressLine = doc.CreateElement("cac", "AddressLine", CacNamespace);
        addressLine.AppendChild(CreateCbcElement(doc, "Line", request.Buyer_address.Line1));
        postalAddress.AppendChild(addressLine);

        var country = doc.CreateElement("cac", "Country", CacNamespace);
        var countryCode = CreateCbcElement(doc, "IdentificationCode", request.Buyer_address.Country_code);
        country.AppendChild(countryCode);
        postalAddress.AppendChild(country);
        
        cacParty.AppendChild(postalAddress);

        var legalEntity = doc.CreateElement("cac", "PartyLegalEntity", CacNamespace);
        legalEntity.AppendChild(CreateCbcElement(doc, "RegistrationName", request.Buyer_name));
        cacParty.AppendChild(legalEntity);

        var contact = doc.CreateElement("cac", "Contact", CacNamespace);
        contact.AppendChild(CreateCbcElement(doc, "Telephone", "01160714390"));
        cacParty.AppendChild(contact);

        party.AppendChild(cacParty);
        return party;
    }

    private XmlElement BuildTaxTotal(XmlDocument doc, double taxableAmount, double taxAmount)
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
        taxCategory.AppendChild(CreateCbcElement(doc, "ID", "06")); // 06 = Not Applicable
        taxCategory.AppendChild(CreateCbcElement(doc, "TaxExemptionReason", "Not subject to tax"));
        
        var taxScheme = doc.CreateElement("cac", "TaxScheme", CacNamespace);
        taxScheme.AppendChild(CreateCbcElement(doc, "ID", "OTH"));
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

    private XmlElement BuildInvoiceLine(XmlDocument doc, LhdnItemDto item, int index)
    {
        var line = doc.CreateElement("cac", "InvoiceLine", CacNamespace);
        line.AppendChild(CreateCbcElement(doc, "ID", index.ToString()));

        var quantity = CreateCbcElement(doc, "InvoicedQuantity", item.Quantity.ToString("F2"));
        quantity.SetAttribute("unitCode", "C62");
        line.AppendChild(quantity);

        var extAmount = CreateCbcElement(doc, "LineExtensionAmount", item.Subtotal.ToString("F2"));
        extAmount.SetAttribute("currencyID", "MYR");
        line.AppendChild(extAmount);

        // Add line level TaxTotal
        line.AppendChild(BuildTaxTotal(doc, item.Subtotal, item.Tax_amount));

        var cacItem = doc.CreateElement("cac", "Item", CacNamespace);
        cacItem.AppendChild(CreateCbcElement(doc, "Description", item.Description));

        var commodity = doc.CreateElement("cac", "CommodityClassification", CacNamespace);
        var classificationCode = CreateCbcElement(doc, "ItemClassificationCode", item.Classification_code);
        classificationCode.SetAttribute("listID", "CLASS");
        commodity.AppendChild(classificationCode);
        cacItem.AppendChild(commodity);

        var classifiedTax = doc.CreateElement("cac", "ClassifiedTaxCategory", CacNamespace);
        classifiedTax.AppendChild(CreateCbcElement(doc, "ID", "06"));
        var taxScheme = doc.CreateElement("cac", "TaxScheme", CacNamespace);
        taxScheme.AppendChild(CreateCbcElement(doc, "ID", "OTH"));
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
