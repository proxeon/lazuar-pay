using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CancelPortalSubscriptionCommandHandler : ICommandHandler<CancelPortalSubscriptionCommand>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public CancelPortalSubscriptionCommandHandler(
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(CancelPortalSubscriptionCommand request, CancellationToken ct)
    {
        var tokenSubscriptionId = _tokenService.ValidateToken(request.Token);
        if (!tokenSubscriptionId.HasValue)
        {
            throw new UnauthorizedAccessException("Invalid or expired portal token.");
        }

        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException($"Workspace with slug '{request.TenantSlug}' not found.");
        }

        var tokenSubscription = await _repository.GetSubscriptionByIdAsync(tokenSubscriptionId.Value, ct);
        if (tokenSubscription == null || tokenSubscription.OrganizationId != tenantId.Value)
        {
            throw new InvalidOperationException("Portal session subscription not found for this workspace.");
        }

        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != tenantId.Value)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        if (subscription.ClientProfileId != tokenSubscription.ClientProfileId)
        {
            throw new UnauthorizedAccessException("Subscription does not belong to this portal session.");
        }

        if (subscription.Status == "CANCELED")
        {
            return;
        }

        if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED"))
        {
            throw new InvalidOperationException($"Subscription cannot be canceled from status '{subscription.Status}'.");
        }

        subscription.Cancel();

        var product = await _repository.GetProductByIdAsync(subscription.ProductId, ct);
        var fulfillmentTargets = product?.FulfillmentTargets.ToList() ?? [];

        await _eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
            subscription.OrganizationId,
            subscription.Id,
            subscription.ClientProfileId,
            subscription.ProductId,
            fulfillmentTargets));

        await _repository.SaveChangesAsync(ct);
    }
}
