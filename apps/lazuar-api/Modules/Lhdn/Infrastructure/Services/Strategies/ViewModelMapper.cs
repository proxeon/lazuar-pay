using System;
using System.Linq;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public static class ViewModelMapper
{
    public static UblInvoiceViewModel MapToViewModel(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var issueDate = request.Issue_date.UtcDateTime;
        var startDate = request.Billing_period_start?.UtcDateTime ?? issueDate.AddDays(-30);
        var endDate = request.Billing_period_end?.UtcDateTime ?? issueDate;

        var model = new UblInvoiceViewModel
        {
            InternalId = request.Internal_id,
            DocumentVersion = documentVersion,
            DocTypeCode = request.Document_type.ToString().TrimStart('_'),
            OriginalLhdnUuid = request.Original_lhdn_uuid ?? "",
            IssueDateString = issueDate.ToString("yyyy-MM-dd"),
            IssueTimeString = issueDate.ToString("HH:mm:ssZ"),
            BillingPeriodStartString = startDate.ToString("yyyy-MM-dd"),
            BillingPeriodEndString = endDate.ToString("yyyy-MM-dd"),
            TotalExcludingTax = (decimal)request.Total_excluding_tax,
            TotalTax = (decimal)request.Total_tax,
            TotalIncludingTax = (decimal)request.Total_including_tax,
            Supplier = new UblPartyViewModel
            {
                Name = "Lazuar Supplier", // In production, this should be pulled from tenant profile
                Tin = config.SupplierTin ?? "NA",
                IdType = config.IdType ?? "BRN",
                IdValue = config.IdValue ?? "NA",
                MsicCode = config.MsicCode ?? "62010"
            },
            Buyer = new UblPartyViewModel
            {
                Name = string.IsNullOrWhiteSpace(request.Buyer_name) ? "NA" : request.Buyer_name,
                Tin = string.IsNullOrWhiteSpace(request.Buyer_tin) ? "NA" : request.Buyer_tin,
                IdType = request.Buyer_id_type.ToString(),
                IdValue = string.IsNullOrWhiteSpace(request.Buyer_id_value) ? "NA" : request.Buyer_id_value,
                Email = string.IsNullOrWhiteSpace(request.Buyer_email) ? "NA" : request.Buyer_email,
                Phone = string.IsNullOrWhiteSpace(request.Buyer_phone) ? "NA" : request.Buyer_phone,
                AddressLine1 = string.IsNullOrWhiteSpace(request.Buyer_address?.Line1) ? "NA" : request.Buyer_address.Line1,
                City = string.IsNullOrWhiteSpace(request.Buyer_address?.City) ? "NA" : request.Buyer_address.City,
                PostalCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Postal_code) ? "00000" : request.Buyer_address.Postal_code,
                StateCode = request.Buyer_address?.State_code.ToString().TrimStart('_') ?? "14",
                CountryCode = string.IsNullOrWhiteSpace(request.Buyer_address?.Country_code) ? "MYS" : request.Buyer_address.Country_code
            }
        };

        if (request.Items != null)
        {
            model.InvoiceLines = request.Items.Select(i => new UblInvoiceLineViewModel
            {
                Description = string.IsNullOrWhiteSpace(i.Description) ? "NA" : i.Description,
                ClassificationCode = string.IsNullOrWhiteSpace(i.Classification_code) ? "022" : i.Classification_code,
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
