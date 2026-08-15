using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.EventHandlers;

public class SubscriptionLifecycleIntegrationEventHandlers :
    IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionCanceledIntegrationEvent>,
    IIntegrationEventHandler<SubscriptionResumedIntegrationEvent>
{
    private readonly IEventBus _eventBus;
    private readonly ICommerceRepository _repository;
    private readonly ICrmQueryService _crmQueryService;

    public SubscriptionLifecycleIntegrationEventHandlers(
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICommerceRepository repository,
        ICrmQueryService crmQueryService)
    {
        _eventBus = eventBus;
        _repository = repository;
        _crmQueryService = crmQueryService;
    }

    public Task HandleAsync(SubscriptionActivatedIntegrationEvent @event) =>
        PublishAsync(
            @event.OrganizationId,
            @event.SubscriptionId,
            @event.ClientProfileId,
            @event.ProductId,
            "ACTIVE",
            "subscription.activated",
            @event.IsFirstPayment);

    public Task HandleAsync(SubscriptionSuspendedIntegrationEvent @event) =>
        PublishAsync(
            @event.OrganizationId,
            @event.SubscriptionId,
            @event.ClientProfileId,
            @event.ProductId,
            "SUSPENDED",
            "subscription.suspended",
            isFirstPayment: null);

    public Task HandleAsync(SubscriptionCanceledIntegrationEvent @event) =>
        PublishAsync(
            @event.OrganizationId,
            @event.SubscriptionId,
            @event.ClientProfileId,
            @event.ProductId,
            "CANCELED",
            "subscription.canceled",
            isFirstPayment: null);

    public Task HandleAsync(SubscriptionResumedIntegrationEvent @event) =>
        PublishAsync(
            @event.OrganizationId,
            @event.SubscriptionId,
            @event.ClientProfileId,
            @event.ProductId,
            "ACTIVE",
            "subscription.resumed",
            isFirstPayment: null);

    private async Task PublishAsync(
        Guid organizationId,
        Guid subscriptionId,
        Guid clientProfileId,
        Guid productId,
        string status,
        string eventType,
        bool? isFirstPayment)
    {
        var payloadElement = await BuildPayloadAsync(
            subscriptionId, clientProfileId, productId, status, isFirstPayment);

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            organizationId, TargetUrl: null, eventType, payloadElement));
        await _repository.SaveChangesAsync();
    }

    private async Task<JsonElement> BuildPayloadAsync(
        Guid subscriptionId,
        Guid clientProfileId,
        Guid productId,
        string status,
        bool? isFirstPayment)
    {
        var sub = await _repository.GetSubscriptionByIdAsync(subscriptionId);
        var product = sub != null
            ? await _repository.GetProductByIdAsync(sub.ProductId)
            : await _repository.GetProductByIdAsync(productId);
        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        var email = profile?.Email;

        if (sub != null)
        {
            return CommerceWebhookPayload.From(sub, product, email, status, isFirstPayment);
        }

        return CommerceWebhookPayload.Build(
            subscriptionId,
            clientProfileId,
            productId,
            status,
            nextBillingDate: null,
            currentPeriodEnd: null,
            email,
            product?.Price,
            product?.Currency,
            product?.Interval,
            metadata: null,
            isFirstPayment);
    }
}
