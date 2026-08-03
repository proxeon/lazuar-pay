using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Application.Commands;

public class RecordRefundCommandHandler : ICommandHandler<RecordRefundCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public RecordRefundCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(RecordRefundCommand request, CancellationToken ct)
    {
        var log = await _repository.GetTransactionLogByIdAsync(request.TransactionLogId, ct);
        if (log == null || log.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Transaction log not found.");
        }

        if (string.Equals(log.Status, "REFUNDED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Transaction is already refunded.");
        }

        if (string.IsNullOrWhiteSpace(log.ExternalReference))
        {
            throw new InvalidOperationException("Transaction has no gateway reference; cannot refund.");
        }

        var amount = request.Amount ?? log.Amount;
        if (amount <= 0)
        {
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        }

        if (amount > log.Amount)
        {
            throw new InvalidOperationException("Refund amount cannot exceed the original transaction amount.");
        }

        var currency = string.IsNullOrWhiteSpace(log.Currency) ? "MYR" : log.Currency;
        var gatewayName = string.IsNullOrWhiteSpace(request.GatewayName) ? "STRIPE" : request.GatewayName.Trim().ToUpperInvariant();

        // PaymentRecordId: commerce transaction log id (stable ops correlation / ledger refund idempotency key).
        // GatewayTransactionId: external gateway charge/PI id stored on the log at capture time.
        await _eventBus.PublishAsync(new GatewayRefundRequestedIntegrationEvent(
            OrganizationId: request.OrganizationId,
            SubscriptionId: request.SubscriptionId ?? Guid.Empty,
            PaymentRecordId: log.Id,
            GatewayTransactionId: log.ExternalReference,
            Amount: amount,
            Currency: currency,
            GatewayName: gatewayName,
            TaxAmount: request.TaxAmount
        ));
    }
}
