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
    private readonly ISecretVault _secretVault;

    public GatewayRefundRequestedIntegrationEventHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus,
        ISecretVault secretVault)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
        _secretVault = secretVault;
    }

    public async Task HandleAsync(GatewayRefundRequestedIntegrationEvent @event)
    {
        // Refunds still allowed when soft-disabled (historical payment obligations).
        var config = await _configRepository.GetByTenantAndGatewayAsync(@event.OrganizationId, @event.GatewayName);
        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Payment configuration not found or inactive."));
            return;
        }

        if (@event.Amount <= 0)
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Refund amount must be greater than zero."));
            return;
        }

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var success = await adapter.IssueRefundAsync(plainApiKey, @event.GatewayTransactionId, @event.Amount);
        if (success)
        {
            // Gateway adapters currently do not return reclaimed fee; treat fee as 0 until webhook enrichment exists.
            var refundedFee = 0m;
            var netRefunded = @event.Amount - refundedFee;

            await _eventBus.PublishAsync(new GatewayRefundCompletedIntegrationEvent(
                OrganizationId: @event.OrganizationId,
                SubscriptionId: @event.SubscriptionId,
                PaymentRecordId: @event.PaymentRecordId,
                GatewayTransactionId: @event.GatewayTransactionId,
                RefundedAmount: @event.Amount,
                Currency: @event.Currency,
                RefundedFee: refundedFee,
                NetRefundedAmount: netRefunded,
                TaxAmount: @event.TaxAmount,
                IsFullRefund: @event.IsFullRefund
            ));
        }
        else
        {
            await _eventBus.PublishAsync(new GatewayRefundFailedIntegrationEvent(
                @event.OrganizationId, @event.SubscriptionId, @event.PaymentRecordId, "Gateway adapter failed to issue refund."));
        }
    }
}
