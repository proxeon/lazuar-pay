using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;

namespace Modules.Payments.Infrastructure.EventHandlers;

public class ExecuteOffSessionChargeIntegrationEventHandler : IIntegrationEventHandler<ExecuteOffSessionChargeIntegrationEvent>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ILogger<ExecuteOffSessionChargeIntegrationEventHandler> _logger;

    public ExecuteOffSessionChargeIntegrationEventHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        ILogger<ExecuteOffSessionChargeIntegrationEventHandler> logger)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _logger = logger;
    }

    public async Task HandleAsync(ExecuteOffSessionChargeIntegrationEvent @event)
    {
        var config = await _configRepository.GetActiveByTenantIdAsync(@event.TenantId);
        
        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            _logger.LogWarning("Cannot execute off-session charge for subscription {SubscriptionId}. Payment gateway not configured for tenant {TenantId}.", @event.SubscriptionId, @event.TenantId);
            return;
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        var success = await adapter.ChargeOffSessionAsync(
            config.ApiKey,
            @event.GatewayCustomerId,
            @event.GatewayTokenId,
            @event.Amount,
            @event.Currency,
            $"Auto-renewal for subscription {@event.SubscriptionId}",
            @event.SubscriptionId.ToString(),
            @event.DunningCampaignId);

        if (!success)
        {
            _logger.LogError("Off-session charge failed at gateway level for subscription {SubscriptionId}.", @event.SubscriptionId);
        }
    }
}
