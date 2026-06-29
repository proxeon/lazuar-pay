using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;
using Modules.Commerce.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ManualSubscriberEnrolledIntegrationEventHandler : IIntegrationEventHandler<ManualSubscriberEnrolledIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public ManualSubscriberEnrolledIntegrationEventHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(ManualSubscriberEnrolledIntegrationEvent @event)
    {
        var referenceType = "MANUAL_ENROLLMENT";
        var referenceId = @event.SubscriptionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Manual subscription logged for customer: {@event.ClientProfileId}");

        entry.AddLine("ASSET_CASH", @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine("REVENUE_GROSS", -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
