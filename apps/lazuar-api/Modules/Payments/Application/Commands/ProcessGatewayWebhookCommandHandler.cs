// apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Entities;

namespace Modules.Payments.Application.Commands;

public class ProcessGatewayWebhookCommandHandler : ICommandHandler<ProcessGatewayWebhookCommand>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentWebhookLogRepository _logRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEventBus _eventBus;

    public ProcessGatewayWebhookCommandHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentWebhookLogRepository logRepository,
        IPaymentGatewayFactory gatewayFactory,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus)
    {
        _configRepository = configRepository;
        _logRepository = logRepository;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
    }

    public async Task Handle(ProcessGatewayWebhookCommand request, CancellationToken cancellationToken)
    {
        // 1. Process Platform Utility Credit Top-Ups First
        // System webhooks bypass tenant DB lookups because they route through the Lazuar Platform Stripe Account
        if (request.GatewayType == "SYSTEM_STRIPE")
        {
            var systemAdapter = _gatewayFactory.GetAdapter("STRIPE");
            var systemResult = await systemAdapter.ParseWebhookAsync(
                "", // API Key not needed for verification
                Environment.GetEnvironmentVariable("LAZUAR_SYSTEM_STRIPE_WEBHOOK_SECRET") ?? "", 
                request.RawBody, 
                request.Headers);

            if (!systemResult.Verified) throw new InvalidOperationException($"System webhook verification failed: {systemResult.Error}");
            if (systemResult.EventType != "PAYMENT_COMPLETED") return;

            var systemProcessed = await _logRepository.HasBeenProcessedAsync(systemResult.EventId, "SYSTEM_STRIPE", cancellationToken);
            if (systemProcessed) return;

            _logRepository.Add(new PaymentWebhookLog(systemResult.EventId, "SYSTEM_STRIPE"));

            if (systemResult.Metadata.TryGetValue("type", out var type) && type == "utility_credit_topup")
            {
                if (systemResult.Metadata.TryGetValue("tenant_id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                {
                    var credits = 0;
                    if (systemResult.AmountPaid >= 50) credits = 500;
                    if (systemResult.AmountPaid >= 100) credits = 1100;
                    if (systemResult.AmountPaid >= 200) credits = 2500;

                    if (credits > 0)
                    {
                        await _eventBus.PublishAsync(new ApiCreditPurchasedIntegrationEvent(
                            tenantId,
                            credits,
                            systemResult.AmountPaid,
                            systemResult.Currency,
                            systemResult.GatewayTransactionId ?? systemResult.EventId
                        ));
                    }
                }
            }

            await _logRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        // 2. Process Standard Tenant Webhooks
        var config = await _configRepository.GetActiveByTenantIdAsync(request.TenantId, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant.");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var parsedResult = await adapter.ParseWebhookAsync(
            config.ApiKey ?? "",
            config.WebhookSecret, 
            request.RawBody, 
            request.Headers,
            config.EstimatedFeePercentage,
            config.FixedFee,
            config.TaxRate);

        if (!parsedResult.Verified)
        {
            throw new InvalidOperationException($"Webhook signature verification failed: {parsedResult.Error}");
        }

        if (parsedResult.EventType != "PAYMENT_COMPLETED")
        {
            return;
        }

        var alreadyProcessed = await _logRepository.HasBeenProcessedAsync(parsedResult.EventId, config.GatewayType, cancellationToken);
        if (alreadyProcessed)
        {
            return; 
        }

        var log = new PaymentWebhookLog(parsedResult.EventId, config.GatewayType);
        _logRepository.Add(log);

        var integrationEvent = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: request.TenantId,
            GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
            AmountPaid: parsedResult.AmountPaid,
            Currency: parsedResult.Currency,
            GatewayFee: parsedResult.GatewayFee,
            TaxAmount: parsedResult.TaxAmount,
            NetAmount: parsedResult.NetAmount,
            FxRate: parsedResult.FxRate,
            BaseCurrency: parsedResult.BaseCurrency,
            LineItems: new List<LineItemDto>(),
            Metadata: parsedResult.Metadata,
            GatewayCustomerId: parsedResult.GatewayCustomerId,
            GatewayTokenId: parsedResult.GatewayTokenId
        );

        await _eventBus.PublishAsync(integrationEvent);
        await _logRepository.SaveChangesAsync(cancellationToken);
    }
}
