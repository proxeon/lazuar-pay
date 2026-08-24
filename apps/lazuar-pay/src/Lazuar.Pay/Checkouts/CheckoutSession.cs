namespace Lazuar.Pay.Checkouts;

public sealed class CheckoutSession
{
    public required string Id { get; init; }
    public required string OrgId { get; init; }
    public string? Provider { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public string? PublicToken { get; init; }
    public string? Interval { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? PayerName { get; init; }
    public string? PayerEmail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
