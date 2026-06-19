namespace Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

public class UblInvoiceLineViewModel
{
    public string Description { get; set; } = "";
    public string ClassificationCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Subtotal { get; set; }
    public string TaxTypeCode { get; set; } = "";
}
