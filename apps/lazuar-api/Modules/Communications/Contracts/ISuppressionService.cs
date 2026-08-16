using System;
using System.Threading.Tasks;

namespace Modules.Communications.Contracts;

/// <summary>
/// Manages the email suppression list (unsubscribes, bounces, complaints) to protect
/// sender reputation and honor opt-outs. Cross-module callers (e.g. Messaging dispatch,
/// broadcast fan-out) use this to skip suppressed addresses before sending.
/// </summary>
public enum SuppressionLane
{
    Transactional,
    Marketing
}

public interface ISuppressionService
{
    /// <summary>True if the email is suppressed for the tenant (any reason). Prefer the lane overload.</summary>
    Task<bool> IsSuppressedAsync(Guid organizationId, string email);

    /// <summary>
    /// Transactional (receipts / dunning / magic-link) is blocked by BOUNCE, COMPLAINT, ANONYMIZED.
    /// Marketing (broadcasts) is also blocked by UNSUBSCRIBE.
    /// </summary>
    Task<bool> IsSuppressedAsync(Guid organizationId, string email, SuppressionLane lane);

    /// <summary>Add a suppression entry. Idempotent on (org, email).</summary>
    Task SuppressAsync(Guid organizationId, string email, string reason, string? source = null);
}
