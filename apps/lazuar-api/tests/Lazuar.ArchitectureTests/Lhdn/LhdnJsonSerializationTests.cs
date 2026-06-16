using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Modules.Lhdn.Infrastructure.Models;
using Modules.Lhdn.Infrastructure.Serialization;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests.Lhdn;

[TestFixture]
public class LhdnJsonSerializationTests
{
    private GoldenMasterData _goldenMaster = null!;

    [SetUp]
    public void Setup()
    {
        _goldenMaster = LoadGoldenMaster();
    }

    [Test]
    public void Serialize_RealLhdnDto_ShouldMatchGoldenMasterCharacterForCharacter()
    {
        // Construct the C# DTO exactly mirroring the real Golden Master test-invoice.json
        var payload = new LhdnJsonDocument(
            D: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            A: "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
            B: "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
            Invoice: new[]
            {
                new LhdnJsonInvoice(
                    ID: "TEST-V11-001",
                    IssueDate: "2025-01-01",
                    IssueTime: "12:00:00Z",
                    InvoiceTypeCode: new[] { new UblInvoiceTypeCode("01", "1.1") },
                    DocumentCurrencyCode: "MYR",
                    TaxCurrencyCode: null,
                    InvoicePeriod: null,
                    BillingReference: null,
                    AccountingSupplierParty: new[]
                    {
                        new LhdnAccountingParty(
                            AdditionalAccountID: null,
                            Party: new[]
                            {
                                new LhdnParty(
                                    IndustryClassificationCode: new[] { new LhdnIndustryClassificationCode("62010", "Computer programming activities") },
                                    PartyIdentification: new[]
                                    {
                                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("C00000000000", "TIN") }),
                                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("000000000000", "BRN") })
                                    },
                                    PartyLegalEntity: new[] { new LhdnPartyLegalEntity("EXAMPLE SUPPLIER SDN. BHD.") },
                                    PostalAddress: new[]
                                    {
                                        new LhdnPostalAddress(
                                            AddressLine: new[] { new LhdnAddressLine("123 Example Street") },
                                            CityName: "Kuala Lumpur",
                                            PostalZone: "50000",
                                            CountrySubentityCode: "14",
                                            Country: new[] { new LhdnCountry(new[] { new LhdnIdentificationCode("MYS", "ISO3166-1", "6") }) }
                                        )
                                    },
                                    Contact: new[] { new LhdnContact("+60300000000", "supplier@example.com") }
                                )
                            }
                        )
                    },
                    AccountingCustomerParty: new[]
                    {
                        new LhdnAccountingParty(
                            AdditionalAccountID: null,
                            Party: new[]
                            {
                                new LhdnParty(
                                    IndustryClassificationCode: null,
                                    PartyIdentification: new[]
                                    {
                                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("EI00000000010", "TIN") }),
                                        new LhdnPartyIdentification(new[] { new LhdnSchemeId("000000000000", "NRIC") })
                                    },
                                    PartyLegalEntity: new[] { new LhdnPartyLegalEntity("EXAMPLE CUSTOMER") },
                                    PostalAddress: new[]
                                    {
                                        new LhdnPostalAddress(
                                            AddressLine: new[] { new LhdnAddressLine("456 Customer Road") },
                                            CityName: "Petaling Jaya",
                                            PostalZone: "47800",
                                            CountrySubentityCode: "10",
                                            Country: new[] { new LhdnCountry(new[] { new LhdnIdentificationCode("MYS", "ISO3166-1", "6") }) }
                                        )
                                    },
                                    Contact: new[] { new LhdnContact("+60300000001", null) } 
                                )
                            }
                        )
                    },
                    PaymentMeans: null,
                    LegalMonetaryTotal: new[]
                    {
                        new LhdnLegalMonetaryTotal(
                            LineExtensionAmount: new[] { new UblAmount(100m, "MYR") },
                            TaxExclusiveAmount: new[] { new UblAmount(100m, "MYR") },
                            TaxInclusiveAmount: new[] { new UblAmount(100m, "MYR") },
                            AllowanceTotalAmount: null!,
                            ChargeTotalAmount: null!,
                            PayableRoundingAmount: null!,
                            PayableAmount: new[] { new UblAmount(100m, "MYR") }
                        )
                    },
                    TaxTotal: new[]
                    {
                        new LhdnTaxTotal(
                            TaxAmount: new[] { new UblAmount(0m, "MYR") },
                            TaxSubtotal: new[]
                            {
                                new LhdnTaxSubtotal(
                                    TaxableAmount: new[] { new UblAmount(100m, "MYR") },
                                    TaxAmount: new[] { new UblAmount(0m, "MYR") },
                                    TaxCategory: new[]
                                    {
                                        new LhdnTaxCategory(
                                            ID: "E",
                                            Percent: null,
                                            TaxExemptionReason: "Exempt New Means of Transport",
                                            TaxScheme: new[] { new LhdnTaxScheme(new[] { new LhdnTaxSchemeId("OTH", "UN/ECE 5153", "6") }) }
                                        )
                                    }
                                )
                            }
                        )
                    },
                    InvoiceLine: new[]
                    {
                        new LhdnInvoiceLine(
                            ID: "1",
                            InvoicedQuantity: new[] { new UblQuantity(1m, "C62") },
                            LineExtensionAmount: new[] { new UblAmount(100m, "MYR") },
                            AllowanceCharge: null,
                            TaxTotal: new[]
                            {
                                new LhdnTaxTotal(
                                    TaxAmount: new[] { new UblAmount(0m, "MYR") },
                                    TaxSubtotal: new[]
                                    {
                                        new LhdnTaxSubtotal(
                                            TaxableAmount: new[] { new UblAmount(100m, "MYR") },
                                            TaxAmount: new[] { new UblAmount(0m, "MYR") },
                                            TaxCategory: new[]
                                            {
                                                new LhdnTaxCategory(
                                                    ID: "E",
                                                    Percent: null,
                                                    TaxExemptionReason: "Exempt New Means of Transport",
                                                    TaxScheme: new[] { new LhdnTaxScheme(new[] { new LhdnTaxSchemeId("OTH", "UN/ECE 5153", "6") }) }
                                                )
                                            }
                                        )
                                    }
                                )
                            },
                            Item: new[]
                            {
                                new LhdnItem(
                                    Description: "Test Service Item",
                                    CommodityClassification: new[]
                                    {
                                        new LhdnCommodityClassification(new[] { new LhdnItemClassificationCode("001", "CLASS") })
                                    }
                                )
                            },
                            Price: new[] { new LhdnPrice(new[] { new UblAmount(100m, "MYR") }) },
                            ItemPriceExtension: new[] { new LhdnItemPriceExtension(new[] { new UblAmount(100m, "MYR") }) }
                        )
                    }
                )
            }
        );

        var serializedJson = JsonSerializer.Serialize(payload, LhdnJsonOptions.Instance);

        serializedJson.Should().Be(_goldenMaster.PreHashedJson);
    }

    [Test]
    public void Hash_Utf8Bytes_ShouldProduceExactBase64AndHexRepresentations()
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(_goldenMaster.PreHashedJson);
        var hashBytes = SHA256.HashData(utf8Bytes);

        var computedBase64 = Convert.ToBase64String(hashBytes);
        var computedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        computedBase64.Should().Be(_goldenMaster.ExpectedBase64Hash);
        computedHex.Should().Be(_goldenMaster.ExpectedHexHash);
    }

    private static GoldenMasterData LoadGoldenMaster()
    {
        var assembly = typeof(LhdnJsonSerializationTests).Assembly;
        var resourceName = "Lazuar.ArchitectureTests.TestData.lhdn-golden-master.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName) 
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        
        using var reader = new StreamReader(stream);
        var jsonContent = reader.ReadToEnd();

        return JsonSerializer.Deserialize<GoldenMasterData>(jsonContent)!;
    }

    private record GoldenMasterData(
        string PreHashedJson,
        string ExpectedBase64Hash,
        string ExpectedHexHash
    );
}
