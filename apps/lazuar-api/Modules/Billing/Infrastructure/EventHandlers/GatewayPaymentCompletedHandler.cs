using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayPaymentCompletedHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly IMediator _mediator;

    public GatewayPaymentCompletedHandler(ILedgerRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var referenceType = "GATEWAY_PAYMENT";
        var referenceId = @event.GatewayTransactionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var isB2b = @event.Metadata.TryGetValue("is_b2b_required", out var b2bFlag) && b2bFlag == "true";

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Gateway payment completed via {@event.Metadata.GetValueOrDefault("type", "UNKNOWN")}",
            isB2b ? "B2B" : "B2C");

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

        if (!isB2b)
        {
            var seqCommand = new GenerateNextSequenceNumberCommand(@event.OrganizationId, $"RCPT-{DateTime.UtcNow:yyyy}");
            var receiptNumber = await _mediator.Send(seqCommand);
            
            entry.UpdateLhdnStatus(receiptNumber, "B2C_RECEIPT");

            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Official Receipt"
            ));
        }

        await _repository.SaveChangesAsync();
    }
}
