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

        var tokenSubscription = await repository.GetSubscriptionByIdAsync(tokenSubscriptionId.Value, ct);
        var pathSubscription = await repository.GetSubscriptionByIdAsync(pathSubscriptionId, ct);
        if (tokenSubscription == null || pathSubscription == null)
        {
            return false;
        }

        return tokenSubscription.OrganizationId == pathSubscription.OrganizationId
            && tokenSubscription.ClientProfileId == pathSubscription.ClientProfileId;
    }
}
