using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Workers;

public class DunningEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DunningEngineJob> _logger;
    private readonly BackgroundWorkerOptions _options;
    private const int BatchSize = 50;

    public DunningEngineJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DunningEngineJob> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
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

            await Task.Delay(_options.DunningEngineInterval, stoppingToken);
        }
    }

    /// <summary>One engine cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessDunningAsync(ct);

    private async Task ProcessDunningAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var campaigns = await db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PriorityOrder)
            .ThenByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);

        await ProcessClaimedBatchAsync(
            ClaimMode.PreDunning,
            campaigns,
            whatsAppEnabled,
            ct);

        await ProcessClaimedBatchAsync(
            ClaimMode.PastDue,
            campaigns,
            whatsAppEnabled,
            ct);
    }

    private enum ClaimMode
    {
        PreDunning,
        PastDue
    }

    private async Task ProcessClaimedBatchAsync(
        ClaimMode mode,
        List<DunningCampaign> campaigns,
        bool whatsAppEnabled,
        CancellationToken ct)
    {
        var failedIds = new HashSet<Guid>();

        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            Subscription? sub;

            try
            {
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimSubscriptionAsync(db, mode, failedIds, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimSubscriptionInMemoryAsync(db, mode, failedIds, ct);
                    if (sub == null) break;
                }

                try
                {
                    if (mode == ClaimMode.PreDunning)
                    {
                        await ProcessPreDunningSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct);
                    }
                    else
                    {
                        await ProcessPastDueSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct);
                    }

                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    failedIds.Add(sub.Id);
                    _logger.LogError(ex, "Dunning failed for subscription {Id}; continuing batch.", sub.Id);
                    if (tx != null) await tx.RollbackAsync(ct);
                }
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        }
    }

    private static async Task<Subscription?> ClaimSubscriptionAsync(
        CommerceDbContext db,
        ClaimMode mode,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND s."Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";

        string sql = mode switch
        {
            ClaimMode.PreDunning => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'ACTIVE'
                  AND s."NextBillingDate" IS NOT NULL
                  AND s."NextBillingDate" > NOW()
                  AND s."NextBillingDate" <= NOW() + INTERVAL '14 days'
                  {excludeClause}
                ORDER BY s."NextBillingDate"
                LIMIT 1
                FOR UPDATE SKIP LOCKED;
                """,
            ClaimMode.PastDue => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'PAST_DUE'
                  AND s."NextBillingDate" IS NOT NULL
                  AND (s."DunningPausedUntil" IS NULL OR s."DunningPausedUntil" <= NOW())
                  {excludeClause}
                ORDER BY s."NextBillingDate"
                LIMIT 1
                FOR UPDATE SKIP LOCKED;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var sub = await db.Subscriptions
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);

        if (sub == null) return null;

        // Load reminder logs for catch-up matching (FromSql composition may not Include).
        await db.Entry(sub).Collection(s => s.ReminderLogs).LoadAsync(ct);
        return sub;
    }

    private static async Task<Subscription?> ClaimSubscriptionInMemoryAsync(
        CommerceDbContext db,
        ClaimMode mode,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        IQueryable<Subscription> query = db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => !excludeIds.Contains(s.Id));

        query = mode switch
        {
            ClaimMode.PreDunning => query.Where(s =>
                s.Status == "ACTIVE"
                && s.NextBillingDate != null
                && s.NextBillingDate > now
                && s.NextBillingDate <= now.AddDays(14)),
            ClaimMode.PastDue => query.Where(s =>
                s.Status == "PAST_DUE"
                && s.NextBillingDate != null
                && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now)),
            _ => query.Where(_ => false)
        };

        return await query.OrderBy(s => s.NextBillingDate).FirstOrDefaultAsync(ct);
    }

    private async Task ProcessPreDunningSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        List<DunningCampaign> campaigns,
        Subscription sub,
        bool whatsAppEnabled,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
        var campaign = campaigns.FirstOrDefault(c =>
            c.OrganizationId == sub.OrganizationId &&
            (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
            (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod))
        );

        if (campaign == null) return;

        int daysUntilDue = (sub.NextBillingDate!.Value.Date - now.Date).Days;
        var targetDate = sub.NextBillingDate.Value.Date;

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
                continue;
            }

            await DispatchCommunicationStepAsync(db, sub, step, daysOverdue: 0, effectiveAction, eventBus, ct);
            sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
            _logger.LogInformation(
                "Dispatched pre-dunning step DayOffset={DayOffset} ({StepId}) for Subscription {SubId} as {Action}.",
                step.DayOffset, step.Id, sub.Id, effectiveAction);
        }
    }

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
        Subscription sub,
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
