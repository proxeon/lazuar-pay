using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

/// <summary>
/// A synchronous cross-module query. 
/// Allows other modules (like Community) to request a Stripe/Billplz checkout URL 
/// so they can immediately redirect the user in the frontend.
/// </summary>
public record GenerateCheckoutSessionQuery(
    Guid TenantId,
    decimal Amount,
    string Currency,
    string ProductName,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    Dictionary<string, string> Metadata) : IQuery<string>;
