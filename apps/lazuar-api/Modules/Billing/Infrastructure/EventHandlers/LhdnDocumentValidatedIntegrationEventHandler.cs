using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
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
        var ledgerEntry = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == @event.OrganizationId && e.ReferenceId == @event.InternalReferenceId);

        if (ledgerEntry != null)
        {
            ledgerEntry.UpdateLhdnStatus(@event.LhdnUuid, @event.Status);
            await _dbContext.SaveChangesAsync();

            if (@event.Status == "VALID")
            {
                var docType = ledgerEntry.ReferenceType.Contains("REFUND") ? "Credit Note" : "Tax Invoice";

                await _mediator.Send(new GenerateAndStoreDocumentCommand(
                    @event.OrganizationId,
                    ledgerEntry.Id,
                    docType,
                    @event.QrLink
                ));
            }
        }
    }
}
