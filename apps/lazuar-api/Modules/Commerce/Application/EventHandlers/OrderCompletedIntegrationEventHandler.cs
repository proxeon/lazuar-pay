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

        // Workspace fan-out: One delivers to all active endpoints (no product-URL match).
        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId, TargetUrl: null, "order.completed", payloadElement));
    }
}
