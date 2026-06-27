using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class GatewayPaymentCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public GatewayPaymentCompletedIntegrationEventHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var type = @event.Metadata.GetValueOrDefault("type");

        if (type == "commerce_subscription" && @event.Metadata.TryGetValue("subscription_id", out var sessionIdStr) && Guid.TryParse(sessionIdStr, out var sessionId))
        {
            // Retrieve the active checkout session representing the purchase intent
            var session = await _repository.GetCheckoutSessionByIdAsync(sessionId);
            if (session == null || session.Status == "COMPLETED")
            {
                return;
            }

            var product = await _repository.GetProductByIdAsync(session.ProductId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {session.ProductId} associated with session {sessionId} not found.");
            }

            // Complete the checkout session to resolve polling status API
            session.Complete();

            if (product.Interval != "one_time")
            {
                // Provision a brand-new recurring subscription on successful payment
                var subscription = new Subscription(
                    session.OrganizationId,
                    session.ClientProfileId,
                    product.Id
                );

                var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
                subscription.Activate(DateTime.UtcNow, nextBilling);

                if (!string.IsNullOrEmpty(@event.GatewayCustomerId) && !string.IsNullOrEmpty(@event.GatewayTokenId))
                {
                    subscription.StoreVaultedToken(@event.GatewayCustomerId, @event.GatewayTokenId);
                }

                _repository.AddSubscription(subscription);

                await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                    subscription.OrganizationId,
                    subscription.Id,
                    subscription.ClientProfileId,
                    subscription.ProductId,
                    product.FulfillmentTargets.ToList(),
                    true
                ));
            }
            else
            {
                // Provision a brand-new one-time purchase order
                var order = new Order(
                    session.OrganizationId,
                    session.ClientProfileId,
                    product.Id,
                    @event.AmountPaid,
                    product.Currency
                );

                order.Complete();
                _repository.AddOrder(order);

                await _eventBus.PublishAsync(new OrderCompletedIntegrationEvent(
                    order.OrganizationId,
                    order.Id,
                    order.ClientProfileId,
                    order.ProductId,
                    product.FulfillmentTargets.ToList()
                ));
            }

            await _repository.SaveChangesAsync();
        }
    }
}
