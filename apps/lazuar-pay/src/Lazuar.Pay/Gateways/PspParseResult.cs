namespace Lazuar.Pay.Gateways;

public sealed class PspParseResult
{
    public required string EventId { get; init; }
    public bool Ignored { get; init; }
    public string? IgnoreReason { get; init; }
    public string? CheckoutId { get; init; }
    public string? ProviderRef { get; init; }
    public long? AmountMinor { get; init; }
    public string? Currency { get; init; }
}

public sealed class PspVerifyException : Exception
{
    public PspVerifyException(string message) : base(message) { }
}
