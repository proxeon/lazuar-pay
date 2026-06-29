using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class PlatformTopUpEventHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;

    public PlatformTopUpEventHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "utility_credit_topup")
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var targetTenantId))
            return;

        var credits = 0;
        if (@event.AmountPaid >= 50) credits = 500;
        if (@event.AmountPaid >= 100) credits = 1100;
        if (@event.AmountPaid >= 200) credits = 2500;

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
                "SYSTEM_CREDIT_TOPUP",
                @event.GatewayTransactionId,
                $"Purchased {credits} Utility Credits via Lazuar Platform",
                "B2B");

            ledgerEntry.AddLine("EXPENSE_SOFTWARE_SUBSCRIPTION", @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
            ledgerEntry.AddLine("ASSET_CASH", -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

            ledgerEntry.ValidateBalanced();
            _dbContext.LedgerEntries.Add(ledgerEntry);

            await _dbContext.SaveChangesAsync();
        }
    }
}
