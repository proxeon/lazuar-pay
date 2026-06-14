using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class InvoiceIssuedHandler : IIntegrationEventHandler<InvoiceIssuedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public InvoiceIssuedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(InvoiceIssuedIntegrationEvent @event)
    {
        var referenceType = "INVOICE_ISSUED";
        var referenceId = @event.InvoiceNumber;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"B2B Invoice issued: {@event.InvoiceNumber}");

        entry.AddLine("ASSET_ACCOUNTS_RECEIVABLE", @event.Amount, @event.Currency, @event.Amount, @event.Currency);
        entry.AddLine("LIABILITY_DEFERRED_REVENUE", -@event.Amount, @event.Currency, -@event.Amount, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
