using System.Text.Json.Serialization;
using Modules.Lhdn.Infrastructure.Serialization;

namespace Modules.Lhdn.Infrastructure.Models;

public record LhdnJsonDocument(
    [property: JsonPropertyOrder(1), JsonPropertyName("_D")] string D,
    [property: JsonPropertyOrder(2), JsonPropertyName("_A")] string A,
    [property: JsonPropertyOrder(3), JsonPropertyName("_B")] string B,
    [property: JsonPropertyOrder(4), JsonPropertyName("_E")] string? E = null,
    [property: JsonPropertyOrder(5), JsonPropertyName("_sig")] string? Sig = null,
    [property: JsonPropertyOrder(6), JsonPropertyName("_sac")] string? Sac = null,
    [property: JsonPropertyOrder(7), JsonPropertyName("_sbc")] string? Sbc = null,
    [property: JsonPropertyOrder(8), JsonPropertyName("_ds")] string? Ds = null,
    [property: JsonPropertyOrder(9), JsonPropertyName("_xades")] string? Xades = null,
    [property: JsonPropertyOrder(10)] LhdnJsonInvoice[] Invoice = null!
);

public record LhdnJsonInvoice(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblValue<string> IssueDate,
    [property: JsonPropertyOrder(3)] UblValue<string> IssueTime,
    [property: JsonPropertyOrder(4)] UblInvoiceTypeCode[] InvoiceTypeCode,
    [property: JsonPropertyOrder(5)] UblValue<string> DocumentCurrencyCode,
    [property: JsonPropertyOrder(6)] UblValue<string>? TaxCurrencyCode,
    [property: JsonPropertyOrder(7)] LhdnInvoicePeriod[]? InvoicePeriod,
    [property: JsonPropertyOrder(8)] LhdnBillingReference[]? BillingReference,
    [property: JsonPropertyOrder(9)] LhdnRootAdditionalDocumentReference[]? AdditionalDocumentReference,
    [property: JsonPropertyOrder(10)] LhdnAccountingParty[] AccountingSupplierParty,
    [property: JsonPropertyOrder(11)] LhdnAccountingParty[] AccountingCustomerParty,
    [property: JsonPropertyOrder(12)] LhdnPaymentMeans[]? PaymentMeans,
    [property: JsonPropertyOrder(13)] LhdnLegalMonetaryTotal[] LegalMonetaryTotal,
    [property: JsonPropertyOrder(14)] LhdnTaxTotal[] TaxTotal,
    [property: JsonPropertyOrder(15)] LhdnInvoiceLine[] InvoiceLine,
    [property: JsonPropertyOrder(100)] LhdnUblExtension[]? UBLExtensions = null, 
    [property: JsonPropertyOrder(101)] object[]? Signature = null 
);

public record UblInvoiceTypeCode(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("listVersionID")] string ListVersionId
);

public record LhdnInvoicePeriod(
    [property: JsonPropertyOrder(1)] UblValue<string> StartDate,
    [property: JsonPropertyOrder(2)] UblValue<string> EndDate,
    [property: JsonPropertyOrder(3)] UblValue<string> Description
);

public record LhdnBillingReference(
    [property: JsonPropertyOrder(1)] LhdnDocumentReference[] AdditionalDocumentReference
);

public record LhdnDocumentReference(
    [property: JsonPropertyOrder(1)] UblValue<string> ID
);

public record LhdnRootAdditionalDocumentReference(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblValue<string>? DocumentType = null,
    [property: JsonPropertyOrder(3)] UblValue<string>? DocumentDescription = null
);

public record LhdnAccountingParty(
    [property: JsonPropertyOrder(1)] LhdnAdditionalAccountId[]? AdditionalAccountID,
    [property: JsonPropertyOrder(2)] LhdnParty[] Party
);

public record LhdnAdditionalAccountId(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("schemeAgencyName")] string SchemeAgencyName
);

public record LhdnParty(
    [property: JsonPropertyOrder(1)] LhdnIndustryClassificationCode[]? IndustryClassificationCode,
    [property: JsonPropertyOrder(2)] LhdnPartyIdentification[] PartyIdentification,
    [property: JsonPropertyOrder(3)] LhdnPartyLegalEntity[] PartyLegalEntity,
    [property: JsonPropertyOrder(4)] LhdnPostalAddress[] PostalAddress,
    [property: JsonPropertyOrder(5)] LhdnContact[] Contact
);

public record LhdnIndustryClassificationCode(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("name")] string Name
);

public record LhdnPartyIdentification(
    [property: JsonPropertyOrder(1)] LhdnSchemeId[] ID
);

public record LhdnSchemeId(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("schemeID")] string SchemeId
);

public record LhdnPostalAddress(
    [property: JsonPropertyOrder(1)] LhdnAddressLine[] AddressLine,
    [property: JsonPropertyOrder(2)] UblValue<string> CityName,
    [property: JsonPropertyOrder(3)] UblValue<string> PostalZone,
    [property: JsonPropertyOrder(4)] UblValue<string> CountrySubentityCode,
    [property: JsonPropertyOrder(5)] LhdnCountry[] Country
);

public record LhdnAddressLine(
    [property: JsonPropertyOrder(1)] UblValue<string> Line
);

public record LhdnCountry(
    [property: JsonPropertyOrder(1)] LhdnIdentificationCode[] IdentificationCode
);

public record LhdnIdentificationCode(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("listID")] string ListId,
    [property: JsonPropertyName("listAgencyID")] string ListAgencyId
);

public record LhdnPartyLegalEntity(
    [property: JsonPropertyOrder(1)] UblValue<string> RegistrationName
);

public record LhdnContact(
    [property: JsonPropertyOrder(1)] UblValue<string>? Telephone,
    [property: JsonPropertyOrder(2)] UblValue<string>? ElectronicMail
);

public record LhdnPaymentMeans(
    [property: JsonPropertyOrder(1)] UblValue<string> PaymentMeansCode
);

public record LhdnTaxTotal(
    [property: JsonPropertyOrder(1)] UblAmount[] TaxAmount,
    [property: JsonPropertyOrder(2)] LhdnTaxSubtotal[] TaxSubtotal
);

public record LhdnTaxSubtotal(
    [property: JsonPropertyOrder(1)] UblAmount[] TaxableAmount,
    [property: JsonPropertyOrder(2)] UblAmount[] TaxAmount,
    [property: JsonPropertyOrder(3)] LhdnTaxCategory[] TaxCategory
);

public record LhdnTaxCategory(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblValue<decimal>? Percent,
    [property: JsonPropertyOrder(3)] UblValue<string>? TaxExemptionReason,
    [property: JsonPropertyOrder(4)] LhdnTaxScheme[] TaxScheme
);

public record LhdnTaxScheme(
    [property: JsonPropertyOrder(1)] LhdnTaxSchemeId[] ID
);

public record LhdnTaxSchemeId(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("schemeID")] string SchemeId,
    [property: JsonPropertyName("schemeAgencyID")] string SchemeAgencyId
);

public record LhdnLegalMonetaryTotal(
    [property: JsonPropertyOrder(1)] UblAmount[] LineExtensionAmount,
    [property: JsonPropertyOrder(2)] UblAmount[] TaxExclusiveAmount,
    [property: JsonPropertyOrder(3)] UblAmount[] TaxInclusiveAmount,
    [property: JsonPropertyOrder(4)] UblAmount[] AllowanceTotalAmount,
    [property: JsonPropertyOrder(5)] UblAmount[] ChargeTotalAmount,
    [property: JsonPropertyOrder(6)] UblAmount[] PayableRoundingAmount,
    [property: JsonPropertyOrder(7)] UblAmount[] PayableAmount
);

public record LhdnInvoiceLine(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblQuantity[] InvoicedQuantity,
    [property: JsonPropertyOrder(3)] UblAmount[] LineExtensionAmount,
    [property: JsonPropertyOrder(4)] LhdnAllowanceCharge[]? AllowanceCharge,
    [property: JsonPropertyOrder(5)] LhdnTaxTotal[] TaxTotal,
    [property: JsonPropertyOrder(6)] LhdnItem[] Item,
    [property: JsonPropertyOrder(7)] LhdnPrice[] Price,
    [property: JsonPropertyOrder(8)] LhdnItemPriceExtension[] ItemPriceExtension
);

public record LhdnAllowanceCharge(
    [property: JsonPropertyOrder(1)] UblValue<bool> ChargeIndicator,
    [property: JsonPropertyOrder(2)] UblValue<string> AllowanceChargeReason,
    [property: JsonPropertyOrder(3)] UblAmount[] Amount
);

public record LhdnItem(
    [property: JsonPropertyOrder(1)] UblValue<string> Description,
    [property: JsonPropertyOrder(2)] LhdnCommodityClassification[] CommodityClassification
);

public record LhdnCommodityClassification(
    [property: JsonPropertyOrder(1)] LhdnItemClassificationCode[] ItemClassificationCode
);

public record LhdnItemClassificationCode(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("listID")] string ListId
);

public record LhdnPrice(
    [property: JsonPropertyOrder(1)] UblAmount[] PriceAmount
);

public record LhdnItemPriceExtension(
    [property: JsonPropertyOrder(1)] UblAmount[] Amount
);

public record UblAmount(
    [property: JsonPropertyName("_")] decimal Value, 
    [property: JsonPropertyName("currencyID")] string CurrencyId
);

public record UblQuantity(
    [property: JsonPropertyName("_")] decimal Value, 
    [property: JsonPropertyName("unitCode")] string UnitCode
);
