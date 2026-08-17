using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayRefundCompletedHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly BillingDbContext _dbContext;
    private readonly IMediator _mediator;

    public GatewayRefundCompletedHandler(
        ILedgerRepository repository,
        BillingDbContext dbContext,
        IMediator mediator)
    {
        _repository = repository;
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        if (@event.RefundedAmount <= 0)
            return;

        var referenceType = LedgerReferenceTypes.GatewayRefund;
        // Per refund attempt (event id), not per capture. A second slice must post a new row.
        var referenceId = @event.PaymentRecordId.ToString("N") + ":" + @event.Id.ToString("N");

        if (await _repository.HasEntryBeenProcessedAsync(@event.OrganizationId, referenceType, referenceId))
            return;

        var taxRefund = await ResolveTaxRefundAmountAsync(@event);
        var (fxRate, baseCurrency) = await ResolveFxAsync(@event);

        async Task PersistAsync(CancellationToken ct)
        {
            var entry = new LedgerEntry(
                @event.OrganizationId,
                referenceType,
                referenceId,
                $"Refund processed for subscription {@event.SubscriptionId} (gateway tx {@event.GatewayTransactionId})");

            var cashOutflow = @event.RefundedAmount - @event.RefundedFee;
            var grossRefund = @event.RefundedAmount - taxRefund;

            entry.AddLine(AccountTypes.AssetCash, -cashOutflow, @event.Currency, -cashOutflow * fxRate, baseCurrency);

            if (@event.RefundedFee > 0)
            {
                entry.AddLine(AccountTypes.ExpenseGatewayFee, -@event.RefundedFee, @event.Currency, -@event.RefundedFee * fxRate, baseCurrency);
            }

            entry.AddLine(AccountTypes.ContraRevenueRefunds, grossRefund, @event.Currency, grossRefund * fxRate, baseCurrency);

            if (taxRefund > 0)
            {
                entry.AddLine(AccountTypes.LiabilityTaxPayable, taxRefund, @event.Currency, taxRefund * fxRate, baseCurrency);
            }

            entry.ValidateBalanced();
            entry.MarkConsolidationNotRequired();

            var creditNoteNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.CreditNotePrefix()), ct);
            if (!string.IsNullOrWhiteSpace(creditNoteNumber))
                entry.AssignCustomerDocumentNumber(creditNoteNumber);

            _repository.Add(entry);
            await _repository.SaveChangesAsync(ct);
        }

        if (_repository is IBillingTransactional transactional)
        {
            await transactional.ExecuteInTransactionAsync(PersistAsync);
        }
        else
        {
            await PersistAsync(default);
        }
    }

    /// <summary>
    /// Prefer explicit TaxAmount on the event; otherwise proportionally reverse tax from the original
    /// GATEWAY_PAYMENT ledger entry matched by gateway transaction id.
    /// </summary>
    private async Task<decimal> ResolveTaxRefundAmountAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        if (@event.TaxAmount > 0)
            return @event.TaxAmount;

        if (string.IsNullOrWhiteSpace(@event.GatewayTransactionId))
            return 0m;

        var originalEntry = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e =>
                e.OrganizationId == @event.OrganizationId
                && e.ReferenceType == LedgerReferenceTypes.GatewayPayment
                && e.ReferenceId == @event.GatewayTransactionId);

        if (originalEntry == null)
            return 0m;

        var originalTax = Math.Abs(originalEntry.Lines
            .Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable)
            .Sum(l => l.Amount));

        if (originalTax <= 0)
            return 0m;

        var originalGross = Math.Abs(originalEntry.Lines
            .Where(l => l.AccountType == AccountTypes.RevenueGross)
            .Sum(l => l.Amount));
        var originalPaid = originalGross + originalTax;

        if (originalPaid <= 0)
            return 0m;

        // Full refund: reverse full tax. Partial: scale tax by refund ratio.
        if (@event.RefundedAmount >= originalPaid)
            return originalTax;

        return Math.Round(@event.RefundedAmount / originalPaid * originalTax, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Prefer event FX; otherwise copy rate and base currency from the original sale so a
    /// USD capture booked into MYR is not reversed as if USD were MYR.
    /// </summary>
    private async Task<(decimal FxRate, string BaseCurrency)> ResolveFxAsync(
        GatewayRefundCompletedIntegrationEvent @event)
    {
        if (@event.FxRate > 0 && !string.IsNullOrWhiteSpace(@event.BaseCurrency))
            return (@event.FxRate, @event.BaseCurrency);

        if (string.IsNullOrWhiteSpace(@event.GatewayTransactionId))
            return (1m, @event.Currency);

        var originalEntry = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e =>
                e.OrganizationId == @event.OrganizationId
                && e.ReferenceType == LedgerReferenceTypes.GatewayPayment
                && e.ReferenceId == @event.GatewayTransactionId);

        var sample = originalEntry?.Lines.FirstOrDefault(l => l.Amount != 0);
        if (sample is null)
            return (1m, @event.Currency);

        var rate = sample.Amount == 0 ? 1m : sample.BaseCurrencyAmount / sample.Amount;
        var baseCurrency = string.IsNullOrWhiteSpace(sample.BaseCurrency)
            ? @event.Currency
            : sample.BaseCurrency;
        return (rate, baseCurrency);
    }
}
