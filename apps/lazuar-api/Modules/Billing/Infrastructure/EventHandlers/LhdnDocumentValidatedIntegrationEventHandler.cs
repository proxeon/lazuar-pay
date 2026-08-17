using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class LhdnDocumentValidatedIntegrationEventHandler : IIntegrationEventHandler<LhdnDocumentValidatedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly IMediator _mediator;

    public LhdnDocumentValidatedIntegrationEventHandler(BillingDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task HandleAsync(LhdnDocumentValidatedIntegrationEvent @event)
    {
        var key = @event.InternalReferenceId;
        var entries = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == @event.OrganizationId
                && (e.ReferenceId == key
                    || e.CustomerDocumentNumber == key
                    || e.TaxInvoiceId == key
                    || e.LhdnDocumentUuid == key))
            .ToListAsync();

        if (entries.Count == 0)
            return;

        var consBatch = key.StartsWith("B2C-CONS-", StringComparison.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (consBatch && IsConsolidatedReceiptChild(entry, key))
                continue;

            entry.UpdateLhdnStatus(@event.LhdnUuid, @event.Status);
        }

        await _dbContext.SaveChangesAsync();

        if (@event.Status != "VALID")
            return;

        // Consolidated QR belongs on the B2C-CONS document, not every receipt.
        if (key.StartsWith("B2C-CONS-", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var entry in entries)
        {
            var docType = ResolveDocumentType(entry);
            await _mediator.Send(new GenerateAndStoreDocumentCommand(
                @event.OrganizationId,
                entry.Id,
                docType,
                @event.QrLink));
        }
    }

    private static bool IsConsolidatedReceiptChild(LedgerEntry entry, string consKey)
    {
        if (string.Equals(entry.ReferenceId, consKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.CustomerDocumentNumber, consKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return entry.CustomerType == "B2C"
               || DocumentSeries.IsReceiptNumber(entry.CustomerDocumentNumber);
    }

    internal static string ResolveDocumentType(LedgerEntry entry)
    {
        if (entry.ReferenceType.Contains("REFUND")
            || entry.ReferenceType == LedgerReferenceTypes.LhdnCancellation
            || DocumentSeries.IsCreditNoteNumber(entry.CustomerDocumentNumber))
        {
            return "Credit Note";
        }

        if (entry.CustomerType == "B2B" || DocumentSeries.IsInvoiceNumber(entry.CustomerDocumentNumber))
        {
            return "Tax Invoice";
        }

        return "Official Receipt";
    }
}
