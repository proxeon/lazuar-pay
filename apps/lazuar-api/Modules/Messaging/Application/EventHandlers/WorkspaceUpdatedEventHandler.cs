using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Modules.Messaging.Domain;
using Modules.One.Contracts;

namespace Modules.Messaging.Application.EventHandlers;

public class WorkspaceUpdatedEventHandler : INotificationHandler<WorkspaceUpdatedIntegrationEvent>
{
    private readonly ITenantReplicaRepository _repository;

    public WorkspaceUpdatedEventHandler(ITenantReplicaRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(WorkspaceUpdatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var replica = await _repository.GetByIdAsync(notification.OrganizationId);

        if (replica != null)
        {
            replica.Update(notification.Name, notification.Slug, replica.IsActive);

            _repository.Update(replica);
            await _repository.SaveChangesAsync();
        }
    }
}
