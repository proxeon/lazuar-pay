using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
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

        if (type == "commerce_subscription" && @event.Metadata.TryGetValue("subscription_id", out var subIdStr) && Guid.TryParse(subIdStr, out var subId))
        {
            var subscription = await _repository.GetSubscriptionByIdAsync(subId);
            if (subscription != null && subscription.Status != "ACTIVE")
            {
                var product = await _repository.GetProductByIdAsync(subscription.ProductId);
                if (product != null)
                {
                    var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
                    var isFirstPayment = subscription.Status == "PENDING";
                    
                    subscription.Activate(DateTime.UtcNow, nextBilling);

                    if (!string.IsNullOrEmpty(@event.GatewayCustomerId) && !string.IsNullOrEmpty(@event.GatewayTokenId))
                    {
                        subscription.StoreVaultedToken(@event.GatewayCustomerId, @event.GatewayTokenId);
                    }

                    await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                        subscription.OrganizationId,
                        subscription.Id,
                        subscription.ClientProfileId,
                        subscription.ProductId,
                        product.FulfillmentTargets.ToList(),
                        isFirstPayment
                    ));

                    await _repository.SaveChangesAsync();
                }
            }
        }
        else if (type == "commerce_order" && @event.Metadata.TryGetValue("order_id", out var orderIdStr) && Guid.TryParse(orderIdStr, out var orderId))
        {
            var order = await _repository.GetOrderByIdAsync(orderId);
            if (order != null && order.Status != "COMPLETED")
            {
                order.Complete();

                var product = await _repository.GetProductByIdAsync(order.ProductId);
                if (product != null)
                {
                    await _eventBus.PublishAsync(new OrderCompletedIntegrationEvent(
                        order.OrganizationId,
                        order.Id,
                        order.ClientProfileId,
                        order.ProductId,
                        product.FulfillmentTargets.ToList()
                    ));

                    await _repository.SaveChangesAsync();
                }
            }
        }
    }
}
