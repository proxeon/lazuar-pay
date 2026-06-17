using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Deducts an API credit from the tenant's wallet when a live document is successfully submitted.
/// Sandbox submissions (IsTestMode = true) are ignored to allow free testing.
/// </summary>
public class LhdnDocumentSubmittedIntegrationEventHandler : IIntegrationEventHandler<LhdnDocumentSubmittedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;

    public LhdnDocumentSubmittedIntegrationEventHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(LhdnDocumentSubmittedIntegrationEvent @event)
    {
        if (@event.IsTestMode) return;

        var wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == @event.OrganizationId);

        if (wallet != null)
        {
            wallet.Deduct(1, $"LHDN Submission: {@event.InternalReferenceId}");
            await _dbContext.SaveChangesAsync();
        }
    }
}
