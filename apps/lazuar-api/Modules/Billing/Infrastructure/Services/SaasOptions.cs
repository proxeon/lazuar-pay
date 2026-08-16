namespace Modules.Billing.Infrastructure.Services;

/// <summary>Strongly-typed binding for the appsettings "Saas" section.</summary>
public class SaasOptions
{
    public SaasPlanOptions Plan { get; set; } = new();
    public SaasSellerOptions Seller { get; set; } = new();
}

public class SaasPlanOptions
{
    public string Code { get; set; } = "hub_starter";
    public string Name { get; set; } = "Hub Starter";
    public decimal AmountMyr { get; set; }
    public string Interval { get; set; } = "mo";
    public string Currency { get; set; } = "MYR";
}

public class SaasSellerOptions
{
    public string LegalName { get; set; } = "Lazuar";
    public string Tin { get; set; } = "";
    public string Address { get; set; } = "";
    public string SstId { get; set; } = "";
    public decimal SstRate { get; set; }
    public string SstReason { get; set; } = "Supplier not SST-registered";
}
