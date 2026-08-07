namespace Modules.Payments.Contracts.Results;

/// <summary>
/// Rich cashier result: hosted URL + provider session/bill id + resolved gateway.
/// Used by M2M integration checkouts; Commerce continues to use the string query wrapper.
/// </summary>
public record GenerateCheckoutSessionResult(
    string CheckoutUrl,
    string? ProviderSessionId,
    string GatewayName);
