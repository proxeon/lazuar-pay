using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Workers;

public class DunningEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DunningEngineJob> _logger;

    public DunningEngineJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DunningEngineJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dunning Engine Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDunningAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the dunning engine.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessDunningAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

        var now = DateTime.UtcNow;
        bool requiresSave = false;
        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);

        var campaigns = await db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PriorityOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var preDunningSubs = await db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => s.Status == "ACTIVE" && s.NextBillingDate != null && s.NextBillingDate > now && s.NextBillingDate <= now.AddDays(14))
            .ToListAsync(ct);

        foreach (var sub in preDunningSubs)
        {
            var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
            var campaign = campaigns.FirstOrDefault(c => 
                c.OrganizationId == sub.OrganizationId &&
                (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
                (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod))
            );

            if (campaign == null) continue;

            int daysUntilDue = (sub.NextBillingDate!.Value.Date - now.Date).Days;
            var targetDate = sub.NextBillingDate.Value.Date;

            // Catch-up: all pre-due steps whose absolute offset is still within daysUntilDue.
            var dueSteps = campaign.Steps
                .Where(s => s.DayOffset < 0
                    && Math.Abs(s.DayOffset) <= daysUntilDue
                    && (s.ActionType == "EMAIL" || s.ActionType == "WHATSAPP" || s.ActionType == "ALL"))
                .Where(s => !sub.ReminderLogs.Any(l =>
                    l.DayOffset == s.DayOffset && l.TargetBillingDate.Date == targetDate))
                .OrderBy(s => s.DayOffset)
                .ToList();

            foreach (var step in dueSteps)
            {
                var effectiveAction = ResolveEffectiveCommunicationAction(step, whatsAppEnabled);
                if (effectiveAction == null)
                {
                    _logger.LogInformation(
                        "Skipped pre-dunning WHATSAPP step DayOffset={DayOffset} ({StepId}) for Subscription {SubId}: WhatsApp disabled and no email body.",
                        step.DayOffset, step.Id, sub.Id);
                    sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
                    requiresSave = true;
                    continue;
                }

                await DispatchCommunicationStepAsync(db, sub, step, daysOverdue: 0, effectiveAction, eventBus, ct);
                sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
                requiresSave = true;
                _logger.LogInformation(
                    "Dispatched pre-dunning step DayOffset={DayOffset} ({StepId}) for Subscription {SubId} as {Action}.",
                    step.DayOffset, step.Id, sub.Id, effectiveAction);
            }
        }

        var pastDueSubs = await db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextBillingDate != null && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now))
            .ToListAsync(ct);

        foreach (var sub in pastDueSubs)
        {
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
                    requiresSave = true;
                }
                else
                {
                    continue;
                }
            }

            var campaign = campaigns.FirstOrDefault(c => c.Id == sub.CurrentDunningCampaignId);
            if (campaign == null) continue;

            if (daysOverdue >= campaign.GracePeriodDays)
            {
                if (campaign.FinalAction == "CANCEL" || campaign.FinalAction == "SUSPEND")
                {
                    if (campaign.FinalAction == "CANCEL")
                    {
                        sub.Cancel();
                        campaign.RecordChurn();
                        _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Canceled.", sub.Id);
                    }
                    else
                    {
                        sub.Suspend();
                        _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Suspended.", sub.Id);
                    }

                    var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                    var targets = product?.FulfillmentTargets.ToList() ?? new System.Collections.Generic.List<string>();

                    // Typed lifecycle events: Communications templates + HTTP outbound via SubscriptionLifecycle handlers.
                    // Engine only fans out internal: FulfillmentRequested to avoid double HTTP webhooks.
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
                        // HTTP targets intentionally omitted here: SubscriptionLifecycleIntegrationEventHandlers
                        // publishes OutboundWebhookRequested from the typed cancel/suspend events above.
                    }

                    requiresSave = true;
                }
                continue; 
            }

            // Catch-up: all due steps with DayOffset in [0, daysOverdue] not yet logged, ordered.
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

                    if (nextAttempt > ChargeAttemptLimits.MaxAttemptsPerBillingCycle
                        || string.IsNullOrEmpty(sub.VaultedCustomerId)
                        || string.IsNullOrEmpty(sub.VaultedTokenId))
                    {
                        _logger.LogWarning(
                            "Skipped auto-charge for Subscription {Id} (nextAttempt={NextAttempt}, max={Max}) due to limits or missing token. Falling back.",
                            sub.Id, nextAttempt, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                    }
                    else
                    {
                        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                        if (product != null)
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
                                sub.VaultedCustomerId,
                                sub.VaultedTokenId,
                                DunningCampaignId: campaign.Id,
                                GatewayName: product.GatewayName,
                                ChargeAttemptId: attempt.Id
                            ));
                            _logger.LogInformation(
                                "Dispatched auto-charge dunning step DayOffset={DayOffset} for Subscription {Id} (attempt {AttemptNumber}/{Max}).",
                                step.DayOffset, sub.Id, nextAttempt, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                        }
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
                requiresSave = true;
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// When WhatsApp is not productized (Messaging:WhatsAppEnabled=false), demote WHATSAPP/ALL
    /// to email-only recovery. Pure WhatsApp steps without email copy are skipped.
    /// </summary>
    private static string? ResolveEffectiveCommunicationAction(Domain.Entities.DunningStep step, bool whatsAppEnabled)
    {
        var action = (step.ActionType ?? "EMAIL").ToUpperInvariant();
        if (action is "AUTOCHARGE" or "AUTO_CHARGE") return action;

        if (whatsAppEnabled) return action;

        if (action == "WHATSAPP")
        {
            // Only demote when email copy exists; otherwise skip the step.
            if (!string.IsNullOrWhiteSpace(step.EmailBody))
                return "EMAIL";
            return null;
        }

        if (action == "ALL")
            return "EMAIL";

        return action;
    }

    private async Task DispatchCommunicationStepAsync(
        CommerceDbContext db,
        Domain.Aggregates.Subscription sub,
        Domain.Entities.DunningStep step,
        int daysOverdue,
        string effectiveActionType,
        IEventBus eventBus,
        CancellationToken ct)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);

        var payloadObj = new
        {
            subscription_id = sub.Id.ToString(),
            client_profile_id = sub.ClientProfileId.ToString(),
            product_id = sub.ProductId.ToString(),
            action_type = effectiveActionType,
            subject = step.Subject,
            email_body = step.EmailBody,
            // When forced to EMAIL only, strip WhatsApp body so Messaging does not attempt WA.
            whatsapp_body = effectiveActionType == "EMAIL" ? string.Empty : step.WhatsAppBody,
            plan_name = product?.Name ?? string.Empty,
            amount = product?.Price ?? 0m,
            currency = product?.Currency ?? string.Empty,
            days_overdue = daysOverdue
        };
        
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
            sub.OrganizationId, "COMMUNICATIONS", "reminder.dunning", payloadElement));
    }
}
