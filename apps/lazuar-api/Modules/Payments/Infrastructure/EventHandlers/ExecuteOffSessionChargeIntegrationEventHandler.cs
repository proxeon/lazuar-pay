using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Payments.Infrastructure.EventHandlers;

public class ExecuteOffSessionChargeIntegrationEventHandler : IIntegrationEventHandler<ExecuteOffSessionChargeIntegrationEvent>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEventBus _eventBus;
    private readonly ISecretVault _secretVault;
    private readonly ILogger<ExecuteOffSessionChargeIntegrationEventHandler> _logger;

    public ExecuteOffSessionChargeIntegrationEventHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus,
        ISecretVault secretVault,
        ILogger<ExecuteOffSessionChargeIntegrationEventHandler> logger)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task HandleAsync(ExecuteOffSessionChargeIntegrationEvent @event)
    {
        if (!PaymentGatewayCapabilities.SupportsOffSession(@event.GatewayName))
        {
            _logger.LogWarning(
                "Off-session charge skipped for subscription {SubscriptionId}: gateway {GatewayName} does not support vaulted charges.",
                @event.SubscriptionId, @event.GatewayName);
            await PublishPaymentFailedAsync(@event, failureReason: "off_session_not_supported");
            return;
        }

        var config = await _configRepository.GetByTenantAndGatewayAsync(@event.TenantId, @event.GatewayName);

        if (config == null || string.IsNullOrEmpty(config.ApiKey) || !config.IsActive)
        {
            _logger.LogWarning(
                "Cannot execute off-session charge for subscription {SubscriptionId}. Gateway {GatewayName} not configured or inactive for tenant {TenantId}.",
                @event.SubscriptionId, @event.GatewayName, @event.TenantId);

            await PublishPaymentFailedAsync(@event, failureReason: "gateway_not_configured");
            return;
        }

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        bool success;
        try
        {
            success = await adapter.ChargeOffSessionAsync(
                plainApiKey,
                @event.GatewayCustomerId,
                @event.GatewayTokenId,
                @event.Amount,
                @event.Currency,
                $"Auto-renewal for subscription {@event.SubscriptionId}",
                @event.SubscriptionId.ToString(),
                @event.TenantId,
                @event.DunningCampaignId,
                @event.Id.ToString());
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "Off-session not supported for gateway {GatewayName} subscription {SubscriptionId}.",
                @event.GatewayName, @event.SubscriptionId);
            await PublishPaymentFailedAsync(@event, failureReason: "off_session_not_supported");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Off-session charge threw for subscription {SubscriptionId} on gateway {GatewayName}.",
                @event.SubscriptionId, @event.GatewayName);
            await PublishPaymentFailedAsync(@event, failureReason: "charge_exception");
            return;
        }

        if (!success)
        {
            _logger.LogError("Off-session charge failed at gateway level for subscription {SubscriptionId}.", @event.SubscriptionId);
            await PublishPaymentFailedAsync(@event, failureReason: "charge_declined");
        }
    }

    private async Task PublishPaymentFailedAsync(ExecuteOffSessionChargeIntegrationEvent @event, string failureReason)
    {
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = @event.SubscriptionId.ToString(),
            ["tenant_id"] = @event.TenantId.ToString(),
            ["receipt"] = @event.SubscriptionId.ToString(),
            ["failure_source"] = "off_session",
            ["failure_reason"] = failureReason,
            ["gateway_name"] = @event.GatewayName
        };

        if (@event.DunningCampaignId.HasValue)
        {
            metadata["dunning_campaign_id"] = @event.DunningCampaignId.Value.ToString();
        }

        if (@event.ChargeAttemptId.HasValue)
        {
            metadata["charge_attempt_id"] = @event.ChargeAttemptId.Value.ToString();
        }

        await _eventBus.PublishAsync(new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: @event.TenantId,
            GatewayTransactionId: "off_session:" + @event.SubscriptionId,
            Metadata: metadata));
    }
}
