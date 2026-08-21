namespace Lazuar.Pay.Checkouts;

public sealed class CheckoutSession
{
    public required string Id { get; init; }
    public required string OrgId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
