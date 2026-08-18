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
        {
            var existing = await _repository.GetByReferenceAsync(
                @event.OrganizationId, referenceType, referenceId);
            if (existing is not null)
                await GenerateDocumentAsync(@event, existing, publishB2b: false);
            return;
        }

        var isB2b = @event.IsB2bRequired;
        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"{@event.PaymentMethod} payment logged for customer: {@event.ClientProfileId}",
            isB2b ? "B2B" : "B2C");

        var tax = Math.Max(0m, @event.TaxAmount);
        if (tax > @event.AmountPaid)
        {
            tax = @event.AmountPaid;
        }

        var cash = @event.AmountPaid;
        var revenue = cash - tax;
        entry.AddLine(AccountTypes.AssetCash, cash, @event.Currency, cash, @event.Currency);
        if (tax > 0)
        {
            entry.AddLine(AccountTypes.LiabilityTaxPayable, -tax, @event.Currency, -tax, @event.Currency);
        }

        entry.AddLine(AccountTypes.RevenueGross, -revenue, @event.Currency, -revenue, @event.Currency);

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

        await GenerateDocumentAsync(@event, entry, publishB2b: true);
    }

    private async Task GenerateDocumentAsync(
        ManualSubscriberEnrolledIntegrationEvent @event, LedgerEntry booked, bool publishB2b)
    {
        var isB2b = booked.CustomerType == "B2B";
        var correlation = @event.SubscriptionId.ToString();
        if (isB2b)
        {
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                booked.Id,
                "Invoice",
                CorrelationId: correlation));

            if (publishB2b)
            {
                await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
                    @event.OrganizationId,
                    booked.Id,
                    booked.CustomerDocumentNumber ?? "",
                    booked.ReferenceId,
                    @event.AmountPaid,
                    @event.TaxAmount,
                    @event.Currency,
                    correlation));
            }

            return;
        }

        await _mediator.Send(new GenerateAndStoreDocumentCommand(
            @event.OrganizationId,
            booked.Id,
            "Official Receipt",
            CorrelationId: correlation));
    }
}
