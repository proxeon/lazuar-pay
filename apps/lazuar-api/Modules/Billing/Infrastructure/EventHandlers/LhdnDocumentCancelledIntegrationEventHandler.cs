using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Reverses financial ledger entries associated with a successfully canceled LHDN invoice.
/// Automatically creates zeroing contra-entries for perfect double-entry bookkeeping.
/// </summary>
public class LhdnDocumentCancelledIntegrationEventHandler : IIntegrationEventHandler<LhdnDocumentCancelledIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly ILedgerRepository _repository;

    public LhdnDocumentCancelledIntegrationEventHandler(BillingDbContext dbContext, ILedgerRepository repository)
    {
        _dbContext = dbContext;
        _repository = repository;
    }

    public async Task HandleAsync(LhdnDocumentCancelledIntegrationEvent @event)
    {
        if (await _repository.HasEntryBeenProcessedAsync(LedgerReferenceTypes.LhdnCancellation, @event.InternalReferenceId))
            return;

        var matches = await LedgerLhdnLookup.MatchingAsync(
            _dbContext.LedgerEntries.Include(e => e.Lines),
            @event.OrganizationId,
            @event.InternalReferenceId);

        var originalEntry = matches.FirstOrDefault();
        if (originalEntry == null)
            return;

        // Apply contra entries dynamically based on original invoice lines
        var cancelEntry = new LedgerEntry(
            @event.OrganizationId,
            LedgerReferenceTypes.LhdnCancellation,
            @event.InternalReferenceId,
            $"Reversal of cancelled LHDN invoice {@event.LhdnUuid} - Reason: {@event.Reason}",
            originalEntry.CustomerType);

        foreach (var line in originalEntry.Lines)
        {
            cancelEntry.AddLine(
                line.AccountType,
                -line.Amount,
                line.Currency,
                -line.BaseCurrencyAmount,
                line.BaseCurrency,
                line.TaxTypeCode,
                line.MsicCode);
        }

        cancelEntry.ValidateBalanced();
        _repository.Add(cancelEntry);

        // LHDN fields only — never overwrite CustomerDocumentNumber.
        originalEntry.UpdateLhdnStatus(@event.LhdnUuid, LhdnValidationStatuses.Cancelled);

        await _repository.SaveChangesAsync();
    }
}
