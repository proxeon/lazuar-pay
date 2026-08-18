using System.Collections.Generic;

namespace Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

public class UblInvoiceViewModel
{
    public string InternalId { get; set; } = "";
    public string DocumentVersion { get; set; } = "";
    public string DocTypeCode { get; set; } = "";
    public string OriginalLhdnUuid { get; set; } = "";
    public string IssueDateString { get; set; } = "";
    public string IssueTimeString { get; set; } = "";
    public string BillingPeriodStartString { get; set; } = "";
    public string BillingPeriodEndString { get; set; } = "";
    public string BillingPeriodDescription { get; set; } = "One-time";

    public decimal TotalExcludingTax { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalIncludingTax { get; set; }

    public UblPartyViewModel Supplier { get; set; } = new();
    public UblPartyViewModel Buyer { get; set; } = new();

    public List<UblInvoiceLineViewModel> InvoiceLines { get; set; } = new();
    public List<UblTaxSubtotalViewModel> TaxSubtotals { get; set; } = new();
}
