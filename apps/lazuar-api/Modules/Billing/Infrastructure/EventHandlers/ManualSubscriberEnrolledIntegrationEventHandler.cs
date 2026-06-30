using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Commerce.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ManualSubscriberEnrolledIntegrationEventHandler : IIntegrationEventHandler<ManualSubscriberEnrolledIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly IMediator _mediator;

    public ManualSubscriberEnrolledIntegrationEventHandler(ILedgerRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task HandleAsync(ManualSubscriberEnrolledIntegrationEvent @event)
    {
        var referenceType = "MANUAL_ENROLLMENT";
        var referenceId = @event.SubscriptionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        // Note: For Manual Enrollment currently all are treated as B2C unless a specific flag dictates otherwise.
        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"Manual subscription logged for customer: {@event.ClientProfileId}",
            "B2C");

        entry.AddLine("ASSET_CASH", @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine("REVENUE_GROSS", -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);

        var seqCommand = new GenerateNextSequenceNumberCommand(@event.OrganizationId, $"RCPT-{DateTime.UtcNow:yyyy}");
        var receiptNumber = await _mediator.Send(seqCommand);
            
        entry.UpdateLhdnStatus(receiptNumber, "B2C_RECEIPT");

        await _mediator.Send(new GenerateAndStoreDocumentCommand(
            @event.OrganizationId,
            entry.Id,
            "Official Receipt"
        ));

        await _repository.SaveChangesAsync();
    }
}
