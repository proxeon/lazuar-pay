using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application.EventHandlers;

public class OrderCompletedIntegrationEventHandler : IIntegrationEventHandler<OrderCompletedIntegrationEvent>
{
    private readonly IEventBus _eventBus;

    public OrderCompletedIntegrationEventHandler([FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task HandleAsync(OrderCompletedIntegrationEvent @event)
    {
        var payloadObj = new
        {
            order_id = @event.OrderId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            status = "COMPLETED"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        foreach (var target in @event.FulfillmentTargets)
        {
            if (target.StartsWith("internal:", System.StringComparison.OrdinalIgnoreCase))
            {
                var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                await _eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    @event.OrganizationId, internalApp, "order.completed", payloadElement));
            }
            else if (target.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                    @event.OrganizationId, target, "order.completed", payloadElement));
            }
        }
    }
}
