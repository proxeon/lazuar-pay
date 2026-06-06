using MediatR;
using Modules.Messaging.Domain;
using Modules.One.Contracts;

namespace Modules.Messaging.Application;

public class TenantCreatedEventHandler : INotificationHandler<TenantProvisionedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public TenantCreatedEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(TenantProvisionedIntegrationEvent notification, CancellationToken cancellationToken)
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
