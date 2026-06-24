using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;
using Modules.Community.Contracts;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ManualPaymentRecordedHandler : IIntegrationEventHandler<CommunityManualPaymentRecordedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public ManualPaymentRecordedHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(CommunityManualPaymentRecordedIntegrationEvent @event)
    {
        var referenceType = "MANUAL_PAYMENT_RECORDED";
        
        // Utilize the newly generated deterministic SystemReference instead of composite ID hacking
        var referenceId = @event.SystemReference;

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Manual payment reconciled for Subscription: {@event.SubscriptionId}");

        entry.AddLine("ASSET_CASH", @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine("REVENUE_GROSS", -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
