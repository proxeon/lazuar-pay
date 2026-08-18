using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;
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
    private readonly IBillingQueryService? _billingQueryService;

    public SubscriptionLifecycleIntegrationEventHandlers(
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICommerceRepository repository,
        ICrmQueryService crmQueryService,
        IBillingQueryService? billingQueryService = null)
    {
        _eventBus = eventBus;
        _repository = repository;
        _crmQueryService = crmQueryService;
        _billingQueryService = billingQueryService;
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
            organizationId, subscriptionId, clientProfileId, productId, status, eventType, isFirstPayment);

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            organizationId, TargetUrl: null, eventType, payloadElement));
        await _repository.SaveChangesAsync();
    }

    private async Task<JsonElement> BuildPayloadAsync(
        Guid organizationId,
        Guid subscriptionId,
        Guid clientProfileId,
        Guid productId,
        string status,
        string eventType,
        bool? isFirstPayment)
    {
        var sub = await _repository.GetSubscriptionByIdAsync(organizationId, subscriptionId);
        var product = sub != null
            ? await _repository.GetProductByIdAsync(sub.OrganizationId, sub.ProductId)
            : await _repository.GetProductByIdAsync(organizationId, productId);
        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        var email = profile?.Email;

        if (sub != null)
        {
            var payloadStatus = eventType == "subscription.activated" ? sub.Status : status;
            var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
                _billingQueryService, sub.OrganizationId);
            return CommerceWebhookPayload.From(
                sub, product, email, payloadStatus, isFirstPayment, merchantHasSst: merchantHasSst);
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
