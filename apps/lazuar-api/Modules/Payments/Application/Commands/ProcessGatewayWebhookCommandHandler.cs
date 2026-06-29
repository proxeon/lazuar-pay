using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
