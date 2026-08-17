using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Payments.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Dunning;

/// <summary>
/// Assigns a matching campaign (if missing), catch-up-dispatches due past-due steps,
/// then applies campaign FinalAction after the later of grace and the last past-due step.
/// Shared by the hourly engine, payment-failed handler, and billing no-token path.
/// </summary>
public sealed class PastDueDunningProcessor
{
    private readonly ILogger _logger;

    public PastDueDunningProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public static Task<List<DunningCampaign>> LoadActiveCampaignsAsync(
        CommerceDbContext db,
        CancellationToken ct) =>
        db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PriorityOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task ProcessAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        Subscription sub,
        IReadOnlyList<DunningCampaign> campaigns,
        bool whatsAppEnabled,
        CancellationToken ct,
        IBillingQueryService? billing = null,
        ICrmQueryService? crm = null)
    {
        var now = DateTime.UtcNow;
        var inferredPaymentMethod = DunningCampaignMatcher.InferPaymentMethod(sub.VaultedTokenId);
        int daysOverdue = (now.Date - sub.NextBillingDate!.Value.Date).Days;
        var targetDate = sub.NextBillingDate.Value.Date;

        if (sub.CurrentDunningCampaignId == null)
        {
            var campaignToAssign = DunningCampaignMatcher.FindBest(
                campaigns, sub.OrganizationId, sub.ProductId, inferredPaymentMethod);

            if (campaignToAssign != null)
            {
                sub.AssignDunningCampaign(campaignToAssign.Id, DunningCampaignSnapshot.From(campaignToAssign));
                _logger.LogInformation(
                    "Assigned dunning campaign {CampaignId} to subscription {SubscriptionId}.",
                    campaignToAssign.Id, sub.Id);
            }
            else
            {
                _logger.LogWarning(
                    "No matching active dunning campaign for subscription {SubscriptionId} (org {OrgId}, product {ProductId}, method {Method}).",
                    sub.Id, sub.OrganizationId, sub.ProductId, inferredPaymentMethod);
                return;
            }
        }

        if (sub.DunningPausedUntil != null && sub.DunningPausedUntil > now)
        {
            return;
        }

        var snapshot = await ResolveSnapshotAsync(db, sub, ct);
        if (snapshot == null)
        {
            return;
        }

        var campaignId = sub.CurrentDunningCampaignId!.Value;
        var dueSteps = snapshot.Steps
            .Where(s => s.DayOffset >= 0 && s.DayOffset <= daysOverdue)
            .Where(s => !sub.ReminderLogs.Any(l =>
                l.DayOffset == s.DayOffset && l.TargetBillingDate.Date == targetDate))
            .OrderBy(s => s.DayOffset)
            .ToList();

        var cycleAttempts = await db.ChargeAttemptLogs
            .Where(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate)
            .ToListAsync(ct);
        var publishedOffSessionThisTick = false;

        foreach (var step in dueSteps)
        {
            var consumeOffset = true;

            if (step.ActionType == "AUTOCHARGE" || step.ActionType == "AUTO_CHARGE")
            {
                var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                var nextAttempt = cycleAttempts.Count + 1;

                var cannotCharge = product == null
                    || !PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                    || sub.IsReminderOnly
                    || sub.HasOpenDispute
                    || nextAttempt > ChargeAttemptLimits.MaxAttemptsPerBillingCycle
                    || string.IsNullOrEmpty(sub.VaultedCustomerId)
                    || string.IsNullOrEmpty(sub.VaultedTokenId);

                var hasHardDecline = cycleAttempts.Any(l =>
                    l.Status == ChargeAttemptLog.StatusFailed
                    && string.Equals(l.DeclineClass, DeclineClassifier.Hard, StringComparison.OrdinalIgnoreCase));

                var hasInFlightOrSettled = publishedOffSessionThisTick
                    || cycleAttempts.Any(l => l.Status == ChargeAttemptLog.StatusPending)
                    || cycleAttempts.Any(l => l.Status == ChargeAttemptLog.StatusSucceeded);

                if (hasHardDecline)
                {
                    var skipped = new ChargeAttemptLog(
                        sub.Id,
                        targetDate,
                        attemptNumber: nextAttempt,
                        source: ChargeAttemptLog.SourceDunning,
                        dunningCampaignId: campaignId,
                        dunningStepId: step.Id);
                    skipped.MarkSkipped("hard_decline_skip", DeclineClassifier.Hard);
                    db.ChargeAttemptLogs.Add(skipped);
                    cycleAttempts.Add(skipped);
                    _logger.LogInformation(
                        "Skipped auto-charge for Subscription {Id} DayOffset={DayOffset}: cycle already has a hard decline.",
                        sub.Id, step.DayOffset);
                }
                else if (cannotCharge || product == null)
                {
                    _logger.LogWarning(
                        "Skipped auto-charge for Subscription {Id} (nextAttempt={NextAttempt}, max={Max}, gateway={Gateway}, reminderOnly={ReminderOnly}) due to limits, reminder-only mode, or missing token.",
                        sub.Id,
                        nextAttempt,
                        ChargeAttemptLimits.MaxAttemptsPerBillingCycle,
                        product?.GatewayName,
                        sub.IsReminderOnly);
                }
                else if (hasInFlightOrSettled)
                {
                    // Do not burn the DayOffset while a PI is processing / already paid this cycle.
                    consumeOffset = false;
                    _logger.LogInformation(
                        "Deferred auto-charge for Subscription {Id} DayOffset={DayOffset}: in-flight PENDING, SUCCEEDED, or already charged this tick.",
                        sub.Id, step.DayOffset);
                }
                else
                {
                    var attempt = new ChargeAttemptLog(
                        sub.Id,
                        targetDate,
                        attemptNumber: nextAttempt,
                        source: ChargeAttemptLog.SourceDunning,
                        dunningCampaignId: campaignId,
                        dunningStepId: step.Id);
                    db.ChargeAttemptLogs.Add(attempt);
                    cycleAttempts.Add(attempt);
                    publishedOffSessionThisTick = true;

                    var breakdown = await Modules.Commerce.Application.SubscriptionBillingAmount.GrossBreakdown(sub, product, billing);
                    await eventBus.PublishAsync(new Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent(
                        sub.OrganizationId,
                        sub.Id,
                        breakdown.Gross,
                        product.Currency,
                        sub.VaultedCustomerId!,
                        sub.VaultedTokenId!,
                        DunningCampaignId: campaignId,
                        GatewayName: product.GatewayName,
                        ChargeAttemptId: attempt.Id,
                        TaxAmount: Modules.Commerce.Application.SubscriptionBillingAmount.LineTax(breakdown),
                        TaxType: breakdown.TaxType
                    ));
                    _logger.LogInformation(
                        "Dispatched auto-charge dunning step DayOffset={DayOffset} for Subscription {Id} (attempt {AttemptNumber}/{Max}).",
                        step.DayOffset, sub.Id, nextAttempt, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                }
            }
            else
            {
                var rawAction = (step.ActionType ?? string.Empty).ToUpperInvariant();
                if (rawAction is not ("EMAIL" or "WHATSAPP" or "ALL"))
                {
                    _logger.LogWarning(
                        "Skipped non-communication dunning ActionType {ActionType} DayOffset={DayOffset} for Subscription {Id}. Terminal cancel/suspend is campaign FinalAction only.",
                        step.ActionType, step.DayOffset, sub.Id);
                }
                else
                {
                    var effectiveAction = DunningStepDispatcher.ResolveEffectiveCommunicationAction(step, whatsAppEnabled);
                    if (effectiveAction == null)
                    {
                        _logger.LogInformation(
                            "Skipped communication dunning WHATSAPP step DayOffset={DayOffset} for Subscription {Id}: WhatsApp disabled and no email body.",
                            step.DayOffset, sub.Id);
                    }
                    else
                    {
                        var sent = await DunningStepDispatcher.DispatchCommunicationStepAsync(
                            db, sub, step, daysOverdue, effectiveAction, eventBus, ct, billing, crm);
                        if (!sent)
                        {
                            consumeOffset = false;
                            _logger.LogWarning(
                                "Did not consume DayOffset={DayOffset} for Subscription {Id}: CRM profile or email missing.",
                                step.DayOffset, sub.Id);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Dispatched communication dunning step DayOffset={DayOffset} for Subscription {Id} as {Action}.",
                                step.DayOffset, sub.Id, effectiveAction);
                        }
                    }
                }
            }

            if (consumeOffset)
            {
                sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
            }
        }

        var terminalDay = ResolveTerminalDayOffset(
            snapshot.GracePeriodDays,
            snapshot.Steps.Select(s => s.DayOffset));
        if (daysOverdue <= terminalDay
            || (snapshot.FinalAction != "CANCEL" && snapshot.FinalAction != "SUSPEND"))
        {
            return;
        }

        if (snapshot.FinalAction == "CANCEL")
        {
            sub.Cancel();
            LazuarMetrics.RecordDunningCancel();
            var trackedCampaign = await db.DunningCampaigns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
            trackedCampaign?.RecordChurn();
            _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Canceled.", sub.Id);
        }
        else
        {
            sub.Suspend();
            _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Suspended.", sub.Id);
        }

        var terminalProduct = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
        var targets = terminalProduct?.FulfillmentTargets.ToList() ?? new List<string>();

        if (snapshot.FinalAction == "CANCEL")
        {
            await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
                sub.OrganizationId,
                sub.Id,
                sub.ClientProfileId,
                sub.ProductId,
                targets));
        }
        else
        {
            await eventBus.PublishAsync(new SubscriptionSuspendedIntegrationEvent(
                sub.OrganizationId,
                sub.Id,
                sub.ClientProfileId,
                sub.ProductId,
                targets));
        }

        var eventTypeString = snapshot.FinalAction == "CANCEL" ? "subscription.canceled" : "subscription.suspended";
        var payloadObj = new
        {
            subscription_id = sub.Id.ToString(),
            client_profile_id = sub.ClientProfileId.ToString(),
            product_id = sub.ProductId.ToString(),
            status = snapshot.FinalAction == "CANCEL" ? "CANCELED" : "SUSPENDED"
        };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        foreach (var target in targets)
        {
            if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
            {
                var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    sub.OrganizationId, internalApp, eventTypeString, payloadElement));
            }
        }
    }

    /// <summary>
    /// Prefer the frozen JSON. If id is set and JSON is missing/corrupt, copy the live campaign
    /// (including archived) so pre-migration rows do not run without a plan.
    /// </summary>
    private async Task<DunningCampaignSnapshot?> ResolveSnapshotAsync(
        CommerceDbContext db,
        Subscription sub,
        CancellationToken ct)
    {
        var parsed = sub.TryGetDunningCampaignSnapshot();
        if (parsed != null && parsed.CampaignId == sub.CurrentDunningCampaignId)
        {
            return parsed;
        }

        if (sub.CurrentDunningCampaignId == null)
        {
            return null;
        }

        var live = await db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == sub.CurrentDunningCampaignId, ct);

        if (live == null)
        {
            _logger.LogWarning(
                "No dunning campaign {CampaignId} for subscription {SubscriptionId}; cannot backfill snapshot.",
                sub.CurrentDunningCampaignId, sub.Id);
            return null;
        }

        var snapshot = DunningCampaignSnapshot.From(live);
        sub.CaptureDunningCampaignSnapshot(snapshot);
        _logger.LogInformation(
            "Backfilled dunning campaign snapshot for subscription {SubscriptionId} from campaign {CampaignId}.",
            sub.Id, live.Id);
        return snapshot;
    }

    /// <summary>
    /// Later of clamped grace and the last past-due (DayOffset &gt;= 0) step. Pre-dunning offsets do not delay terminal.
    /// </summary>
    internal static int ResolveTerminalDayOffset(int gracePeriodDays, IEnumerable<int> dayOffsets)
    {
        var lastPastDueDay = dayOffsets.Where(offset => offset >= 0).DefaultIfEmpty(0).Max();
        return Math.Max(Math.Max(0, gracePeriodDays), lastPastDueDay);
    }
}
