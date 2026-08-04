using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class GatewayRefundCompletedHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly BillingDbContext _dbContext;

    public GatewayRefundCompletedHandler(ILedgerRepository repository, BillingDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        if (@event.RefundedAmount <= 0)
            return;

        var referenceType = LedgerReferenceTypes.GatewayRefund;
        var referenceId = @event.PaymentRecordId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var taxRefund = await ResolveTaxRefundAmountAsync(@event);

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Refund processed for subscription {@event.SubscriptionId} (gateway tx {@event.GatewayTransactionId})");

        var cashOutflow = @event.RefundedAmount - @event.RefundedFee;
        var grossRefund = @event.RefundedAmount - taxRefund;

        // Mirror GatewayPaymentCompletedHandler: cash/fee/revenue/tax symmetry with opposite signs.
        entry.AddLine(AccountTypes.AssetCash, -cashOutflow, @event.Currency, -cashOutflow, @event.Currency);

        if (@event.RefundedFee > 0)
        {
            entry.AddLine(AccountTypes.ExpenseGatewayFee, -@event.RefundedFee, @event.Currency, -@event.RefundedFee, @event.Currency);
        }

        entry.AddLine(AccountTypes.ContraRevenueRefunds, grossRefund, @event.Currency, grossRefund, @event.Currency);

        if (taxRefund > 0)
        {
            // Original payment credits LIABILITY_TAX_PAYABLE (negative); reverse with a debit (positive).
            entry.AddLine(AccountTypes.LiabilityTaxPayable, taxRefund, @event.Currency, taxRefund, @event.Currency);
        }

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
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
}
