using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class RequestPortalMagicLinkCommandHandler : ICommandHandler<RequestPortalMagicLinkCommand>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly ICrmQueryService _crmQueryService;
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public RequestPortalMagicLinkCommandHandler(
        IOneQueryService oneQueryService,
        ICrmQueryService crmQueryService,
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _oneQueryService = oneQueryService;
        _crmQueryService = crmQueryService;
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(RequestPortalMagicLinkCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            return;
        }

        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        if (!tenantId.HasValue)
        {
            return;
        }

        var profile = await _crmQueryService.GetClientProfileByEmailAsync(tenantId.Value, request.Email);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Email) || !Guid.TryParse(profile.Id, out var profileId))
        {
            return;
        }

        var subscription = await _repository.GetNewestSubscriptionForClientAsync(tenantId.Value, profileId, ct);
        if (subscription == null)
        {
            return;
        }

        await _eventBus.PublishAsync(new PortalMagicLinkRequestedIntegrationEvent(
            tenantId.Value,
            subscription.Id,
            profileId));
        await _repository.SaveChangesAsync(ct);
    }
}
