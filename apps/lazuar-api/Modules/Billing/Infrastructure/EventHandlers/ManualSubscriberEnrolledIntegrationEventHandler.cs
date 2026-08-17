using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Application;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Commerce.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ManualSubscriberEnrolledIntegrationEventHandler : IIntegrationEventHandler<ManualSubscriberEnrolledIntegrationEvent>
{
    private readonly ILedgerRepository _repository;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    public ManualSubscriberEnrolledIntegrationEventHandler(
        ILedgerRepository repository,
        IMediator mediator,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _mediator = mediator;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(ManualSubscriberEnrolledIntegrationEvent @event)
    {
        var referenceType = LedgerReferenceTypes.ManualEnrollment;
        // Per-payment key (tx log id). Empty Guid is an in-flight outbox event from before LP-065.
        var referenceId = @event.TransactionLogId != Guid.Empty
            ? @event.TransactionLogId.ToString()
            : @event.SubscriptionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(@event.OrganizationId, referenceType, referenceId))
            return;

        var isB2b = @event.IsB2bRequired;
        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"{@event.PaymentMethod} payment logged for customer: {@event.ClientProfileId}",
            isB2b ? "B2B" : "B2C");

        entry.AddLine(AccountTypes.AssetCash, @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine(AccountTypes.RevenueGross, -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        entry.ValidateBalanced();
        _repository.Add(entry);

        if (isB2b)
        {
            var invoiceNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.InvoicePrefix()));
            entry.AssignB2bInvoice(invoiceNumber);
        }
        else
        {
            var receiptNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.ReceiptPrefix()));
            entry.AssignB2cReceipt(receiptNumber);
        }

        await _repository.SaveChangesAsync();

        var correlation = @event.SubscriptionId.ToString();
        if (isB2b)
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Invoice",
                CorrelationId: correlation));

            await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
                @event.OrganizationId,
                entry.Id,
                entry.CustomerDocumentNumber!,
                referenceId,
                @event.AmountPaid,
                0m,
                @event.Currency,
                correlation));
        }
        else
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                "Official Receipt",
                CorrelationId: correlation));
        }
    }
}
