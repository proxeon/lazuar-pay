using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Modules.Commerce.Contracts;

/// <summary>
/// Read access to a tenant's active subscribers for cross-module broadcast fan-out.
/// Resides in Contracts so the Communications module can enumerate recipients without
/// referencing Commerce.Application (architecture boundary).
/// </summary>
public interface ISubscriberQueryService
{
    /// <summary>Count of active (non-pending, non-canceled) subscribers for the tenant.</summary>
    Task<int> GetActiveSubscriberCountAsync(Guid organizationId);

    /// <summary>Page of active subscriber recipients (email/phone/name).</summary>
    Task<IReadOnlyList<SubscriberRecipient>> GetActiveSubscriberRecipientsAsync(Guid organizationId, int page, int limit);

    /// <summary>
    /// Commerce-schema snapshot for dunning / lifecycle mail (plan, list price, next bill).
    /// Null when the subscription is missing or belongs to another org.
    /// </summary>
    Task<SubscriptionMailContext?> GetSubscriptionMailContextAsync(Guid organizationId, Guid subscriptionId);
}

public record SubscriberRecipient(Guid SubscriptionId, string Email, string? Phone, string? Name);

public sealed record SubscriptionMailContext(
    Guid SubscriptionId,
    Guid ProductId,
    string PlanName,
    decimal Price,
    string Currency,
    DateTime? NextBillingDate,
    string Status);
