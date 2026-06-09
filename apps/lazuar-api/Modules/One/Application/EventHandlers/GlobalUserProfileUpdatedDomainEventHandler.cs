using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain.Events;

namespace Modules.One.Application.EventHandlers;

public class GlobalUserProfileUpdatedDomainEventHandler : INotificationHandler<GlobalUserProfileUpdatedDomainEvent>
{
    private readonly IEventBus _eventBus;

    public GlobalUserProfileUpdatedDomainEventHandler([FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(GlobalUserProfileUpdatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(new GlobalUserProfileUpdatedIntegrationEvent(
            notification.UserId,
            notification.Email,
            notification.Name
        ));
    }
}
