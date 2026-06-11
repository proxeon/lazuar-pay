using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Events;
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
        var referenceType = "COMMISSION_ACCRUED";
        var referenceId = @event.CommissionId;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Affiliate commission accrued for Partner: {@event.AffiliateId}");

        entry.AddLine("EXPENSE_COMMISSION", @event.Amount, @event.Currency, @event.Amount, @event.Currency);
        entry.AddLine("LIABILITY_AFFILIATE_PAYABLE", -@event.Amount, @event.Currency, -@event.Amount, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
