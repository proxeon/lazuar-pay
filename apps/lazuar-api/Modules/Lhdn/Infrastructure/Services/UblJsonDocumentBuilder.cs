using System.Text.Json.Nodes;
using Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>Maps the Scriban view model into LHDN's nested UBL JSON arrays.</summary>
public static class UblJsonDocumentBuilder
{
    public static JsonObject Build(UblInvoiceViewModel model)
    {
        var invoice = new JsonObject
        {
            ["ID"] = Text(model.InternalId),
            ["IssueDate"] = Text(model.IssueDateString),
            ["IssueTime"] = Text(model.IssueTimeString),
            ["InvoiceTypeCode"] = new JsonArray(new JsonObject
            {
                ["_"] = model.DocTypeCode,
                ["listVersionID"] = model.DocumentVersion
            }),
            ["DocumentCurrencyCode"] = Text("MYR"),
            ["TaxCurrencyCode"] = Text("MYR"),
            ["InvoicePeriod"] = new JsonArray(new JsonObject
            {
                ["StartDate"] = Text(model.BillingPeriodStartString),
                ["EndDate"] = Text(model.BillingPeriodEndString),
                ["Description"] = Text("Monthly")
            }),
            ["AccountingSupplierParty"] = new JsonArray(new JsonObject
            {
                ["Party"] = new JsonArray(Party(model.Supplier, includeMsic: true))
            }),
            ["AccountingCustomerParty"] = new JsonArray(new JsonObject
            {
                ["Party"] = new JsonArray(Party(model.Buyer, includeMsic: false))
            }),
            ["TaxTotal"] = new JsonArray(new JsonObject
            {
                ["TaxAmount"] = Amount(model.TotalTax),
                ["TaxSubtotal"] = TaxSubtotals(model)
            }),
            ["LegalMonetaryTotal"] = new JsonArray(new JsonObject
            {
                ["LineExtensionAmount"] = Amount(model.TotalExcludingTax),
                ["TaxExclusiveAmount"] = Amount(model.TotalExcludingTax),
                ["TaxInclusiveAmount"] = Amount(model.TotalIncludingTax),
                ["PayableAmount"] = Amount(model.TotalIncludingTax)
            }),
            ["InvoiceLine"] = InvoiceLines(model)
        };

        return new JsonObject
        {
            ["_D"] = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            ["_A"] = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
            ["_B"] = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
            ["Invoice"] = new JsonArray(invoice)
        };
    }

    public static JsonArray BuildSignatureExtensions(string signatureBase64, string certificateBase64, string documentHashHex)
    {
        return new JsonArray(new JsonObject
        {
            ["UBLExtension"] = new JsonArray(new JsonObject
            {
                ["ExtensionURI"] = Text("urn:oasis:names:specification:ubl:dsig:enveloped:xades"),
                ["ExtensionContent"] = new JsonArray(new JsonObject
                {
                    ["UBLDocumentSignatures"] = new JsonArray(new JsonObject
                    {
                        ["SignatureInformation"] = new JsonArray(new JsonObject
                        {
                            ["ID"] = Text("urn:oasis:names:specification:ubl:signature:1"),
                            ["ReferencedSignatureID"] = Text("urn:oasis:names:specification:ubl:signature:Invoice"),
                            ["Signature"] = new JsonArray(new JsonObject
                            {
                                ["Id"] = "urn:oasis:names:specification:ubl:signature:Invoice",
                                ["SignatureValue"] = Text(signatureBase64),
                                ["KeyInfo"] = new JsonArray(new JsonObject
                                {
                                    ["X509Data"] = new JsonArray(new JsonObject
                                    {
                                        ["X509Certificate"] = Text(certificateBase64)
                                    })
                                }),
                                ["SignedInfo"] = new JsonArray(new JsonObject
                                {
                                    ["DigestValue"] = Text(documentHashHex)
                                })
                            })
                        })
                    })
                })
            })
        });
    }

    private static JsonObject Party(UblPartyViewModel party, bool includeMsic)
    {
        var identifications = new JsonArray
        {
            Identification("TIN", party.Tin),
            Identification(string.IsNullOrWhiteSpace(party.IdType) ? "BRN" : party.IdType, party.IdValue)
        };

        if (!string.IsNullOrWhiteSpace(party.SstNumber))
        {
            identifications.Add(Identification("SST", party.SstNumber));
        }

        var obj = new JsonObject
        {
            ["PartyIdentification"] = identifications,
            ["PostalAddress"] = new JsonArray(new JsonObject
            {
                ["CityName"] = Text(party.City),
                ["PostalZone"] = Text(party.PostalCode),
                ["CountrySubentityCode"] = Text(party.StateCode),
                ["AddressLine"] = new JsonArray(new JsonObject { ["Line"] = Text(party.AddressLine1) }),
                ["Country"] = new JsonArray(new JsonObject
                {
                    ["IdentificationCode"] = Text(party.CountryCode)
                })
            }),
            ["PartyLegalEntity"] = new JsonArray(new JsonObject
            {
                ["RegistrationName"] = Text(party.Name)
            })
        };

        if (includeMsic)
        {
            obj["IndustryClassificationCode"] = Text(party.MsicCode);
        }

        return obj;
    }

    private static JsonArray InvoiceLines(UblInvoiceViewModel model)
    {
        var lines = new JsonArray();
        var index = 1;
        foreach (var line in model.InvoiceLines)
        {
            lines.Add(new JsonObject
            {
                ["ID"] = Text(index.ToString()),
                ["InvoicedQuantity"] = new JsonArray(new JsonObject { ["_"] = line.Quantity, ["unitCode"] = "C62" }),
                ["LineExtensionAmount"] = Amount(line.Subtotal),
                ["Item"] = new JsonArray(new JsonObject
                {
                    ["Description"] = Text(line.Description),
                    ["CommodityClassification"] = new JsonArray(new JsonObject
                    {
                        ["ItemClassificationCode"] = new JsonArray(new JsonObject
                        {
                            ["_"] = line.ClassificationCode,
                            ["listID"] = "CLASS"
                        })
                    })
                }),
                ["Price"] = new JsonArray(new JsonObject { ["PriceAmount"] = Amount(line.UnitPrice) }),
                ["TaxTotal"] = new JsonArray(new JsonObject
                {
                    ["TaxAmount"] = Amount(line.TaxAmount),
                    ["TaxSubtotal"] = new JsonArray(new JsonObject
                    {
                        ["TaxableAmount"] = Amount(line.Subtotal),
                        ["TaxAmount"] = Amount(line.TaxAmount),
                        ["Percent"] = new JsonArray(new JsonObject { ["_"] = line.TaxRate }),
                        ["TaxCategory"] = new JsonArray(new JsonObject { ["ID"] = Text(line.TaxTypeCode) })
                    })
                })
            });
            index++;
        }

        return lines;
    }

    private static JsonArray TaxSubtotals(UblInvoiceViewModel model)
    {
        var arr = new JsonArray();
        foreach (var sub in model.TaxSubtotals)
        {
            arr.Add(new JsonObject
            {
                ["TaxableAmount"] = Amount(sub.TaxableAmount),
                ["TaxAmount"] = Amount(sub.TaxAmount),
                ["TaxCategory"] = new JsonArray(new JsonObject { ["ID"] = Text(sub.TaxCategoryCode) })
            });
        }

        return arr;
    }

    private static JsonObject Identification(string schemeId, string value) =>
        new()
        {
            ["ID"] = new JsonArray(new JsonObject
            {
                ["_"] = value,
                ["schemeID"] = schemeId
            })
        };

    private static JsonArray Text(string? value) =>
        new(new JsonObject { ["_"] = value ?? "" });

    private static JsonArray Amount(decimal value) =>
        new(new JsonObject { ["_"] = value.ToString("0.00"), ["currencyID"] = "MYR" });
}
