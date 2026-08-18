using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Commerce.Contracts;
using Modules.Commerce.Domain.Aggregates;
using Modules.One.Contracts;

namespace Modules.Commerce.Application;

internal static class PortalSubscriptionAccess
{
    public static async Task<Subscription> ResolveOwnedAsync(
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        ICommerceRepository repository,
        string tenantSlug,
        string token,
        Guid subscriptionId,
        CancellationToken ct)
    {
        var tokenSubscriptionId = tokenService.ValidateToken(token);
        if (!tokenSubscriptionId.HasValue)
        {
            throw new UnauthorizedAccessException("Invalid or expired portal token.");
        }

        var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException($"Workspace with slug '{tenantSlug}' not found.");
        }

        var tokenSubscription = await repository.GetSubscriptionByIdAsync(tenantId.Value, tokenSubscriptionId.Value, ct);
        if (tokenSubscription == null || tokenSubscription.OrganizationId != tenantId.Value)
        {
            throw new InvalidOperationException("Portal session subscription not found for this workspace.");
        }

        var subscription = await repository.GetSubscriptionByIdAsync(tenantId.Value, subscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != tenantId.Value)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        if (subscription.ClientProfileId != tokenSubscription.ClientProfileId)
        {
            throw new UnauthorizedAccessException("Subscription does not belong to this portal session.");
        }

        return subscription;
    }
}
