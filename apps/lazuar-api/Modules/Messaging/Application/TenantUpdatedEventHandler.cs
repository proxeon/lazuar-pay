using MediatR;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantUpdatedEventHandler : INotificationHandler<TenantUpdatedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public TenantUpdatedEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(TenantUpdatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var replica = await _repository.GetByIdAsync(notification.TenantId);
        if (replica != null)
        {
            replica.Update(notification.Name, notification.Slug, notification.IsActive);
            _repository.Update(replica);
            await _repository.SaveChangesAsync();
        }
    }
}
