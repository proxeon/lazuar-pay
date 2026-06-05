using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Application.Commands;
using Modules.Payments.Contracts.Events;

namespace Modules.Community.Application.IntegrationEvents;

public class GatewayPaymentFailedIntegrationEventHandler 
    : IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public GatewayPaymentFailedIntegrationEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleAsync(GatewayPaymentFailedIntegrationEvent @event)
    {
        // 1. Safeguard context boundary
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "community_subscription")
        {
            return;
        }

        // 2. Extract subscription id
        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) || 
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            return;
        }

        // 3. Dispatch failure command to community domain
        var command = new TransitionSubscriptionToPastDueCommand(
            @event.OrganizationId,
            subscriptionId
        );

        await _mediator.Send(command);
    }
}
