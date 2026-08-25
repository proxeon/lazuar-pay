namespace Lazuar.Pay.Checkouts;

public sealed class CreateCheckoutRequest
{
    public string? OrgId { get; set; }
    public string? Provider { get; set; }
    public string? ProductId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? IdempotencyKey { get; set; }
}
