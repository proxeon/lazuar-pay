using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Receives official LHDN UUIDs and attaches them to the internal financial ledger entry.
/// </summary>
public class LhdnDocumentValidatedIntegrationEventHandler : IIntegrationEventHandler<LhdnDocumentValidatedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;

    public LhdnDocumentValidatedIntegrationEventHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(LhdnDocumentValidatedIntegrationEvent @event)
    {
        var ledgerEntry = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == @event.OrganizationId && e.ReferenceId == @event.InternalReferenceId);

        if (ledgerEntry != null)
        {
            ledgerEntry.UpdateLhdnStatus(@event.LhdnUuid, @event.Status);
            await _dbContext.SaveChangesAsync();
        }
    }
}
