using MediatR;
using Modules.Messaging.Domain;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantCreatedEventHandler : INotificationHandler<TenantCreatedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public TenantCreatedEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(TenantCreatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(notification.TenantId);
        if (existing == null)
        {
            var replica = new TenantReplica(notification.TenantId, notification.Name, notification.Slug, notification.IsActive);
            await _repository.AddAsync(replica);
            await _repository.SaveChangesAsync();
        }
    }
}
