using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayRefundCompletedHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public GatewayRefundCompletedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var referenceType = "GATEWAY_REFUND";
        var referenceId = @event.PaymentRecordId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Refund processed for subscription {@event.SubscriptionId}");

        var cashOutflow = @event.RefundedAmount - @event.RefundedFee;

        entry.AddLine("ASSET_CASH", -cashOutflow, @event.Currency, -cashOutflow, @event.Currency);
        entry.AddLine("CONTRA_REVENUE_REFUNDS", @event.RefundedAmount, @event.Currency, @event.RefundedAmount, @event.Currency);
        
        if (@event.RefundedFee > 0)
        {
            entry.AddLine("EXPENSE_GATEWAY_FEE", -@event.RefundedFee, @event.Currency, -@event.RefundedFee, @event.Currency);
        }

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
