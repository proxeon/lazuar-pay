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
/// Utility top-up claw + Hub SaaS fee reverse. Does not cancel Commerce seats.
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

        var alreadyReversed = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e =>
                e.OrganizationId == tenantId
                && e.ReferenceType == LedgerReferenceTypes.SystemCreditChargeback
                && e.ReferenceId == @event.GatewayTransactionId);
        if (alreadyReversed)
        {
            return;
        }

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
                "No SYSTEM_CREDIT_TOPUP ledger entry found for disputed gateway tx {GatewayTxId}; skipping claw and ledger reverse.",
                @event.GatewayTransactionId);
            return;
        }

        var creditsToClawback = CreditsGrantedOnTopUp(originalTopUp);
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

        await ReverseUtilityTopUpLedgerAsync(tenantId, @event, originalTopUp);
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
        await ReverseSaasFeeLedgerAsync(tenantId, @event);
        await _dbContext.SaveChangesAsync();
        _logger.LogWarning(
            "Marked Hub SaaS subscription PAST_DUE for tenant {TenantId} after dispute {GatewayTxId}; credits unchanged.",
            tenantId, @event.GatewayTransactionId);
    }

    private async Task ReverseSaasFeeLedgerAsync(
        Guid tenantId,
        GatewayDisputeCreatedIntegrationEvent @event)
    {
        var referenceType = LedgerReferenceTypes.SystemSaasFeeReverse;
        var referenceId = @event.GatewayTransactionId;

        var alreadyReversed = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e =>
                e.OrganizationId == tenantId
                && e.ReferenceType == referenceType
                && e.ReferenceId == referenceId);
        if (alreadyReversed)
            return;

        var original = await _dbContext.LedgerEntries
            .Include(e => e.Lines)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e =>
                e.OrganizationId == tenantId
                && e.ReferenceType == LedgerReferenceTypes.SystemSaasFee
                && e.ReferenceId == referenceId);
        if (original == null)
        {
            _logger.LogWarning(
                "No SYSTEM_SAAS_FEE ledger entry for disputed Hub tx {GatewayTxId}; PAST_DUE only.",
                referenceId);
            return;
        }

        var reverseEntry = new LedgerEntry(
            tenantId,
            referenceType,
            referenceId,
            $"Dispute reverse of Hub SaaS fee {@event.GatewayTransactionId}",
            "B2B");

        foreach (var line in original.Lines)
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
    }

    private int CreditsGrantedOnTopUp(LedgerEntry originalTopUp)
    {
        const string prefix = "Purchased ";
        const string suffix = " Utility Credits";
        var description = originalTopUp.Description ?? "";
        var start = description.IndexOf(prefix, StringComparison.Ordinal);
        var end = description.IndexOf(suffix, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            var raw = description[(start + prefix.Length)..end].Trim();
            if (int.TryParse(raw, out var parsed) && parsed > 0)
                return parsed;
        }

        var paid = Math.Abs(originalTopUp.Lines
            .Where(l => l.AccountType == AccountTypes.ExpenseSoftwareSubscription)
            .Sum(l => l.Amount));
        return _creditOptions.Packages
            .Where(p => p.AmountMyr <= paid)
            .OrderByDescending(p => p.AmountMyr)
            .Select(p => (int?)p.Credits)
            .FirstOrDefault() ?? 0;
    }

    private async Task ReverseUtilityTopUpLedgerAsync(
        Guid tenantId,
        GatewayDisputeCreatedIntegrationEvent @event,
        LedgerEntry originalTopUp)
    {
        var referenceType = LedgerReferenceTypes.SystemCreditChargeback;
        var referenceId = @event.GatewayTransactionId;

        var alreadyReversed = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e =>
                e.OrganizationId == tenantId
                && e.ReferenceType == referenceType
                && e.ReferenceId == referenceId);

        if (alreadyReversed)
            return;

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
