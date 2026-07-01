using System;
using System.Threading.Tasks;

namespace Modules.Communications.Contracts;

/// <summary>
/// Manages the email suppression list (unsubscribes, bounces, complaints) to protect
/// sender reputation and honor opt-outs. Cross-module callers (e.g. Messaging dispatch,
/// broadcast fan-out) use this to skip suppressed addresses before sending.
/// </summary>
public interface ISuppressionService
{
    /// <summary>True if the email is suppressed for the tenant (any reason).</summary>
    Task<bool> IsSuppressedAsync(Guid organizationId, string email);

    /// <summary>Add a suppression entry. Idempotent on (org, email).</summary>
    Task SuppressAsync(Guid organizationId, string email, string reason, string? source = null);
}
