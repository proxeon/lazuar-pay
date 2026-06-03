using BuildingBlocks.Application;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantUpdatedIntegrationEventHandler : IIntegrationEventHandler<TenantUpdatedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public TenantUpdatedIntegrationEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(TenantUpdatedIntegrationEvent @event)
    {
        var replica = await _repository.GetByIdAsync(@event.TenantId);
        if (replica != null)
        {
            replica.Update(@event.Name, @event.Slug, @event.IsActive);
            _repository.Update(replica);
            await _repository.SaveChangesAsync();
        }
    }
}
