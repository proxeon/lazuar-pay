using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application.EventHandlers;

public class SubscriptionLifecycleIntegrationEventHandlers : 
    IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionCanceledIntegrationEvent>
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

        foreach (var target in @event.FulfillmentTargets)
        {
            if (target.StartsWith("internal:", System.StringComparison.OrdinalIgnoreCase))
            {
                var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                await _eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    @event.OrganizationId, internalApp, "subscription.activated", payloadElement));
            }
            else if (target.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                    @event.OrganizationId, target, "subscription.activated", payloadElement));
            }
        }
    }

    public async Task HandleAsync(SubscriptionSuspendedIntegrationEvent @event)
    {
        var payloadObj = new
        {
            subscription_id = @event.SubscriptionId.ToString(),
            client_profile_id = @event.ClientProfileId.ToString(),
            product_id = @event.ProductId.ToString(),
            status = "PAST_DUE"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        foreach (var target in @event.FulfillmentTargets)
        {
            if (target.StartsWith("internal:", System.StringComparison.OrdinalIgnoreCase))
            {
                var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                await _eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    @event.OrganizationId, internalApp, "subscription.suspended", payloadElement));
            }
            else if (target.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                    @event.OrganizationId, target, "subscription.suspended", payloadElement));
            }
        }
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

        foreach (var target in @event.FulfillmentTargets)
        {
            if (target.StartsWith("internal:", System.StringComparison.OrdinalIgnoreCase))
            {
                var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                await _eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    @event.OrganizationId, internalApp, "subscription.canceled", payloadElement));
            }
            else if (target.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                    @event.OrganizationId, target, "subscription.canceled", payloadElement));
            }
        }
    }
}
