using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class CommissionAccruedHandler : IIntegrationEventHandler<CommissionAccruedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public CommissionAccruedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(CommissionAccruedIntegrationEvent @event)
    {
        var referenceType = LedgerReferenceTypes.CommissionAccrued;
        var referenceId = @event.CommissionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Affiliate commission accrued for Partner: {@event.AffiliateId}");

        entry.AddLine(AccountTypes.ExpenseCommission, @event.Amount, @event.Currency, @event.Amount, @event.Currency);
        entry.AddLine(AccountTypes.LiabilityAffiliatePayable, -@event.Amount, @event.Currency, -@event.Amount, @event.Currency);

        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
