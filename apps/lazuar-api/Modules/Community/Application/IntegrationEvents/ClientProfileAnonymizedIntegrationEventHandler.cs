using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Application.Commands;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.IntegrationEvents;

public class ClientProfileAnonymizedIntegrationEventHandler
    : IIntegrationEventHandler<ClientProfileAnonymizedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;

    public ClientProfileAnonymizedIntegrationEventHandler(
        IMediator mediator,
        ICommunitySubscriptionRepository subscriptionRepository)
    {
        _mediator = mediator;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(ClientProfileAnonymizedIntegrationEvent @event)
    {
        var subscriptionIds = await _subscriptionRepository.GetSubscriptionIdsByProfileIdAsync(
            @event.OrganizationId,
            @event.ClientProfileId);

        foreach (var subscriptionId in subscriptionIds)
        {
            var command = new BanSubscriberCommand(@event.OrganizationId, subscriptionId);
            await _mediator.Send(command);
        }
    }
}
