using System;
using System.Collections.Generic;

namespace Modules.Billing.Infrastructure.Documents;

public class InvoiceDocumentModel
{
    public string DocumentType { get; set; } = "Official Receipt";
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string Currency { get; set; } = "MYR";

    public string CompanyName { get; set; } = string.Empty;
    public string CompanyTin { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public byte[]? CompanyLogo { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public List<InvoiceLineItemModel> LineItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public string? LhdnUuid { get; set; }
    public string? LhdnQrLink { get; set; }
}

public class InvoiceLineItemModel
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
