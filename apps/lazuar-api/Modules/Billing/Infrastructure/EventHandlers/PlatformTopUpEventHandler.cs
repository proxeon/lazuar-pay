using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class PlatformTopUpEventHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly CreditCostOptions _creditOptions;

    public PlatformTopUpEventHandler(BillingDbContext dbContext, IOptions<CreditCostOptions> creditOptions)
    {
        _dbContext = dbContext;
        _creditOptions = creditOptions.Value;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type) || type != PlatformCheckoutTypes.UtilityCreditTopup)
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var targetTenantId))
            return;

        // No stable transaction id → cannot safely credit (risk of double-grant on redelivery).
        if (string.IsNullOrWhiteSpace(@event.GatewayTransactionId))
            return;

        // Transaction-level idempotency: never double-credit the same gateway payment.
        var alreadyToppedUp = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemCreditTopup
                           && e.ReferenceId == @event.GatewayTransactionId);
        if (alreadyToppedUp)
            return;

        // Grant the highest package whose price threshold the tenant has met.
        var credits = _creditOptions.Packages
            .Where(p => p.AmountMyr <= @event.AmountPaid)
            .OrderByDescending(p => p.AmountMyr)
            .Select(p => (int?)p.Credits)
            .FirstOrDefault() ?? 0;

        if (credits > 0)
        {
            var wallet = await _dbContext.TenantCreditBalances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.OrganizationId == targetTenantId);

            if (wallet == null)
            {
                wallet = new TenantCreditBalance(targetTenantId);
                _dbContext.TenantCreditBalances.Add(wallet);
            }

            var reference = $"Platform Top-Up: {@event.GatewayTransactionId}";
            wallet.TopUp(credits, reference);

            var ledgerEntry = new LedgerEntry(
                targetTenantId,
                LedgerReferenceTypes.SystemCreditTopup,
                @event.GatewayTransactionId,
                $"Purchased {credits} Utility Credits via Lazuar Platform",
                "B2B");

            ledgerEntry.AddLine(AccountTypes.ExpenseSoftwareSubscription, @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
            ledgerEntry.AddLine(AccountTypes.AssetCash, -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

            ledgerEntry.ValidateBalanced();
            ledgerEntry.MarkConsolidationNotRequired();
            _dbContext.LedgerEntries.Add(ledgerEntry);

            await _dbContext.SaveChangesAsync();
        }
    }
}
