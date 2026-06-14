using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayPaymentCompletedHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public GatewayPaymentCompletedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var referenceType = "GATEWAY_PAYMENT";
        var referenceId = @event.GatewayTransactionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Gateway payment completed via {@event.Metadata.GetValueOrDefault("type", "UNKNOWN")}");

        var baseCurrency = @event.BaseCurrency;
        var fxRate = @event.FxRate;
        var grossRevenue = @event.AmountPaid - @event.TaxAmount;

        entry.AddLine("ASSET_CASH", @event.NetAmount, @event.Currency, @event.NetAmount * fxRate, baseCurrency);
        
        if (@event.GatewayFee > 0)
        {
            entry.AddLine("EXPENSE_GATEWAY_FEE", @event.GatewayFee, @event.Currency, @event.GatewayFee * fxRate, baseCurrency);
        }

        entry.AddLine("REVENUE_GROSS", -grossRevenue, @event.Currency, -grossRevenue * fxRate, baseCurrency);

        if (@event.TaxAmount > 0)
        {
            entry.AddLine("LIABILITY_TAX_PAYABLE", -@event.TaxAmount, @event.Currency, -@event.TaxAmount * fxRate, baseCurrency);
        }

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
