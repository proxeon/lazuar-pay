using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        // Poll every 1 hour to catch specific TimeOfDay schedules
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing community lifecycle job.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessSubscriptionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var now = DateTime.UtcNow;

        // 1. Transition to PAST_DUE
        var overdue = await db.Subscriptions
            .Where(s => s.Status == "ACTIVE" && s.NextRenewalDate != null && s.NextRenewalDate < now)
            .ToListAsync(ct);

        foreach (var sub in overdue)
        {
            sub.MarkAsPastDue();
        }

        // 2. Expire old PAST_DUE subscriptions based on Grace Period
        var pastDue = await db.Subscriptions
            .Where(s => s.Status == "PAST_DUE" && s.NextRenewalDate != null)
            .Join(db.Plans, s => s.PlanId, p => p.Id, (s, p) => new { Sub = s, Plan = p })
            .ToListAsync(ct);

        foreach (var item in pastDue)
        {
            if (item.Sub.NextRenewalDate!.Value.AddDays(item.Plan.GracePeriodDays) < now)
            {
                item.Sub.Expire();
            }
        }

        // 3. Dynamic Reminder Schedules
        var currentHourMinute = now.ToString("HH:mm");
        
        var activeSchedules = await db.ReminderSchedules
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

        foreach (var schedule in activeSchedules)
        {
            // Only process schedules that match the current hour (rough matching for hourly polling)
            if (!schedule.TimeOfDay.StartsWith(now.ToString("HH"))) continue;

            // Calculate the exact date the subscription should be due to trigger this reminder
            var targetRenewalDate = now.Date.AddDays(-schedule.DaysRelativeToDue);

            var query = db.Subscriptions
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
                await mediator.Publish(new SubscriptionRenewalDueDomainEvent(
                    sub.Id, 
                    sub.OrganizationId, 
                    sub.ClientProfileId, 
                    sub.NextRenewalDate!.Value, 
                    schedule.TemplateId, 
                    schedule.Channel), ct);
            }
        }

        if (overdue.Any() || pastDue.Any())
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
