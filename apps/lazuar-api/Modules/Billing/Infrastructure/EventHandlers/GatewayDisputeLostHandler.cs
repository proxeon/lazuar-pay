using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Lost GMV chargeback: reverse the original sale when Stripe did not already refund it.
/// Utility / Hub disputes stay on ChargebackClawbackHandler.
/// </summary>
public class GatewayDisputeLostHandler : IIntegrationEventHandler<GatewayDisputeClosedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly ILedgerRepository _repository;

    public GatewayDisputeLostHandler(BillingDbContext dbContext, ILedgerRepository repository)
    {
        _dbContext = dbContext;
        _repository = repository;
    }

    public async Task HandleAsync(GatewayDisputeClosedIntegrationEvent @event)
    {
        if (!string.Equals(@event.Outcome, "lost", StringComparison.OrdinalIgnoreCase))
            return;

        if (@event.Metadata is not null
            && @event.Metadata.TryGetValue("type", out var type)
            && PlatformCheckoutTypes.IsPlatformCollected(type))
            return;

        if (string.IsNullOrWhiteSpace(@event.GatewayTransactionId))
            return;

        if (await _repository.HasEntryBeenProcessedAsync(
                @event.OrganizationId, LedgerReferenceTypes.GatewayDispute, @event.GatewayTransactionId))
            return;

        var original = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e =>
                e.OrganizationId == @event.OrganizationId
                && e.ReferenceType == LedgerReferenceTypes.GatewayPayment
                && e.ReferenceId == @event.GatewayTransactionId);

        if (original == null)
            return;

        var originalPaid = Math.Abs(original.Lines
            .Where(l => l.AccountType is AccountTypes.RevenueGross or AccountTypes.LiabilityTaxPayable)
            .Sum(l => l.Amount));

        var refunds = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .Where(e =>
                e.OrganizationId == @event.OrganizationId
                && e.ReferenceType == LedgerReferenceTypes.GatewayRefund
                && e.Description != null
                && e.Description.Contains("gateway tx " + @event.GatewayTransactionId))
            .ToListAsync();
        var alreadyRefunded = refunds.SelectMany(e => e.Lines)
            .Where(l => l.AccountType == AccountTypes.ContraRevenueRefunds
                        || l.AccountType == AccountTypes.LiabilityTaxPayable)
            .Sum(l => l.Amount);

        if (alreadyRefunded >= originalPaid && originalPaid > 0)
            return;

        var reverse = new LedgerEntry(
            @event.OrganizationId,
            LedgerReferenceTypes.GatewayDispute,
            @event.GatewayTransactionId,
            $"Lost chargeback for gateway tx {@event.GatewayTransactionId}",
            original.CustomerType);

        foreach (var line in original.Lines)
        {
            reverse.AddLine(
                line.AccountType,
                -line.Amount,
                line.Currency,
                -line.BaseCurrencyAmount,
                line.BaseCurrency,
                line.TaxTypeCode,
                line.MsicCode);
        }

        reverse.ValidateBalanced();
        reverse.MarkConsolidationNotRequired();
        _repository.Add(reverse);
        await _repository.SaveChangesAsync();
    }
}
