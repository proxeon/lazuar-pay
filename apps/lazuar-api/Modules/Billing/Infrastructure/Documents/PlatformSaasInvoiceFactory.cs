using System;
using Modules.Billing.Domain;
using Modules.Billing.Infrastructure.Services;

namespace Modules.Billing.Infrastructure.Documents;

public static class PlatformSaasInvoiceFactory
{
    public static InvoiceDocumentModel Create(
        SaasOptions saas,
        string invoiceNumber,
        DateTime issueDate,
        string buyerName,
        string buyerEmail,
        decimal amount,
        string currency)
    {
        var seller = saas.Seller;
        var plan = saas.Plan;
        var hasTin = !string.IsNullOrWhiteSpace(seller.Tin);
        var sstReason = string.IsNullOrWhiteSpace(seller.SstReason)
            ? "Supplier not SST-registered"
            : seller.SstReason;

        return new InvoiceDocumentModel
        {
            DocumentType = hasTin ? "Tax invoice" : "Invoice / payment receipt",
            InvoiceNumber = invoiceNumber,
            IssueDate = issueDate,
            Currency = currency,
            CompanyName = string.IsNullOrWhiteSpace(seller.LegalName) ? "Lazuar" : seller.LegalName,
            CompanyTin = seller.Tin ?? "",
            CompanyAddress = seller.Address ?? "",
            CustomerName = buyerName,
            CustomerEmail = buyerEmail,
            LineItems =
            [
                new InvoiceLineItemModel
                {
                    Description = SaasPlanInterval.LineDescription(plan.Name, plan.Interval),
                    Amount = amount
                }
            ],
            Subtotal = amount,
            Tax = 0,
            ShowZeroTax = true,
            TaxLabel = "SST (0%):",
            Total = amount,
            Notes = sstReason
        };
    }
}
