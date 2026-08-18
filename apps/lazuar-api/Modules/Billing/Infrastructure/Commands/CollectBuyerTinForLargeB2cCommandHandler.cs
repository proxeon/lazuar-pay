using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Commerce.Contracts;

namespace Modules.Billing.Infrastructure.Commands;

public class CollectBuyerTinForLargeB2cCommandHandler : ICommandHandler<CollectBuyerTinForLargeB2cCommand>
{
    private readonly BillingDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    private readonly ICommerceBuyerIdentity _buyerIdentity;

    public CollectBuyerTinForLargeB2cCommandHandler(
        BillingDbContext dbContext,
        IMediator mediator,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        ICommerceBuyerIdentity buyerIdentity)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _eventBus = eventBus;
        _buyerIdentity = buyerIdentity;
    }

    public async Task Handle(CollectBuyerTinForLargeB2cCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Tin)
            || string.IsNullOrWhiteSpace(request.IdType)
            || string.IsNullOrWhiteSpace(request.IdValue)
            || string.IsNullOrWhiteSpace(request.CompanyName)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("TIN, ID pair, company name, and email are required.");
        }

        var entry = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(
                e => e.Id == request.LedgerEntryId && e.OrganizationId == request.OrganizationId,
                ct);

        if (entry == null)
            throw new InvalidOperationException("Ledger entry not found.");

        await _buyerIdentity.AttachTinAsync(
            request.OrganizationId,
            string.IsNullOrWhiteSpace(request.FullName) ? request.CompanyName : request.FullName,
            request.Email.Trim(),
            request.Tin.Trim(),
            request.IdType.Trim(),
            request.IdValue.Trim(),
            request.CompanyName.Trim(),
            ct);

        entry.ConvertNeedsBuyerTinToB2b();

        if (string.IsNullOrWhiteSpace(entry.CustomerDocumentNumber))
        {
            var invoiceNumber = await _mediator.Send(
                new GenerateNextSequenceNumberCommand(request.OrganizationId, DocumentSeries.InvoicePrefix()), ct);
            entry.AssignB2bInvoice(invoiceNumber);
        }

        await _dbContext.SaveChangesAsync(ct);

        var tax = Math.Abs(entry.Lines
            .Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable)
            .Sum(l => l.Amount));
        var gross = Math.Abs(entry.Lines
            .Where(l => l.AccountType == AccountTypes.RevenueGross)
            .Sum(l => l.Amount));
        var currency = entry.Lines.FirstOrDefault()?.Currency ?? "MYR";

        await _eventBus.PublishAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            request.OrganizationId,
            entry.Id,
            entry.CustomerDocumentNumber ?? "",
            entry.ReferenceId,
            gross,
            tax,
            currency,
            entry.Id.ToString(),
            entry.Description));
    }
}
