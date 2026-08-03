using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application.EventHandlers;

public class SubscriptionLifecycleIntegrationEventHandlers :
    IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionCanceledIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionResumedIntegrationEvent>
{
    private readonly IEventBus _eventBus;

    public SubscriptionLifecycleIntegrationEventHandlers([FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionActivatedIntegrationEvent @event)
    {
        var payloadObj = new
        {
            subscription_id = @event.SubscriptionId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            is_first_payment = @event.IsFirstPayment,
            status = "ACTIVE"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId, TargetUrl: null, "subscription.activated", payloadElement));
    }

    public async Task HandleAsync(SubscriptionSuspendedIntegrationEvent @event)
    {
        var payloadObj = new
        {
            subscription_id = @event.SubscriptionId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            status = "SUSPENDED"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId, TargetUrl: null, "subscription.suspended", payloadElement));
    }

    public async Task HandleAsync(SubscriptionCanceledIntegrationEvent @event)
    {
        var payloadObj = new
        {
            subscription_id = @event.SubscriptionId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            status = "CANCELED"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId, TargetUrl: null, "subscription.canceled", payloadElement));
    }

    public async Task HandleAsync(SubscriptionResumedIntegrationEvent @event)
    {
        var payloadObj = new
        {
            subscription_id = @event.SubscriptionId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            status = "ACTIVE"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId, TargetUrl: null, "subscription.resumed", payloadElement));
    }
}
