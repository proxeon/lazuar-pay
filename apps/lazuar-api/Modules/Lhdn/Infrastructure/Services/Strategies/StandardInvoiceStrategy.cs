using System;
using System.Linq;
using System.Text.Json;
using Modules.Lhdn.Domain;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class StandardInvoiceStrategy : IDocumentGenerationStrategy
{
    private readonly ITemplateEngine _templateEngine;

    public StandardInvoiceStrategy(ITemplateEngine templateEngine)
    {
        _templateEngine = templateEngine;
    }

    public string Generate(TaxDocument document, LhdnTenantConfig config, string version)
    {
        var isForeignBuyer = document.BuyerCountryCode != "MYS" && document.BuyerCountryCode != "MY";

        // LHDN explicitly mandates these override values for foreign buyers (export sales)
        var safeBuyerTin = isForeignBuyer ? "EI00000000020" : document.BuyerTin;
        var safeBuyerIdType = isForeignBuyer ? "PASSPORT" : document.BuyerIdType;
        var safeBuyerIdValue = isForeignBuyer ? "NA" : document.BuyerIdValue;
        var safeBuyerStateCode = isForeignBuyer ? "00" : document.BuyerStateCode; // 00 is the LHDN state code for Foreign countries

        // Ensure default fallbacks are completely valid
        if (string.IsNullOrEmpty(safeBuyerTin)) safeBuyerTin = "EI00000000010"; // Default B2C General TIN
        if (string.IsNullOrEmpty(safeBuyerIdType)) safeBuyerIdType = "NRIC";
        if (string.IsNullOrEmpty(safeBuyerIdValue)) safeBuyerIdValue = "000000000000";

        var payload = new
        {
            InternalId = document.InternalReferenceId,
            IssueTime = document.IssueDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            IssueDate = document.IssueDate.ToString("yyyy-MM-dd"),
            IssueTimeOnly = document.IssueDate.ToString("HH:mm:ssZ"),
            
            SupplierName = config.SupplierName,
            SupplierTin = config.SupplierTin,
            SupplierMsicCode = config.MsicCode,
            SupplierIdType = "BRN",
            SupplierIdValue = config.SupplierRegistrationNumber ?? "NA",
            SupplierSstNumber = config.SupplierSstNumber ?? "NA",

            BuyerName = document.BuyerName,
            BuyerTin = safeBuyerTin,
            BuyerIdType = safeBuyerIdType,
            BuyerIdValue = safeBuyerIdValue,
            BuyerEmail = document.BuyerEmail ?? "NA",
            BuyerPhone = document.BuyerPhone ?? "NA",
            
            BuyerAddressLine1 = document.BuyerAddressLine1 ?? "NA",
            BuyerAddressLine2 = document.BuyerAddressLine2 ?? "NA",
            BuyerCity = document.BuyerCity ?? "NA",
            BuyerPostalCode = document.BuyerPostalCode ?? "NA",
            BuyerStateCode = safeBuyerStateCode,
            BuyerCountryCode = document.BuyerCountryCode ?? "MYS",

            TotalExcludingTax = document.TotalExcludingTax.ToString("0.00"),
            TotalTax = document.TotalTax.ToString("0.00"),
            TotalIncludingTax = document.TotalIncludingTax.ToString("0.00"),

            Items = document.Items.Select((item, idx) => new
            {
                LineId = (idx + 1).ToString(),
                Description = item.Description,
                ClassificationCode = item.ClassificationCode,
                Quantity = item.Quantity.ToString("0.00"),
                UnitPrice = item.UnitPrice.ToString("0.00"),
                TaxRate = item.TaxRate.ToString("0.00"),
                TaxAmount = item.TaxAmount.ToString("0.00"),
                Subtotal = item.Subtotal.ToString("0.00"),
                TaxTypeCode = item.TaxTypeCode,
                // Zero-rated (export) logic mapped via tax code
                TaxExemptionReason = item.TaxTypeCode == "E" ? "Export of Services" : "NA"
            }).ToList()
        };

        var jsonStr = JsonSerializer.Serialize(payload);

        if (version == "1.1")
        {
            return _templateEngine.Render("Invoice_v1_1", jsonStr);
        }

        return _templateEngine.Render("Invoice_v1_0", jsonStr);
    }
}
