namespace Lazuar.Pay.PaymentLinks;

public sealed class CreatePaymentLinkRequest
{
    public string? OrgId { get; set; }
    public string? Provider { get; set; }
    public string? ProductId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    /// <summary>1 is one person. N is a cap. Ignored when Unlimited is true. Default 1.</summary>
    public int? MaxPayers { get; set; }
    public bool Unlimited { get; set; }
    public string? Label { get; set; }
}
