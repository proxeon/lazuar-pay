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
        // 1. Get Tenant Config
        var config = await _configRepository.GetActiveByTenantIdAsync(request.TenantId, cancellationToken);
        if (config == null || string.IsNullOrEmpty(config.WebhookSecret))
        {
            throw new InvalidOperationException("Webhook secret not configured for this tenant.");
        }

        // 2. Validate & Parse via Adapter
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);
        var parsedResult = await adapter.ParseWebhookAsync(config.WebhookSecret, request.RawBody, request.Headers);

        if (!parsedResult.Verified)
        {
            throw new InvalidOperationException($"Webhook signature verification failed: {parsedResult.Error}");
        }

        // If it's not a completed payment event (e.g., checkout.session.expired), we just ack it and stop here.
        if (parsedResult.EventType != "PAYMENT_COMPLETED")
        {
            return; 
        }

        // 3. Idempotency Check
        var alreadyProcessed = await _logRepository.HasBeenProcessedAsync(parsedResult.EventId, config.GatewayType, cancellationToken);
        if (alreadyProcessed)
        {
            return; // Gracefully acknowledge duplicate webhooks sent by Stripe/Billplz
        }

        // 4. Lock it (Save Log)
        var log = new PaymentWebhookLog(parsedResult.EventId, config.GatewayType);
        _logRepository.Add(log);
        
        // 5. Publish Integration Event to the Outbox
        var integrationEvent = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: request.TenantId,
            GatewayTransactionId: parsedResult.GatewayTransactionId ?? parsedResult.EventId,
            AmountPaid: parsedResult.AmountPaid,
            Currency: parsedResult.Currency,
            Metadata: parsedResult.Metadata
        );

        await _eventBus.PublishAsync(integrationEvent);
        
        // 6. Flush the context to commit the log and outbox message transactionally!
        // If the DB fails here, neither the log nor the outbox event is saved.
        await _logRepository.SaveChangesAsync(cancellationToken);
    }
}
