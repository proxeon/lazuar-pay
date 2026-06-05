using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using Modules.Community.Infrastructure;
using Modules.Community.Domain.Events;

namespace Modules.Community.Infrastructure.Workers;

public class CommunityLifecycleJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommunityLifecycleJob> _logger;

    public CommunityLifecycleJob(IServiceScopeFactory scopeFactory, ILogger<CommunityLifecycleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Community Lifecycle Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecycleActionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the community lifecycle actions.");
            }

            // Poll every 1 hour to handle specific TimeOfDay schedules accurately
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessLifecycleActionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var now = DateTime.UtcNow;
        bool requiresSave = false;

        // 1. Transition ACTIVE overdue subscriptions to PAST_DUE
        var overdue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "ACTIVE" 
                     && s.NextRenewalDate != null 
                     && s.NextRenewalDate < now)
            .ToListAsync(ct);

        if (overdue.Any())
        {
            foreach (var sub in overdue)
            {
                sub.MarkAsPastDue();
            }
            requiresSave = true;
            _logger.LogInformation("Transitioned {Count} overdue subscription(s) to PAST_DUE state.", overdue.Count);
        }

        // 2. Expire old PAST_DUE subscriptions based on Plan's GracePeriodDays
        var pastDue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextRenewalDate != null)
            .Join(db.Plans.IgnoreQueryFilters(), s => s.PlanId, p => p.Id, (s, p) => new { Sub = s, Plan = p })
            .ToListAsync(ct);

        foreach (var item in pastDue)
        {
            if (item.Sub.NextRenewalDate!.Value.AddDays(item.Plan.GracePeriodDays) < now)
            {
                item.Sub.Expire();
                requiresSave = true;
                _logger.LogWarning("Subscription {Id} exceeded grace period. Transitioned to EXPIRED.", item.Sub.Id);
            }
        }

        // 3. NEW: Clean up stale PENDING subscriptions older than 3 days
        var stalePendingCutoff = now.AddDays(-3);
        var stalePending = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PENDING" 
                     && s.CreatedAt < stalePendingCutoff)
            .ToListAsync(ct);

        if (stalePending.Any())
        {
            foreach (var sub in stalePending)
            {
                sub.Cancel(); // Transitions PENDING -> CANCELLED, raising SubscriptionCancelledDomainEvent to outbox
            }
            requiresSave = true;
            _logger.LogInformation("Cancelled {Count} stale PENDING subscription checkout session(s).", stalePending.Count);
        }

        // 4. Dynamic Reminder Schedules Processing
        var activeSchedules = await db.ReminderSchedules
            .IgnoreQueryFilters()
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

        foreach (var schedule in activeSchedules)
        {
            // Only process schedules that match the current hour
            if (!schedule.TimeOfDay.StartsWith(now.ToString("HH"))) 
            {
                continue;
            }

            var targetRenewalDate = now.Date.AddDays(-schedule.DaysRelativeToDue);

            var query = db.Subscriptions
                .IgnoreQueryFilters()
                .Include(s => s.ReminderLogs)
                .Where(s => s.OrganizationId == schedule.OrganizationId
                         && (s.Status == "ACTIVE" || s.Status == "PAST_DUE")
                         && s.NextRenewalDate != null
                         && s.NextRenewalDate.Value.Date == targetRenewalDate
                         && (s.RemindersPausedUntil == null || s.RemindersPausedUntil < now));

            if (schedule.PlanId.HasValue)
            {
                query = query.Where(s => s.PlanId == schedule.PlanId.Value);
            }

            var subscriptionsToRemind = await query.ToListAsync(ct);

            foreach (var sub in subscriptionsToRemind)
            {
                // Ensure a schedule only fires exactly once per target renewal date per subscription (idempotency check)
                if (sub.ReminderLogs.Any(l => l.ScheduleId == schedule.Id && l.TargetRenewalDate.Date == targetRenewalDate.Date))
                {
                    continue;
                }

                // Mutate state to record the dispatch log locally
                sub.RecordReminderDispatched(schedule.Id, targetRenewalDate);
                requiresSave = true;

                // Fire domain event to local outbox
                await mediator.Publish(new SubscriptionRenewalDueDomainEvent(
                    sub.Id, 
                    sub.OrganizationId, 
                    sub.ClientProfileId, 
                    sub.NextRenewalDate!.Value, 
                    schedule.TemplateId, 
                    schedule.Channel), ct);
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
