using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Contracts.Events;
using Modules.Payments.Contracts.Events;
using System.Text.Json;

namespace Modules.Commerce.Infrastructure.Workers;

public class CommerceLifecycleJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommerceLifecycleJob> _logger;
    private const int DefaultGracePeriodDays = 3; // Fallback if no schedules match

    public CommerceLifecycleJob(IServiceScopeFactory scopeFactory, ILogger<CommerceLifecycleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Commerce Lifecycle Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecycleActionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing commerce lifecycle actions.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessLifecycleActionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

        var now = DateTime.UtcNow;
        bool requiresSave = false;

        var overdue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "ACTIVE" && s.NextBillingDate != null && s.NextBillingDate < now)
            .ToListAsync(ct);

        foreach (var sub in overdue)
        {
            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
            if (product == null) continue;

            if (!string.IsNullOrEmpty(sub.VaultedTokenId) && !string.IsNullOrEmpty(sub.VaultedCustomerId))
            {
                var targetDate = sub.NextBillingDate!.Value.Date;
                var attemptExists = await db.ChargeAttemptLogs
                    .AnyAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);

                if (!attemptExists)
                {
                    db.ChargeAttemptLogs.Add(new ChargeAttemptLog(sub.Id, targetDate));
                    
                    await eventBus.PublishAsync(new ExecuteOffSessionChargeIntegrationEvent(
                        sub.OrganizationId,
                        sub.Id,
                        product.Price,
                        product.Currency,
                        sub.VaultedCustomerId,
                        sub.VaultedTokenId
                    ));
                    
                    requiresSave = true;
                    _logger.LogInformation("Dispatched auto-debit request for subscription {Id}.", sub.Id);
                }
            }
            else
            {
                sub.MarkAsPastDue();
                
                var payloadObj = new
                {
                    subscription_id = sub.Id.ToString(),
                    client_profile_id = sub.ClientProfileId.ToString(),
                    product_id = sub.ProductId.ToString(),
                    status = "PAST_DUE"
                };
                var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                foreach (var target in product.FulfillmentTargets)
                {
                    if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
                    {
                        var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                            sub.OrganizationId, internalApp, "subscription.suspended", payloadElement));
                    }
                    else if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                            sub.OrganizationId, target, "subscription.suspended", payloadElement));
                    }
                }
                
                requiresSave = true;
                _logger.LogInformation("Transitioned subscription {Id} to PAST_DUE state.", sub.Id);
            }
        }

        // --- NEW: Dynamic Dunning Evaluation (Replaces hardcoded Grace Period) ---
        var activeSchedules = await db.ReminderSchedules
            .IgnoreQueryFilters()
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

        foreach (var schedule in activeSchedules)
        {
            if (!schedule.TimeOfDay.StartsWith(now.ToString("HH"))) continue;

            var targetBillingDate = now.Date.AddDays(-schedule.DaysRelativeToDue);

            var query = db.Subscriptions
                .IgnoreQueryFilters()
                .Include(s => s.ReminderLogs)
                .Where(s => s.OrganizationId == schedule.OrganizationId
                    && (s.Status == "ACTIVE" || s.Status == "PAST_DUE")
                    && s.NextBillingDate != null
                    && s.NextBillingDate.Value.Date == targetBillingDate);

            if (schedule.ProductId.HasValue)
            {
                query = query.Where(s => s.ProductId == schedule.ProductId.Value);
            }

            var subscriptionsToRemind = await query.ToListAsync(ct);

            foreach (var sub in subscriptionsToRemind)
            {
                if (sub.ReminderLogs.Any(l => l.ScheduleId == schedule.Id && l.TargetBillingDate.Date == targetBillingDate.Date))
                    continue;

                sub.RecordReminderDispatched(schedule.Id, targetBillingDate);
                requiresSave = true;

                // Here we construct a generic webhook payload that Communications can intercept to send the actual email
                var payloadObj = new
                {
                    subscription_id = sub.Id.ToString(),
                    client_profile_id = sub.ClientProfileId.ToString(),
                    product_id = sub.ProductId.ToString(),
                    template_id = schedule.TemplateId.ToString(),
                    channel = schedule.Channel
                };
                var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                    sub.OrganizationId, "COMMUNICATIONS", "reminder.due", payloadElement));
            }
        }

        // Final Grace Period Catch-All
        var pastDue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextBillingDate != null)
            .ToListAsync(ct);

        foreach (var sub in pastDue)
        {
            // We use the default 3 days if there are no explicit cancellation schedules
            if (sub.NextBillingDate!.Value.AddDays(DefaultGracePeriodDays) < now)
            {
                sub.Cancel();
                
                var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                
                var payloadObj = new
                {
                    subscription_id = sub.Id.ToString(),
                    client_profile_id = sub.ClientProfileId.ToString(),
                    product_id = sub.ProductId.ToString(),
                    status = "CANCELED"
                };
                var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                var targets = product?.FulfillmentTargets.ToList() ?? new System.Collections.Generic.List<string>();
                foreach (var target in targets)
                {
                    if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
                    {
                        var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                            sub.OrganizationId, internalApp, "subscription.canceled", payloadElement));
                    }
                    else if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                            sub.OrganizationId, target, "subscription.canceled", payloadElement));
                    }
                }
                
                requiresSave = true;
                _logger.LogWarning("Subscription {Id} exceeded grace period. Transitioned to CANCELED.", sub.Id);
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
