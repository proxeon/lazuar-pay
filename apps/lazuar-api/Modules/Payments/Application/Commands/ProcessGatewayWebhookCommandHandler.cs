using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Entities;

namespace Modules.Payments.Application.Commands;

public partial class ProcessGatewayWebhookCommandHandler : ICommandHandler<ProcessGatewayWebhookCommand>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentWebhookLogRepository _logRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEventBus _eventBus;
    private readonly ISecretVault _secretVault;
    private readonly IIntegrationCheckoutSessionRepository _sessions;
    private readonly ILogger<ProcessGatewayWebhookCommandHandler> _logger;

    public ProcessGatewayWebhookCommandHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentWebhookLogRepository logRepository,
        IPaymentGatewayFactory gatewayFactory,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus,
        ISecretVault secretVault,
        IIntegrationCheckoutSessionRepository sessions,
        ILogger<ProcessGatewayWebhookCommandHandler> logger)
    {
        _configRepository = configRepository;
        _logRepository = logRepository;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
        _secretVault = secretVault;
        _sessions = sessions;
        _logger = logger;
    }

    public async Task Handle(ProcessGatewayWebhookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await HandleCoreAsync(request, cancellationToken);
        }
        catch
        {
            LazuarMetrics.RecordWebhookFailed("payment");
            throw;
        }
    }

    private async Task HandleCoreAsync(ProcessGatewayWebhookCommand request, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, request.GatewayType, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant gateway.");
        }

        // Webhooks still process when gateway is soft-disabled (credentials retained).
        var plainApiKey = _secretVault.DecryptOrPlaintextNullable(config.ApiKey) ?? "";
        var plainWebhookSecret = _secretVault.DecryptOrPlaintext(config.WebhookSecret!);

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var parsedResult = await adapter.ParseWebhookAsync(
            plainApiKey,
            plainWebhookSecret,
            request.RawBody,
            request.Headers,
            0, // estimatedFeePercentage - removed from config
            0, // fixedFee - removed from config
            0); // taxRate - removed from config

        if (!parsedResult.Verified)
        {
            throw new InvalidOperationException($"Webhook signature verification failed: {parsedResult.Error}");
        }

        if (parsedResult.EventType != "PAYMENT_COMPLETED"
            && parsedResult.EventType != "DISPUTE_CREATED"
            && parsedResult.EventType != "PAYMENT_FAILED")
        {
            return;
        }

        var alreadyProcessed = await _logRepository.HasBeenProcessedAsync(parsedResult.EventId, config.GatewayType, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var businessKey = BuildBusinessKey(parsedResult.EventType, parsedResult.GatewayTransactionId);
        if (businessKey is not null)
        {
            var businessKeyProcessed = await _logRepository.HasBusinessKeyBeenProcessedAsync(
                businessKey, config.GatewayType, cancellationToken);
            if (businessKeyProcessed)
            {
                return;
            }
        }

        // Rehydrate stripped gateway metadata from IntegrationCheckoutSession (Billplz bill id, etc.).
        var metadata = await MergeSessionMetadataAsync(
            request.TenantId,
            parsedResult.GatewayTransactionId,
            parsedResult.Metadata,
            cancellationToken);

        var log = new PaymentWebhookLog(parsedResult.EventId, config.GatewayType, businessKey);
        _logRepository.Add(log);

        if (parsedResult.EventType == "DISPUTE_CREATED")
        {
            await _eventBus.PublishAsync(new GatewayDisputeCreatedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                AmountDisputed: parsedResult.AmountPaid,
                Currency: parsedResult.Currency,
                Metadata: metadata));
            await TrySaveChangesAsync(cancellationToken);
            LogProcessed(request, parsedResult.EventId, config.GatewayType, parsedResult.GatewayTransactionId, parsedResult.EventType, metadata);
            return;
        }

        if (parsedResult.EventType == "PAYMENT_FAILED")
        {
            await _eventBus.PublishAsync(new GatewayPaymentFailedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                Metadata: metadata));
            await TrySaveChangesAsync(cancellationToken);
            LogProcessed(request, parsedResult.EventId, config.GatewayType, parsedResult.GatewayTransactionId, parsedResult.EventType, metadata);
            return;
        }

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
            Metadata: metadata,
            GatewayCustomerId: parsedResult.GatewayCustomerId,
            GatewayTokenId: parsedResult.GatewayTokenId
        );

        await _eventBus.PublishAsync(integrationEvent);
        await TrySaveChangesAsync(cancellationToken);
        LogProcessed(request, parsedResult.EventId, config.GatewayType, parsedResult.GatewayTransactionId, parsedResult.EventType, metadata);
    }
}
