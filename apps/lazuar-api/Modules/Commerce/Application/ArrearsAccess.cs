using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;

namespace Modules.Commerce.Application;

/// <summary>
/// Portal HMAC gate for public arrears / update-payment: valid token, workspace slug, and same sub or same org + client.
/// </summary>
public static class ArrearsAccess
{
    public static async Task<bool> IsAuthorizedAsync(
        IMagicLinkTokenService tokenService,
        ICommerceRepository repository,
        string? token,
        Guid pathSubscriptionId,
        string? tenantSlug,
        IOneQueryService one,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(tenantSlug))
        {
            return false;
        }

        var tokenSubscriptionId = tokenService.ValidateToken(token);
        if (!tokenSubscriptionId.HasValue)
        {
            return false;
        }

        var tenantId = await one.GetTenantIdBySlugAsync(tenantSlug);
        if (!tenantId.HasValue)
        {
            return false;
        }

        var tokenSubscription = await repository.GetSubscriptionByIdForPortalTokenAsync(tokenSubscriptionId.Value, ct);
        if (tokenSubscription == null || tokenSubscription.OrganizationId != tenantId.Value)
        {
            return false;
        }

        if (tokenSubscriptionId.Value == pathSubscriptionId)
        {
            return true;
        }

        var pathSubscription = await repository.GetSubscriptionByIdAsync(
            tenantId.Value, pathSubscriptionId, ct);
        if (pathSubscription == null)
        {
            return false;
        }

        return tokenSubscription.ClientProfileId == pathSubscription.ClientProfileId;
    }
}
