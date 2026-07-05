using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;

namespace Modules.Payments.Infrastructure.EventHandlers;

public class GatewayRefundRequestedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundRequestedIntegrationEvent>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEventBus _eventBus;

    public GatewayRefundRequestedIntegrationEventHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(GatewayRefundRequestedIntegrationEvent @event)
    {
        var config = await _configRepository.GetByTenantAndGatewayAsync(@event.OrganizationId, @event.GatewayName);
        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Payment configuration not found or inactive."));
            return;
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        
        var success = await adapter.IssueRefundAsync(config.ApiKey, @event.GatewayTransactionId, 0);
        if (success)
        {
            await _eventBus.PublishAsync(new GatewayRefundCompletedIntegrationEvent(
                OrganizationId: @event.OrganizationId,
                SubscriptionId: @event.SubscriptionId,
                PaymentRecordId: @event.PaymentRecordId,
                RefundedAmount: 0,
                Currency: "MYR",
                RefundedFee: 0m,
                NetRefundedAmount: 0m
            ));
        }
        else
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Gateway adapter failed to issue refund."));
        }
    }
}
