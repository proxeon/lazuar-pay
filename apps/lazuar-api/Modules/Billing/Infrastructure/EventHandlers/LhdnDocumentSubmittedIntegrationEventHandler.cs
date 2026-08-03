using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Observability hook for successful MyInvois submissions.
/// Utility credits are charged once on accept in <c>SubmitTaxDocumentCommand</c>
/// via <c>ICreditCostService</c> / <c>DeductTenantCreditCommand</c> (idempotent key <c>lhdn:…</c>).
/// This handler must not deduct wallet credits — a prior hard-coded deduct of 1 caused double charging.
/// </summary>
public class LhdnDocumentSubmittedIntegrationEventHandler : IIntegrationEventHandler<LhdnDocumentSubmittedIntegrationEvent>
{
    private readonly ILogger<LhdnDocumentSubmittedIntegrationEventHandler> _logger;

    public LhdnDocumentSubmittedIntegrationEventHandler(ILogger<LhdnDocumentSubmittedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(LhdnDocumentSubmittedIntegrationEvent @event)
    {
        _logger.LogDebug(
            "LHDN document submitted for org {OrganizationId} ref {InternalReferenceId} (test={IsTestMode}); credit charge is owned by SubmitTaxDocumentCommand.",
            @event.OrganizationId,
            @event.InternalReferenceId,
            @event.IsTestMode);

        return Task.CompletedTask;
    }
}
