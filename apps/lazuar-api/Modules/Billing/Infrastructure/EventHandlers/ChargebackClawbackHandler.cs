using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

/// <summary>
/// Consumes gateway dispute events and claws back credits granted for a disputed utility-credit
/// top-up, and reverses the matching SYSTEM_CREDIT_TOPUP ledger entry.
///
/// Scope (A.6 / C.1 MVP): utility clawback only.
/// - Handles only metadata.type == "utility_credit_topup" (platform credit purchases).
/// - Does NOT suspend commerce subscriptions or reverse merchant GMV ledger entries.
/// </summary>
public class ChargebackClawbackHandler : IIntegrationEventHandler<GatewayDisputeCreatedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly BillingDbContext _dbContext;
    private readonly CreditCostOptions _creditOptions;
    private readonly ILogger<ChargebackClawbackHandler> _logger;

    public ChargebackClawbackHandler(
        IMediator mediator,
        BillingDbContext dbContext,
        IOptions<CreditCostOptions> creditOptions,
        ILogger<ChargebackClawbackHandler> logger)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _creditOptions = creditOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayDisputeCreatedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type))
            return;

        if (type == PlatformCheckoutTypes.PlatformSaasFee)
        {
            await MarkSaasPastDueAsync(@event);
            return;
        }

        // Utility-credit top-ups only — commerce chargebacks are intentionally out of scope for MVP.
        if (type != PlatformCheckoutTypes.UtilityCreditTopup)
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr) || !Guid.TryParse(tenantIdStr, out var tenantId))
            return;

        // Recompute the credits that were granted for the disputed amount (same package logic as PlatformTopUpEventHandler).
        var creditsToClawback = _creditOptions.Packages
            .Where(p => p.AmountMyr <= @event.AmountDisputed)
            .OrderByDescending(p => p.AmountMyr)
            .Select(p => (int?)p.Credits)
            .FirstOrDefault() ?? 0;

        if (creditsToClawback > 0)
        {
            await _mediator.Send(new ClawbackCreditsCommand(
                tenantId,
                creditsToClawback,
                $"Chargeback clawback: {@event.GatewayTransactionId}"));

            _logger.LogWarning(
                "Clawed back {Credits} credits from tenant {TenantId} following dispute {DisputeId}.",
                creditsToClawback, tenantId, @event.GatewayTransactionId);
        }

        await ReverseUtilityTopUpLedgerAsync(tenantId, @event);
    }

    private async Task MarkSaasPastDueAsync(GatewayDisputeCreatedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr)
            || !Guid.TryParse(tenantIdStr, out var tenantId))
            return;

        var subscription = await _dbContext.WorkspaceSaasSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == tenantId);

        if (subscription == null)
        {
            _logger.LogWarning(
                "Hub SaaS dispute {GatewayTxId} has no subscription for tenant {TenantId}; credits unchanged.",
                @event.GatewayTransactionId, tenantId);
            return;
        }

        subscription.MarkPastDue();
        await _dbContext.SaveChangesAsync();
        _logger.LogWarning(
            "Marked Hub SaaS subscription PAST_DUE for tenant {TenantId} after dispute {GatewayTxId}; credits unchanged.",
            tenantId, @event.GatewayTransactionId);
    }

    private async Task ReverseUtilityTopUpLedgerAsync(Guid tenantId, GatewayDisputeCreatedIntegrationEvent @event)
    {
        var referenceType = LedgerReferenceTypes.SystemCreditChargeback;
        var referenceId = @event.GatewayTransactionId;

        // Idempotent on ReferenceType + ReferenceId.
        var alreadyReversed = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId);

        if (alreadyReversed)
            return;

        var originalTopUp = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e =>
                e.OrganizationId == tenantId
                && e.ReferenceType == LedgerReferenceTypes.SystemCreditTopup
                && e.ReferenceId == @event.GatewayTransactionId);

        if (originalTopUp == null)
        {
            _logger.LogWarning(
                "No SYSTEM_CREDIT_TOPUP ledger entry found for disputed gateway tx {GatewayTxId}; skipping ledger reverse.",
                @event.GatewayTransactionId);
            return;
        }

        var reverseEntry = new LedgerEntry(
            tenantId,
            referenceType,
            referenceId,
            $"Chargeback reverse of utility top-up {@event.GatewayTransactionId}",
            "B2B");

        foreach (var line in originalTopUp.Lines)
        {
            reverseEntry.AddLine(
                line.AccountType,
                -line.Amount,
                line.Currency,
                -line.BaseCurrencyAmount,
                line.BaseCurrency,
                line.TaxTypeCode,
                line.MsicCode);
        }

        reverseEntry.ValidateBalanced();
        reverseEntry.MarkConsolidationNotRequired();
        _dbContext.LedgerEntries.Add(reverseEntry);
        await _dbContext.SaveChangesAsync();

        _logger.LogWarning(
            "Posted SYSTEM_CREDIT_CHARGEBACK ledger reverse for tenant {TenantId} gateway tx {GatewayTxId}.",
            tenantId, @event.GatewayTransactionId);
    }
}
