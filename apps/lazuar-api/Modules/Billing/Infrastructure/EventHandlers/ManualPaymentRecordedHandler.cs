using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ManualPaymentRecordedHandler : IIntegrationEventHandler<ManualPaymentRecordedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public ManualPaymentRecordedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(ManualPaymentRecordedIntegrationEvent @event)
    {
        var referenceType = "MANUAL_PAYMENT_RECORDED";
        var referenceId = $"{@event.InvoiceNumber}_{@event.ReferenceNumber ?? @event.Id.ToString()}";

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Manual bank transfer reconciled for Invoice: {@event.InvoiceNumber}");

        entry.AddLine("ASSET_CASH", @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine("ASSET_ACCOUNTS_RECEIVABLE", -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
