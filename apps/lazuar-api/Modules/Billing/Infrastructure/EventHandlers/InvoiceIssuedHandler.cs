using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
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
        var referenceType = LedgerReferenceTypes.InvoiceIssued;
        var referenceId = @event.InvoiceNumber;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"B2B Invoice issued: {@event.InvoiceNumber}");

        // Deferred revenue schedule creation is not wired yet (C.1 honesty gate).
        // RevenueRecognitionJob is unregistered until schedules are created from product periods.
        // Booking to LIABILITY_DEFERRED_REVENUE remains so AR is honest; recognition amortization is deferred.
        entry.AddLine(AccountTypes.AssetAccountsReceivable, @event.Amount, @event.Currency, @event.Amount, @event.Currency);
        entry.AddLine(AccountTypes.LiabilityDeferredRevenue, -@event.Amount, @event.Currency, -@event.Amount, @event.Currency);

        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
