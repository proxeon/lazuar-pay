using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain.Events;

namespace Modules.One.Application.EventHandlers;

public class OrganizationUpdatedDomainEventHandler : INotificationHandler<OrganizationUpdatedDomainEvent>
{
    private readonly IEventBus _eventBus;

    public OrganizationUpdatedDomainEventHandler([FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(OrganizationUpdatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new WorkspaceUpdatedIntegrationEvent(
            notification.OrganizationId,
            notification.Name,
            notification.Slug
        ));
    }
}
