using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
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
        // Platform utility credit top-ups are wallet + SYSTEM_CREDIT_TOPUP only
        // (PlatformTopUpEventHandler). Skip merchant GMV / revenue ledger path so the
        // same gateway txn is not dual-posted as creator sale revenue.
        if (@event.Metadata.TryGetValue("type", out var paymentType) && paymentType == "utility_credit_topup")
            return;

        var referenceType = LedgerReferenceTypes.GatewayPayment;
        var referenceId = @event.GatewayTransactionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var isB2b = @event.Metadata.TryGetValue("is_b2b_required", out var b2bFlag) && b2bFlag == "true";

        // The LedgerEntry constructor assigns Timestamp = DateTime.UtcNow automatically.
        // This ensures LHDN e-Invoices reflect the actual realization date of the recovered payment,
        // rather than the original overdue NextBillingDate of the subscription.
        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Gateway payment completed via {@event.Metadata.GetValueOrDefault("type", "UNKNOWN")}",
            isB2b ? "B2B" : "B2C");

        var baseCurrency = @event.BaseCurrency;
        var fxRate = @event.FxRate;
        var grossRevenue = @event.AmountPaid - @event.TaxAmount;

        entry.AddLine(AccountTypes.AssetCash, @event.NetAmount, @event.Currency, @event.NetAmount * fxRate, baseCurrency);

        if (@event.GatewayFee > 0)
        {
            entry.AddLine(AccountTypes.ExpenseGatewayFee, @event.GatewayFee, @event.Currency, @event.GatewayFee * fxRate, baseCurrency);
        }

        entry.AddLine(AccountTypes.RevenueGross, -grossRevenue, @event.Currency, -grossRevenue * fxRate, baseCurrency);

        if (@event.TaxAmount > 0)
        {
            entry.AddLine(AccountTypes.LiabilityTaxPayable, -@event.TaxAmount, @event.Currency, -@event.TaxAmount * fxRate, baseCurrency);
        }

        entry.ValidateBalanced();
        _repository.Add(entry);

        if (!isB2b)
        {
            var seqCommand = new GenerateNextSequenceNumberCommand(@event.OrganizationId, $"RCPT-{DateTime.UtcNow:yyyy}");
            var receiptNumber = await _mediator.Send(seqCommand);
            entry.AssignB2cReceipt(receiptNumber);
        }
        else
        {
            entry.MarkConsolidationNotRequired();
        }

        await _repository.SaveChangesAsync();

        if (!isB2b)
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Official Receipt"
            ));
        }
    }
}
