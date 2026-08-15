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
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts;

namespace Modules.Commerce.Infrastructure.Workers;

public partial class DunningEngineJob
{
    private async Task ProcessPastDueSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        List<DunningCampaign> campaigns,
        Subscription sub,
        bool whatsAppEnabled,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
        int daysOverdue = (now.Date - sub.NextBillingDate!.Value.Date).Days;
        var targetDate = sub.NextBillingDate.Value.Date;

        if (sub.CurrentDunningCampaignId == null)
        {
            var campaignToAssign = campaigns.FirstOrDefault(c =>
                c.OrganizationId == sub.OrganizationId &&
                (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
                (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod))
            );

            if (campaignToAssign != null)
            {
                sub.AssignDunningCampaign(campaignToAssign.Id);
            }
            else
            {
                return;
            }
        }

        var campaign = campaigns.FirstOrDefault(c => c.Id == sub.CurrentDunningCampaignId);
        if (campaign == null) return;

        if (daysOverdue >= campaign.GracePeriodDays)
        {
            if (campaign.FinalAction == "CANCEL" || campaign.FinalAction == "SUSPEND")
            {
                if (campaign.FinalAction == "CANCEL")
                {
                    sub.Cancel();
                    LazuarMetrics.RecordDunningCancel();
                    // Campaign is AsNoTracking snapshot — reload for RecordChurn if needed.
                    var trackedCampaign = await db.DunningCampaigns
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c => c.Id == campaign.Id, ct);
                    trackedCampaign?.RecordChurn();
                    _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Canceled.", sub.Id);
                }
                else
                {
                    sub.Suspend();
                    _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Suspended.", sub.Id);
                }

                var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                var targets = product?.FulfillmentTargets.ToList() ?? new List<string>();

                if (campaign.FinalAction == "CANCEL")
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

                var eventTypeString = campaign.FinalAction == "CANCEL" ? "subscription.canceled" : "subscription.suspended";
                var payloadObj = new
                {
                    subscription_id = sub.Id.ToString(),
                    client_profile_id = sub.ClientProfileId.ToString(),
                    product_id = sub.ProductId.ToString(),
                    status = campaign.FinalAction == "CANCEL" ? "CANCELED" : "SUSPENDED"
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
            return;
        }

        var dueSteps = campaign.Steps
            .Where(s => s.DayOffset >= 0 && s.DayOffset <= daysOverdue)
            .Where(s => !sub.ReminderLogs.Any(l =>
                l.DayOffset == s.DayOffset && l.TargetBillingDate.Date == targetDate))
            .OrderBy(s => s.DayOffset)
            .ToList();

        foreach (var step in dueSteps)
        {
            if (step.ActionType == "AUTOCHARGE" || step.ActionType == "AUTO_CHARGE")
            {
                var attemptCount = await db.ChargeAttemptLogs.CountAsync(
                    l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);
                var nextAttempt = attemptCount + 1;
                var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);

                var cannotCharge = product == null
                    || !PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                    || sub.IsReminderOnly
                    || nextAttempt > ChargeAttemptLimits.MaxAttemptsPerBillingCycle
                    || string.IsNullOrEmpty(sub.VaultedCustomerId)
                    || string.IsNullOrEmpty(sub.VaultedTokenId);

                if (cannotCharge || product == null)
                {
                    _logger.LogWarning(
                        "Skipped auto-charge for Subscription {Id} (nextAttempt={NextAttempt}, max={Max}, gateway={Gateway}, reminderOnly={ReminderOnly}) due to limits, reminder-only mode, or missing token.",
                        sub.Id,
                        nextAttempt,
                        ChargeAttemptLimits.MaxAttemptsPerBillingCycle,
                        product?.GatewayName,
                        sub.IsReminderOnly);
                }
                else
                {
                    var attempt = new ChargeAttemptLog(
                        sub.Id,
                        targetDate,
                        attemptNumber: nextAttempt,
                        source: ChargeAttemptLog.SourceDunning,
                        dunningCampaignId: campaign.Id,
                        dunningStepId: step.Id);
                    db.ChargeAttemptLogs.Add(attempt);

                    await eventBus.PublishAsync(new Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent(
                        sub.OrganizationId,
                        sub.Id,
                        product.Price,
                        product.Currency,
                        sub.VaultedCustomerId!,
                        sub.VaultedTokenId!,
                        DunningCampaignId: campaign.Id,
                        GatewayName: product.GatewayName,
                        ChargeAttemptId: attempt.Id
                    ));
                    _logger.LogInformation(
                        "Dispatched auto-charge dunning step DayOffset={DayOffset} for Subscription {Id} (attempt {AttemptNumber}/{Max}).",
                        step.DayOffset, sub.Id, nextAttempt, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                }
            }
            else
            {
                var effectiveAction = ResolveEffectiveCommunicationAction(step, whatsAppEnabled);
                if (effectiveAction == null)
                {
                    _logger.LogInformation(
                        "Skipped communication dunning WHATSAPP step DayOffset={DayOffset} for Subscription {Id}: WhatsApp disabled and no email body.",
                        step.DayOffset, sub.Id);
                }
                else
                {
                    await DispatchCommunicationStepAsync(db, sub, step, daysOverdue, effectiveAction, eventBus, ct);
                    _logger.LogInformation(
                        "Dispatched communication dunning step DayOffset={DayOffset} for Subscription {Id} as {Action}.",
                        step.DayOffset, sub.Id, effectiveAction);
                }
            }

            sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
        }
    }
}
