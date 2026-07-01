using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts;
using Modules.Billing.Domain.Aggregates;
using Modules.One.Contracts;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Seeds a new tenant's wallet with a one-time starter credit grant when the BILLING
/// entitlement is granted (i.e. on signup). Idempotent: skips if a wallet already exists.
/// </summary>
public class StarterCreditSeederHandler : IIntegrationEventHandler<AppEntitlementGrantedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly ICreditCostService _creditCostService;

    public StarterCreditSeederHandler(BillingDbContext dbContext, ICreditCostService creditCostService)
    {
        _dbContext = dbContext;
        _creditCostService = creditCostService;
    }

    public async Task HandleAsync(AppEntitlementGrantedIntegrationEvent @event)
    {
        if (!string.Equals(@event.AppId, "BILLING", StringComparison.OrdinalIgnoreCase))
            return;

        var existing = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == @event.TenantId);

        if (existing != null) return; // idempotent

        var grant = _creditCostService.GetStarterGrant();
        if (grant <= 0) return;

        var wallet = new TenantCreditBalance(@event.TenantId);
        wallet.TopUp(grant, "Starter credits (free grant)");
        _dbContext.TenantCreditBalances.Add(wallet);
        await _dbContext.SaveChangesAsync();
    }
}
