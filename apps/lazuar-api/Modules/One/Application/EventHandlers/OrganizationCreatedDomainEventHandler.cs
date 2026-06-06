using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain.Events;

namespace Modules.One.Application.EventHandlers;

public class OrganizationCreatedDomainEventHandler : INotificationHandler<OrganizationCreatedDomainEvent>
{
    private readonly IEventBus _eventBus;

    public OrganizationCreatedDomainEventHandler([FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new TenantProvisionedIntegrationEvent(
            notification.OrganizationId,
            notification.Name,
            notification.Slug,
            true
        ));
    }
}
