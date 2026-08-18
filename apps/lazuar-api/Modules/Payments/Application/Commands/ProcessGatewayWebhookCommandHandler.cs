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
            if (parsedResult.UnusableAfterVerify)
            {
                throw new PaymentWebhookUnusablePayloadException(
                    parsedResult.Error ?? "Unusable webhook payload.");
            }

            throw new InvalidOperationException($"Webhook signature verification failed: {parsedResult.Error}");
        }

        if (parsedResult.EventType != "PAYMENT_COMPLETED"
            && parsedResult.EventType != "DISPUTE_CREATED"
            && parsedResult.EventType != "DISPUTE_CLOSED"
            && parsedResult.EventType != "PAYMENT_FAILED"
            && parsedResult.EventType != "REFUND_COMPLETED")
        {
            return;
        }

        if (TryGetInboundTenantId(parsedResult.Metadata, out var inboundTenant)
            && inboundTenant != request.TenantId
            && !IsPlatformCheckoutWebhook(request.TenantId, parsedResult.Metadata))
        {
            _logger.LogWarning(
                "Rejected payment webhook: inbound tenant_id {InboundTenant} does not match URL tenant {UrlTenant}.",
                inboundTenant, request.TenantId);
            return;
        }

        var businessKey = BuildBusinessKey(parsedResult.EventType, parsedResult.GatewayTransactionId);
        var existing = await _logRepository.GetByEventIdAsync(
            parsedResult.EventId, config.GatewayType, request.TenantId, cancellationToken);
        if (existing is null && businessKey is not null)
        {
            existing = await _logRepository.GetByBusinessKeyAsync(
                businessKey, config.GatewayType, request.TenantId, cancellationToken);
        }

        if (existing is not null)
        {
            await HandleExistingLogAsync(request, parsedResult, existing, cancellationToken);
            return;
        }

        if (parsedResult.EventType == "PAYMENT_FAILED"
            && !string.IsNullOrWhiteSpace(parsedResult.GatewayTransactionId))
        {
            var completed = await _logRepository.GetByBusinessKeyAsync(
                "PAYMENT_COMPLETED:" + parsedResult.GatewayTransactionId,
                config.GatewayType,
                request.TenantId,
                cancellationToken);
            if (completed is not null)
            {
                _logger.LogInformation(
                    "Ignoring late PAYMENT_FAILED after PAYMENT_COMPLETED for {GatewayTransactionId}.",
                    parsedResult.GatewayTransactionId);
                return;
            }
        }

        // Rehydrate stripped gateway metadata from IntegrationCheckoutSession (Billplz bill id, etc.).
        var metadata = await MergeSessionMetadataAsync(
            request.TenantId,
            parsedResult.GatewayTransactionId,
            parsedResult.Metadata,
            cancellationToken);

        var log = new PaymentWebhookLog(
            parsedResult.EventId, config.GatewayType, businessKey, organizationId: request.TenantId);
        _logRepository.Add(log);
        await PublishParsedEventAsync(request, parsedResult, metadata, log);
        await TrySaveChangesAsync(cancellationToken);
        LogProcessed(request, parsedResult.EventId, config.GatewayType, parsedResult.GatewayTransactionId, parsedResult.EventType, metadata);
    }

    private async Task HandleExistingLogAsync(
        ProcessGatewayWebhookCommand request,
        GatewayWebhookParsedResult parsedResult,
        PaymentWebhookLog existing,
        CancellationToken cancellationToken)
    {
        // Pre-ticket backfill / seed rows have no outbox correlation — do not invent work.
        if (existing.OutboxMessageId is null)
        {
            return;
        }

        var requeue = await _logRepository.TryRequeueDeadOutboxAsync(
            existing.OutboxMessageId.Value, cancellationToken);
        if (requeue == OutboxRequeueResult.AlreadyActive)
        {
            return;
        }

        if (requeue == OutboxRequeueResult.Requeued)
        {
            _logger.LogInformation(
                "Re-queued Dead payment webhook outbox. EventId={EventId} Provider={Provider} OutboxMessageId={OutboxMessageId}",
                existing.EventId,
                existing.Provider,
                existing.OutboxMessageId);
            return;
        }

        var metadata = await MergeSessionMetadataAsync(
            request.TenantId,
            parsedResult.GatewayTransactionId,
            parsedResult.Metadata,
            cancellationToken);
        await PublishParsedEventAsync(request, parsedResult, metadata, existing);
        await _logRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Re-published payment webhook after missing outbox. EventId={EventId} Provider={Provider} OutboxMessageId={OutboxMessageId}",
            existing.EventId,
            existing.Provider,
            existing.OutboxMessageId);
    }

    private async Task PublishParsedEventAsync(
        ProcessGatewayWebhookCommand request,
        GatewayWebhookParsedResult parsedResult,
        Dictionary<string, string> metadata,
        PaymentWebhookLog log)
    {
        if (parsedResult.EventType == "DISPUTE_CREATED")
        {
            var disputeEvent = new GatewayDisputeCreatedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                AmountDisputed: parsedResult.AmountPaid,
                Currency: parsedResult.Currency,
                Metadata: metadata);
            log.AssignOutboxMessageId(disputeEvent.Id);
            await _eventBus.PublishAsync(disputeEvent);
            return;
        }

        if (parsedResult.EventType == "DISPUTE_CLOSED")
        {
            metadata.TryGetValue("dispute_outcome", out var outcome);
            var closedEvent = new GatewayDisputeClosedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                Outcome: outcome ?? "closed",
                Metadata: metadata,
                Amount: parsedResult.AmountPaid);
            log.AssignOutboxMessageId(closedEvent.Id);
            await _eventBus.PublishAsync(closedEvent);
            return;
        }

        if (parsedResult.EventType == "PAYMENT_FAILED")
        {
            var failedEvent = new GatewayPaymentFailedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                Metadata: metadata);
            log.AssignOutboxMessageId(failedEvent.Id);
            await _eventBus.PublishAsync(failedEvent);
            return;
        }

        if (parsedResult.EventType == "REFUND_COMPLETED")
        {
            var refunded = parsedResult.AmountPaid;
            var refundEvent = new GatewayRefundCompletedIntegrationEvent(
                OrganizationId: request.TenantId,
                SubscriptionId: Guid.Empty,
                PaymentRecordId: Guid.Empty,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                RefundedAmount: refunded,
                Currency: parsedResult.Currency,
                RefundedFee: 0m,
                NetRefundedAmount: refunded,
                TaxAmount: 0m,
                IsFullRefund: false,
                FxRate: parsedResult.FxRate,
                BaseCurrency: parsedResult.BaseCurrency,
                RefundId: parsedResult.EventId);
            log.AssignOutboxMessageId(refundEvent.Id);
            await _eventBus.PublishAsync(refundEvent);
            return;
        }

        var completedEvent = new GatewayPaymentCompletedIntegrationEvent(
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
            GatewayTokenId: parsedResult.GatewayTokenId);
        log.AssignOutboxMessageId(completedEvent.Id);
        await _eventBus.PublishAsync(completedEvent);
    }
}
