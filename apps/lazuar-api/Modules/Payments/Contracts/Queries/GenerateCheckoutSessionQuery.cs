using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

/// <summary>
/// A synchronous cross-module query. 
/// Allows other modules to request a checkout URL so they can immediately redirect the user in the frontend.
/// The SetupFutureUsage flag instructs the gateway to vault the payment method for future off-session charging.
/// </summary>
public record GenerateCheckoutSessionQuery(
    Guid TenantId,
    decimal Amount,
    string Currency,
    string ProductName,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    Dictionary<string, string> Metadata,
    bool SetupFutureUsage = false) : IQuery<string>;
