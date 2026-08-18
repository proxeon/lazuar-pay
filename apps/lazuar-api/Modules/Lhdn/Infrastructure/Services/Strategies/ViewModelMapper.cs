using System;
using System.Linq;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

/// <summary>
/// Transforms incoming API DTOs into structured ViewModels for logic-less template rendering.
/// Calculates required mathematical groupings (like TaxSubtotals) to satisfy LHDN Schematron rules.
/// Handles the "Entity Swap" for Self-Billed documents where the Lazuar Tenant acts as the Buyer instead of the Supplier.
/// </summary>
public static class ViewModelMapper
{
    public static UblInvoiceViewModel MapToViewModel(
        SubmitDocumentRequestDto request,
        LhdnTenantConfig config,
        string documentVersion,
        string? supplierSstNumber = null)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var startDate = request.Billing_period_start?.UtcDateTime ?? issueDate.AddDays(-30);
        var endDate = request.Billing_period_end?.UtcDateTime ?? issueDate;

        // LHDN rule: Classification '004' is strictly for Consolidated B2C (General TIN + ID = NA). 
        // We must not flag a valid individual freelancer (who has an NRIC) as a Consolidated B2C.
        var isConsolidatedB2c = request.Buyer_tin == "EI00000000010" && request.Buyer_id_value == "NA";
        
        int rawEnumValue = (int)request.Document_type;
        var docTypeCodeString = rawEnumValue.ToString("D2");
        
        var isSelfBilled = docTypeCodeString is "11" or "12" or "13" or "14";

        // Supplier (or buyer on self-billed) legal profile comes from tenant config — never hardcode sample HQ.
        var configParty = new UblPartyViewModel
        {
            Name = string.IsNullOrWhiteSpace(config.LegalName) ? "NA" : config.LegalName,
            Tin = string.IsNullOrWhiteSpace(config.SupplierTin) ? "NA" : config.SupplierTin,
            IdType = string.IsNullOrWhiteSpace(config.IdType) ? "BRN" : config.IdType,
            IdValue = string.IsNullOrWhiteSpace(config.IdValue) ? "NA" : config.IdValue,
            MsicCode = string.IsNullOrWhiteSpace(config.MsicCode) ? "00000" : config.MsicCode,
            Phone = "",
            Email = "",
            AddressLine1 = string.IsNullOrWhiteSpace(config.AddressLine1) ? "NA" : config.AddressLine1,
            City = string.IsNullOrWhiteSpace(config.City) ? "NA" : config.City,
            PostalCode = string.IsNullOrWhiteSpace(config.Postal) ? "00000" : config.Postal,
            StateCode = string.IsNullOrWhiteSpace(config.State) ? "17" : config.State.TrimStart('_'),
            CountryCode = string.IsNullOrWhiteSpace(config.Country) ? "MYS" : config.Country,
            SstNumber = string.IsNullOrWhiteSpace(supplierSstNumber) ? null : supplierSstNumber.Trim()
        };

        var requestParty = new UblPartyViewModel
        {
            Name = string.IsNullOrWhiteSpace(request.Buyer_name) ? "NA" : request.Buyer_name,
            Tin = string.IsNullOrWhiteSpace(request.Buyer_tin) ? "NA" : request.Buyer_tin,
            IdType = request.Buyer_id_type.ToString(),
            IdValue = string.IsNullOrWhiteSpace(request.Buyer_id_value) ? "NA" : request.Buyer_id_value,
            Email = string.IsNullOrWhiteSpace(request.Buyer_email) ? "" : request.Buyer_email,
            Phone = string.IsNullOrWhiteSpace(request.Buyer_phone) ? "" : request.Buyer_phone,
            AddressLine1 = string.IsNullOrWhiteSpace(request.Buyer_address?.Line1) ? "NA" : request.Buyer_address.Line1,
            City = string.IsNullOrWhiteSpace(request.Buyer_address?.City) ? "NA" : request.Buyer_address.City,
            PostalCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Postal_code) ? "00000" : request.Buyer_address.Postal_code,
            StateCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Line1)
                ? "17"
                : request.Buyer_address.State_code.ToString().TrimStart('_'),
            CountryCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Country_code) ? "MYS" : request.Buyer_address.Country_code,
            MsicCode = "00000" 
        };

        var model = new UblInvoiceViewModel
        {
            InternalId = request.Internal_id,
            DocumentVersion = documentVersion,
            DocTypeCode = docTypeCodeString,
            OriginalLhdnUuid = request.Original_lhdn_uuid ?? "",
            IssueDateString = issueDate.ToString("yyyy-MM-dd"),
            IssueTimeString = issueDate.ToString("HH:mm:ssZ"),
            BillingPeriodStartString = startDate.ToString("yyyy-MM-dd"),
            BillingPeriodEndString = endDate.ToString("yyyy-MM-dd"),
            BillingPeriodDescription = isConsolidatedB2c ? "Monthly" : "One-time",
            TotalExcludingTax = (decimal)request.Total_excluding_tax,
            TotalTax = (decimal)request.Total_tax,
            TotalIncludingTax = (decimal)request.Total_including_tax,
            
            // Entity Swap
            Supplier = isSelfBilled ? requestParty : configParty,
            Buyer = isSelfBilled ? configParty : requestParty
        };

        if (request.Items != null)
        {
            model.InvoiceLines = request.Items.Select(i => new UblInvoiceLineViewModel
            {
                Description = string.IsNullOrWhiteSpace(i.Description) ? "NA" : i.Description,
                ClassificationCode = isConsolidatedB2c ? "004" : (string.IsNullOrWhiteSpace(i.Classification_code) ? "022" : i.Classification_code),
                Quantity = (decimal)i.Quantity,
                UnitPrice = (decimal)i.Unit_price,
                TaxRate = (decimal)i.Tax_rate,
                TaxAmount = (decimal)i.Tax_amount,
                Subtotal = (decimal)i.Subtotal,
                TaxTypeCode = i.Tax_type_code.ToString().TrimStart('_')
            }).ToList();

            model.TaxSubtotals = model.InvoiceLines
                .GroupBy(l => l.TaxTypeCode)
                .Select(g => new UblTaxSubtotalViewModel
                {
                    TaxCategoryCode = g.Key,
                    TaxableAmount = g.Sum(l => l.Subtotal),
                    TaxAmount = g.Sum(l => l.TaxAmount)
                })
                .ToList();
        }

        return model;
    }
}
