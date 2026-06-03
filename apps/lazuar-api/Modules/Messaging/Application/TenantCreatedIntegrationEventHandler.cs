using BuildingBlocks.Application;
using Modules.Messaging.Domain;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantCreatedIntegrationEventHandler : IIntegrationEventHandler<TenantCreatedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public TenantCreatedIntegrationEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(TenantCreatedIntegrationEvent @event)
    {
        var existing = await _repository.GetByIdAsync(@event.TenantId);
        if (existing == null)
        {
            var replica = new TenantReplica(@event.TenantId, @event.Name, @event.Slug, @event.IsActive);
            await _repository.AddAsync(replica);
            await _repository.SaveChangesAsync();
        }
    }
}
