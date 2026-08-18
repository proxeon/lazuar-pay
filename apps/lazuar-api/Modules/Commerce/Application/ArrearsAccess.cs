using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Commerce.Contracts;

namespace Modules.Commerce.Application;

/// <summary>
/// Portal HMAC gate for public arrears / update-payment: valid token and same sub, or same org + client.
/// </summary>
public static class ArrearsAccess
{
    public static async Task<bool> IsAuthorizedAsync(
        IMagicLinkTokenService tokenService,
        ICommerceRepository repository,
        string? token,
        Guid pathSubscriptionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenSubscriptionId = tokenService.ValidateToken(token);
        if (!tokenSubscriptionId.HasValue)
        {
            return false;
        }

        if (tokenSubscriptionId.Value == pathSubscriptionId)
        {
            return true;
        }

        var tokenSubscription = await repository.GetSubscriptionByIdForPortalTokenAsync(tokenSubscriptionId.Value, ct);
        if (tokenSubscription == null)
        {
            return false;
        }

        var pathSubscription = await repository.GetSubscriptionByIdAsync(
            tokenSubscription.OrganizationId, pathSubscriptionId, ct);
        if (pathSubscription == null)
        {
            return false;
        }

        return tokenSubscription.ClientProfileId == pathSubscription.ClientProfileId;
    }
}
