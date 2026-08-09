using System;

namespace Modules.Commerce.Contracts;

/// <summary>
/// Portal magic-link tokens scoped to a Commerce subscription id.
/// HMAC wire format is owned by Commerce; Communications mints for dunning URLs via this port.
/// </summary>
public interface IMagicLinkTokenService
{
    string GenerateToken(Guid subscriptionId);
    Guid? ValidateToken(string token);
}
