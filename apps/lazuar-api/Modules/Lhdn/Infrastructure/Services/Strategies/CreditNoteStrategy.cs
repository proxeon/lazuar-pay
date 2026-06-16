using System.Linq;
using System.Text.Json.Serialization;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Models;
using Modules.Lhdn.Infrastructure.Serialization;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class CreditNoteStrategy : IUblDocumentStrategy
{
    public object Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var startDate = request.Billing_period_start?.UtcDateTime ?? issueDate;
        var endDate = request.Billing_period_end?.UtcDateTime ?? issueDate;

        var isB2c = string.IsNullOrWhiteSpace(request.Buyer_tin) || request.Buyer_tin == "EI00000000010";

        var billingReference = string.IsNullOrWhiteSpace(request.Original_lhdn_uuid) ? null : new[]
        {
            new LhdnCreditNoteBillingReference(
                InvoiceDocumentReference: new[] { new LhdnCreditNoteDocumentReference("NA", request.Original_lhdn_uuid) }
            )
        };

        var invoice = new LhdnJsonCreditNoteInvoice(
            ID: request.Internal_id,
            IssueDate: issueDate.ToString("yyyy-MM-dd"),
            IssueTime: issueDate.ToString("HH:mm:ssZ"),
            InvoiceTypeCode: new[] { new UblInvoiceTypeCode("02", documentVersion) },
            DocumentCurrencyCode: "MYR",
            TaxCurrencyCode: "MYR",
            InvoicePeriod: new[]
            {
                new LhdnInvoicePeriod(
                    StartDate: startDate.ToString("yyyy-MM-dd"),
                    EndDate: endDate.ToString("yyyy-MM-dd"),
                    Description: "Monthly"
                )
            },
            BillingReference: billingReference,
            AccountingSupplierParty: BuildSupplierParty(config),
            AccountingCustomerParty: BuildCustomerParty(request, isB2c),
            PaymentMeans: null,
            TaxTotal: BuildTaxTotal(request.Total_excluding_tax, request.Total_tax, "06"),
            LegalMonetaryTotal: BuildLegalMonetaryTotal(request),
            InvoiceLine: BuildInvoiceLines(request.Items, isB2c)
        );

        return new LhdnJsonDocumentCreditNote(
            D: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            A: "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
            B: "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
            Invoice: new[] { invoice }
        );
    }

    // Extended models localized for Credit Notes to attach original UUID references
    private record LhdnCreditNoteBillingReference(
        [property: JsonPropertyOrder(1)] LhdnCreditNoteDocumentReference[] InvoiceDocumentReference
    );

    private record LhdnCreditNoteDocumentReference(
        [property: JsonPropertyOrder(1)] UblValue<string> ID,
        [property: JsonPropertyOrder(2)] UblValue<string> UUID
    );

    private record LhdnJsonCreditNoteInvoice(
        [property: JsonPropertyOrder(1)] UblValue<string> ID,
        [property: JsonPropertyOrder(2)] UblValue<string> IssueDate,
        [property: JsonPropertyOrder(3)] UblValue<string> IssueTime,
        [property: JsonPropertyOrder(4)] UblInvoiceTypeCode[] InvoiceTypeCode,
        [property: JsonPropertyOrder(5)] UblValue<string> DocumentCurrencyCode,
        [property: JsonPropertyOrder(6)] UblValue<string>? TaxCurrencyCode,
        [property: JsonPropertyOrder(7)] LhdnInvoicePeriod[]? InvoicePeriod,
        [property: JsonPropertyOrder(8)] LhdnCreditNoteBillingReference[]? BillingReference,
        [property: JsonPropertyOrder(9)] LhdnAccountingParty[] AccountingSupplierParty,
        [property: JsonPropertyOrder(10)] LhdnAccountingParty[] AccountingCustomerParty,
        [property: JsonPropertyOrder(11)] LhdnPaymentMeans[]? PaymentMeans,
        [property: JsonPropertyOrder(12)] LhdnTaxTotal[] TaxTotal,
        [property: JsonPropertyOrder(13)] LhdnLegalMonetaryTotal[] LegalMonetaryTotal,
        [property: JsonPropertyOrder(14)] LhdnInvoiceLine[] InvoiceLine,
        [property: JsonPropertyOrder(100)] object[]? UBLExtensions = null, 
        [property: JsonPropertyOrder(101)] object[]? Signature = null 
    );

    private record LhdnJsonDocumentCreditNote(
        [property: JsonPropertyOrder(1), JsonPropertyName("_D")] string D,
        [property: JsonPropertyOrder(2), JsonPropertyName("_A")] string A,
        [property: JsonPropertyOrder(3), JsonPropertyName("_B")] string B,
        [property: JsonPropertyOrder(4)] LhdnJsonCreditNoteInvoice[] Invoice
    );

    private static LhdnAccountingParty[] BuildSupplierParty(LhdnTenantConfig config) => new[]
    {
        new LhdnAccountingParty(
            AdditionalAccountID: new[] { new LhdnAdditionalAccountId("CPT-CCN-W-211111-KL-000002", "CertEX") },
            Party: new[]
            {
                new LhdnParty(
                    IndustryClassificationCode: new[] { new LhdnIndustryClassificationCode(config.MsicCode ?? "00000", "System Supplier") },
                    PartyIdentification: new[]
                    {
                        new LhdnPartyIdentification(new[] { new LhdnSchemeId(config.SupplierTin, "TIN") }),
                        new LhdnPartyIdentification(new[] { new LhdnSchemeId(config.IdValue, config.IdType) }),
                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("NA", "SST") }),
                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("NA", "TTX") })
                    },
                    PostalAddress: new[]
                    {
                        new LhdnPostalAddress(
                            CityName: "Kuala Lumpur",
                            PostalZone: "50480",
                            CountrySubentityCode: "14",
                            AddressLine: new[] { new LhdnAddressLine("Lot 66") },
                            Country: new[] { new LhdnCountry(new[] { new LhdnIdentificationCode("MYS", "ISO3166-1", "6") }) }
                        )
                    },
                    PartyLegalEntity: new[] { new LhdnPartyLegalEntity("System Supplier") },
                    Contact: new[] { new LhdnContact("+60123456789", "admin@lazuar.com") }
                )
            }
        )
    };

    private static LhdnAccountingParty[] BuildCustomerParty(SubmitDocumentRequestDto request, bool isB2c)
    {
        var tin = isB2c ? "EI00000000010" : request.Buyer_tin;
        var idType = isB2c ? "BRN" : request.Buyer_id_type.ToString();
        var idValue = isB2c ? "NA" : request.Buyer_id_value;
        var name = isB2c ? "General Public" : request.Buyer_name;
        var phone = isB2c ? "+60123456789" : (request.Buyer_phone ?? "+60123456789");
        var email = isB2c ? "na@example.com" : (request.Buyer_email ?? "na@example.com");
        var stateCode = isB2c ? "17" : request.Buyer_address.State_code.ToString().TrimStart('_');

        return new[]
        {
            new LhdnAccountingParty(
                AdditionalAccountID: null,
                Party: new[]
                {
                    new LhdnParty(
                        IndustryClassificationCode: null,
                        PartyIdentification: new[]
                        {
                            new LhdnPartyIdentification(new[] { new LhdnSchemeId(tin, "TIN") }),
                            new LhdnPartyIdentification(new[] { new LhdnSchemeId(idValue, idType) }),
                            new LhdnPartyIdentification(new[] { new LhdnSchemeId("NA", "SST") }),
                            new LhdnPartyIdentification(new[] { new LhdnSchemeId("NA", "TTX") })
                        },
                        PostalAddress: new[]
                        {
                            new LhdnPostalAddress(
                                CityName: isB2c ? "NA" : request.Buyer_address.City,
                                PostalZone: isB2c ? "00000" : request.Buyer_address.Postal_code,
                                CountrySubentityCode: stateCode,
                                AddressLine: new[] { new LhdnAddressLine(isB2c ? "NA" : request.Buyer_address.Line1) },
                                Country: new[] { new LhdnCountry(new[] { new LhdnIdentificationCode(isB2c ? "MYS" : request.Buyer_address.Country_code, "ISO3166-1", "6") }) }
                            )
                        },
                        PartyLegalEntity: new[] { new LhdnPartyLegalEntity(name) },
                        Contact: new[] { new LhdnContact(phone, email) }
                    )
                }
            )
        };
    }

    private static LhdnTaxTotal[] BuildTaxTotal(double taxableAmount, double taxAmount, string taxTypeCode) => new[]
    {
        new LhdnTaxTotal(
            TaxAmount: new[] { new UblAmount((decimal)taxAmount, "MYR") },
            TaxSubtotal: new[]
            {
                new LhdnTaxSubtotal(
                    TaxableAmount: new[] { new UblAmount((decimal)taxableAmount, "MYR") },
                    TaxAmount: new[] { new UblAmount((decimal)taxAmount, "MYR") },
                    TaxCategory: new[]
                    {
                        new LhdnTaxCategory(
                            ID: taxTypeCode,
                            Percent: null,
                            TaxExemptionReason: taxTypeCode is "E" or "06" ? "Not subject to tax" : null,
                            TaxScheme: new[] { new LhdnTaxScheme(new[] { new LhdnTaxSchemeId("OTH", "UN/ECE 5153", "6") }) }
                        )
                    }
                )
            }
        )
    };

    private static LhdnLegalMonetaryTotal[] BuildLegalMonetaryTotal(SubmitDocumentRequestDto request) => new[]
    {
        new LhdnLegalMonetaryTotal(
            LineExtensionAmount: new[] { new UblAmount((decimal)request.Total_excluding_tax, "MYR") },
            TaxExclusiveAmount: new[] { new UblAmount((decimal)request.Total_excluding_tax, "MYR") },
            TaxInclusiveAmount: new[] { new UblAmount((decimal)request.Total_including_tax, "MYR") },
            AllowanceTotalAmount: new[] { new UblAmount(0m, "MYR") },
            ChargeTotalAmount: new[] { new UblAmount(0m, "MYR") },
            PayableRoundingAmount: new[] { new UblAmount(0m, "MYR") },
            PayableAmount: new[] { new UblAmount((decimal)request.Total_including_tax, "MYR") }
        )
    };

    private static LhdnInvoiceLine[] BuildInvoiceLines(System.Collections.Generic.IEnumerable<LhdnItemDto> items, bool isB2c)
    {
        return items.Select((item, index) =>
        {
            var cleanTaxCode = item.Tax_type_code.ToString().TrimStart('_');
            var classCode = isB2c ? "004" : item.Classification_code;

            return new LhdnInvoiceLine(
                ID: (index + 1).ToString(),
                InvoicedQuantity: new[] { new UblQuantity((decimal)item.Quantity, "C62") },
                LineExtensionAmount: new[] { new UblAmount((decimal)item.Subtotal, "MYR") },
                AllowanceCharge: null,
                TaxTotal: BuildTaxTotal(item.Subtotal, item.Tax_amount, cleanTaxCode),
                Item: new[]
                {
                    new LhdnItem(
                        Description: item.Description,
                        CommodityClassification: new[]
                        {
                            new LhdnCommodityClassification(new[] { new LhdnItemClassificationCode("NA", "PTC") }),
                            new LhdnCommodityClassification(new[] { new LhdnItemClassificationCode(classCode, "CLASS") })
                        }
                    )
                },
                Price: new[] { new LhdnPrice(new[] { new UblAmount((decimal)item.Unit_price, "MYR") }) },
                ItemPriceExtension: new[] { new LhdnItemPriceExtension(new[] { new UblAmount((decimal)item.Subtotal, "MYR") }) }
            );
        }).ToArray();
    }
}
