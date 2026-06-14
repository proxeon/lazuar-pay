using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Application.Commands;
using Modules.Payments.Contracts.Events;

namespace Modules.Community.Application.IntegrationEvents;

public class GatewayPaymentCompletedIntegrationEventHandler
    : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;

    public GatewayPaymentCompletedIntegrationEventHandler(
        IMediator mediator,
        ICommunitySubscriptionRepository subscriptionRepository)
    {
        _mediator = mediator;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "community_subscription")
        {
            return;
        }

        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) ||
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            throw new InvalidOperationException("Gateway payment completed for community, but missing valid subscription_id in metadata.");
        }

        if (!string.IsNullOrEmpty(@event.GatewayTokenId) && !string.IsNullOrEmpty(@event.GatewayCustomerId))
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription != null && subscription.OrganizationId == @event.OrganizationId)
            {
                subscription.StoreVaultedToken(@event.GatewayCustomerId, @event.GatewayTokenId);
                await _subscriptionRepository.SaveChangesAsync();
            }
        }

        var command = new RecordSubscriptionPaymentCommand(
            OrganizationId: @event.OrganizationId,
            SubscriptionId: subscriptionId,
            Amount: @event.AmountPaid,
            Currency: @event.Currency,
            PaymentMethod: "ONLINE_GATEWAY",
            ExternalReference: @event.GatewayTransactionId,
            RecordedBy: "SYSTEM"
        );

        await _mediator.Send(command);
    }
}
