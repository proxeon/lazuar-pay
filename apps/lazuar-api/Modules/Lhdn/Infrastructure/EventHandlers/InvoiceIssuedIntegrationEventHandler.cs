using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.EventHandlers;

/// <summary>
/// InvoiceIssued has no honest buyer identity. MyInvois submit is
/// <see cref="B2bTaxInvoiceRequestedIntegrationEventHandler"/>. This handler
/// must never file stub TIN C1234567890.
/// </summary>
public class InvoiceIssuedIntegrationEventHandler : IIntegrationEventHandler<InvoiceIssuedIntegrationEvent>
{
    private readonly ILogger<InvoiceIssuedIntegrationEventHandler> _logger;

    public InvoiceIssuedIntegrationEventHandler(ILogger<InvoiceIssuedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(InvoiceIssuedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Ignoring InvoiceIssued {Invoice} — MyInvois submit uses B2bTaxInvoiceRequested only.",
            @event.InvoiceNumber);
        return Task.CompletedTask;
    }
}
