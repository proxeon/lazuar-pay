namespace Modules.Lhdn.Infrastructure.Services.Strategies.ViewModels;

public class UblTaxSubtotalViewModel
{
    public string TaxCategoryCode { get; set; } = "";
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}
