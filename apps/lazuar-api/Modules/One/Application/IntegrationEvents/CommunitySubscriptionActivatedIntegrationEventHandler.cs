using BuildingBlocks.Application;
using Modules.Community.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.IntegrationEvents;

public class CommunitySubscriptionActivatedIntegrationEventHandler : IIntegrationEventHandler<CommunitySubscriptionActivatedIntegrationEvent>
{
    private readonly IOneRepository _repository;

    public CommunitySubscriptionActivatedIntegrationEventHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(CommunitySubscriptionActivatedIntegrationEvent @event)
    {
        if (!@event.GlobalUserId.HasValue)
            return; // Was a manual admin entry with no global identity attached

        var exists = await _repository.HasMembershipAsync(@event.GlobalUserId.Value, @event.OrganizationId);

        if (!exists)
        {
            var membership = new TenantMembership(@event.GlobalUserId.Value, @event.OrganizationId, "CLIENT");
            _repository.AddTenantMembership(membership);
            await _repository.SaveChangesAsync();
        }
    }
}
