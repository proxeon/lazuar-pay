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
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;

    public GatewayPaymentFailedIntegrationEventHandler(
        IMediator mediator,
        ICommunitySubscriptionRepository subscriptionRepository)
    {
        _mediator = mediator;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(GatewayPaymentFailedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "community_subscription")
        {
            return;
        }

        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) ||
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            return;
        }

        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
        if (subscription != null && subscription.OrganizationId == @event.OrganizationId)
        {
            if (!string.IsNullOrEmpty(subscription.VaultedTokenId))
            {
                subscription.ClearVaultedToken();
                await _subscriptionRepository.SaveChangesAsync();
            }
        }

        var command = new TransitionSubscriptionToPastDueCommand(
            @event.OrganizationId,
            subscriptionId
        );

        await _mediator.Send(command);
    }
}
