using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;

namespace Modules.Billing.Infrastructure.Commands;

public class ClawbackCreditsCommandHandler : ICommandHandler<ClawbackCreditsCommand>
{
    private readonly BillingDbContext _dbContext;

    public ClawbackCreditsCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(ClawbackCreditsCommand request, CancellationToken ct)
    {
        var wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId, ct);

        if (wallet == null) return;

        var alreadyClawed = await _dbContext.CreditLedgers
            .IgnoreQueryFilters()
            .AnyAsync(l => l.TenantCreditBalanceId == wallet.Id && l.Reference == request.Reference, ct);
        if (alreadyClawed)
        {
            return;
        }

        wallet.Clawback(request.Amount, request.Reference);
        await _dbContext.SaveChangesAsync(ct);
    }
}
