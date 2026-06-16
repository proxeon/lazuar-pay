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
    public static UblInvoiceViewModel MapToViewModel(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var startDate = request.Billing_period_start?.UtcDateTime ?? issueDate.AddDays(-30);
        var endDate = request.Billing_period_end?.UtcDateTime ?? issueDate;

        var isGeneralPublic = request.Buyer_tin == "EI00000000010";
        var docTypeCodeString = request.Document_type.ToString().TrimStart('_');
        
        var isSelfBilled = docTypeCodeString is "11" or "12" or "13" or "14";

        var configParty = new UblPartyViewModel
        {
            Name = "Lazuar Tenant",
            Tin = config.SupplierTin ?? "NA",
            IdType = config.IdType ?? "BRN",
            IdValue = config.IdValue ?? "NA",
            MsicCode = config.MsicCode ?? "62010",
            Phone = "+60123456789",
            Email = "tenant@example.com"
        };

        var requestParty = new UblPartyViewModel
        {
            Name = string.IsNullOrWhiteSpace(request.Buyer_name) ? "NA" : request.Buyer_name,
            Tin = isGeneralPublic ? "EI00000000010" : (string.IsNullOrWhiteSpace(request.Buyer_tin) ? "NA" : request.Buyer_tin),
            IdType = request.Buyer_id_type.ToString(),
            IdValue = string.IsNullOrWhiteSpace(request.Buyer_id_value) ? "NA" : request.Buyer_id_value,
            Email = string.IsNullOrWhiteSpace(request.Buyer_email) ? "" : request.Buyer_email,
            Phone = string.IsNullOrWhiteSpace(request.Buyer_phone) ? "+60000000000" : request.Buyer_phone,
            AddressLine1 = string.IsNullOrWhiteSpace(request.Buyer_address?.Line1) ? "NA" : request.Buyer_address.Line1,
            City = string.IsNullOrWhiteSpace(request.Buyer_address?.City) ? "NA" : request.Buyer_address.City,
            PostalCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Postal_code) ? "00000" : request.Buyer_address.Postal_code,
            StateCode = request.Buyer_address?.State_code.ToString().TrimStart('_') ?? "14",
            CountryCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Country_code) ? "MYS" : request.Buyer_address.Country_code,
            MsicCode = "00000" // Fallback for external vendors who do not provide their MSIC code
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
            TotalExcludingTax = (decimal)request.Total_excluding_tax,
            TotalTax = (decimal)request.Total_tax,
            TotalIncludingTax = (decimal)request.Total_including_tax,
            
            Supplier = isSelfBilled ? requestParty : configParty,
            Buyer = isSelfBilled ? configParty : requestParty
        };

        if (request.Items != null)
        {
            model.InvoiceLines = request.Items.Select(i => new UblInvoiceLineViewModel
            {
                Description = string.IsNullOrWhiteSpace(i.Description) ? "NA" : i.Description,
                ClassificationCode = isGeneralPublic ? "004" : (string.IsNullOrWhiteSpace(i.Classification_code) ? "022" : i.Classification_code),
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
