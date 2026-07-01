using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;
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
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "utility_credit_topup")
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var targetTenantId))
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
