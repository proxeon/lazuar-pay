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
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, request.GatewayType, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant gateway.");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var parsedResult = await adapter.ParseWebhookAsync(
            config.ApiKey ?? "",
            config.WebhookSecret,
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

        var log = new PaymentWebhookLog(parsedResult.EventId, config.GatewayType, businessKey);
        _logRepository.Add(log);

        if (parsedResult.EventType == "DISPUTE_CREATED")
        {
            await _eventBus.PublishAsync(new GatewayDisputeCreatedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                AmountDisputed: parsedResult.AmountPaid,
                Currency: parsedResult.Currency,
                Metadata: parsedResult.Metadata));
            await TrySaveChangesAsync(cancellationToken);
            return;
        }

        if (parsedResult.EventType == "PAYMENT_FAILED")
        {
            await _eventBus.PublishAsync(new GatewayPaymentFailedIntegrationEvent(
                OrganizationId: request.TenantId,
                GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
                Metadata: parsedResult.Metadata ?? new Dictionary<string, string>()));
            await TrySaveChangesAsync(cancellationToken);
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
            Metadata: parsedResult.Metadata,
            GatewayCustomerId: parsedResult.GatewayCustomerId,
            GatewayTokenId: parsedResult.GatewayTokenId
        );

        await _eventBus.PublishAsync(integrationEvent);
        await TrySaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Business key for payment-level idempotency across dual gateway events
    /// (e.g. Stripe checkout.session.completed + payment_intent.succeeded).
    /// </summary>
    private static string? BuildBusinessKey(string eventType, string? gatewayTransactionId)
    {
        if (string.IsNullOrEmpty(gatewayTransactionId))
        {
            return null;
        }

        // Money events only (caller already filters to these)
        return eventType + ":" + gatewayTransactionId;
    }

    private async Task TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _logRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent delivery raced past the pre-checks; treat as successful duplicate (HTTP 200).
            return;
        }
    }

    /// <summary>
    /// Detects PostgreSQL unique_violation (SQLSTATE 23505) without hard-depending on Npgsql in Application.
    /// </summary>
    public static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? ex = exception; ex != null; ex = ex.InnerException)
        {
            var sqlState = ex.GetType().GetProperty("SqlState")?.GetValue(ex) as string;
            if (sqlState == "23505")
            {
                return true;
            }

            if (ex.Message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
