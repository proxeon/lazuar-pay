using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Consumes gateway dispute events and claws back credits granted for a disputed utility-credit
/// top-up. Recovers up to the tenant's remaining balance (spent credits are a loss). Idempotent
/// via the webhook log (the Payments module deduplicates by Stripe event id before publishing).
///
/// Scope (A.6 / MVP): utility clawback only.
/// - Handles only metadata.type == "utility_credit_topup" (platform credit purchases).
/// - Does NOT suspend commerce subscriptions, reverse merchant GMV ledger entries, or reverse tax
///   for disputed customer payments. Commerce dispute suspension / full chargeback ledger is later work.
/// </summary>
public class ChargebackClawbackHandler : IIntegrationEventHandler<GatewayDisputeCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly CreditCostOptions _creditOptions;
    private readonly ILogger<ChargebackClawbackHandler> _logger;

    public ChargebackClawbackHandler(
        IMediator mediator,
        IOptions<CreditCostOptions> creditOptions,
        ILogger<ChargebackClawbackHandler> logger)
    {
        _mediator = mediator;
        _creditOptions = creditOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayDisputeCreatedIntegrationEvent @event)
    {
        // Utility-credit top-ups only — commerce chargebacks are intentionally out of scope for MVP.
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "utility_credit_topup")
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var tenantId))
            return;

        // Recompute the credits that were granted for the disputed amount (same package logic as PlatformTopUpEventHandler).
        var creditsToClawback = _creditOptions.Packages
            .Where(p => p.AmountMyr <= @event.AmountDisputed)
            .OrderByDescending(p => p.AmountMyr)
            .Select(p => (int?)p.Credits)
            .FirstOrDefault() ?? 0;

        if (creditsToClawback <= 0)
            return;

        await _mediator.Send(new ClawbackCreditsCommand(
            tenantId,
            creditsToClawback,
            $"Chargeback clawback: {@event.GatewayTransactionId}"));

        _logger.LogWarning("Clawed back {Credits} credits from tenant {TenantId} following dispute {DisputeId}.",
            creditsToClawback, tenantId, @event.GatewayTransactionId);
    }
}
