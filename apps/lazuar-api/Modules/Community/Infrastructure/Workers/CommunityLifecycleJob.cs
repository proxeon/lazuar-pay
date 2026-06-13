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
using Modules.Payments.Contracts.Events;

namespace Modules.Community.Infrastructure.Workers;

public class CommunityLifecycleJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommunityLifecycleJob> _logger;

    // A system-level identifier to track auto-debit attempts in the idempotency log
    private static readonly Guid AutoDebitScheduleId = new("FFFFFFFF-FFFF-FFFF-FFFF-000000000001");

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

        // Process active subscriptions that have crossed their renewal date
        var overdue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .Where(s => s.Status == "ACTIVE"
                && s.NextRenewalDate != null
                && s.NextRenewalDate < now)
            .Join(db.Plans.IgnoreQueryFilters(), s => s.PlanId, p => p.Id, (s, p) => new { Sub = s, Plan = p })
            .ToListAsync(ct);

        if (overdue.Any())
        {
            foreach (var item in overdue)
            {
                var sub = item.Sub;
                var plan = item.Plan;

                // Branch A: Vaulted Tokens (Auto-Debit)
                if (!string.IsNullOrEmpty(sub.VaultedTokenId) && !string.IsNullOrEmpty(sub.VaultedCustomerId))
                {
                    // Ensure we only attempt to auto-debit once per billing cycle
                    if (!sub.ReminderLogs.Any(l => l.ScheduleId == AutoDebitScheduleId && l.TargetRenewalDate.Date == sub.NextRenewalDate!.Value.Date))
                    {
                        sub.RecordReminderDispatched(AutoDebitScheduleId, sub.NextRenewalDate!.Value);
                        
                        await mediator.Publish(new ExecuteOffSessionChargeIntegrationEvent(
                            sub.OrganizationId,
                            sub.Id,
                            plan.Price,
                            "MYR",
                            sub.VaultedCustomerId,
                            sub.VaultedTokenId
                        ), ct);

                        requiresSave = true;
                        _logger.LogInformation("Dispatched auto-debit request for subscription {Id}.", sub.Id);
                    }
                }
                // Branch B: Manual Renewals
                else
                {
                    sub.MarkAsPastDue();
                    requiresSave = true;
                    _logger.LogInformation("Transitioned subscription {Id} to PAST_DUE state.", sub.Id);
                }
            }
        }

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
                if (sub.PendingCouponId.HasValue)
                {
                    var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == sub.PendingCouponId.Value, ct);
                    if (coupon != null)
                    {
                        coupon.ReleaseReservation();
                    }
                }

                sub.Cancel();
            }
            requiresSave = true;
            _logger.LogInformation("Cancelled {Count} stale PENDING subscription checkout session(s).", stalePending.Count);
        }

        var activeSchedules = await db.ReminderSchedules
            .IgnoreQueryFilters()
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

        foreach (var schedule in activeSchedules)
        {
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
                if (sub.ReminderLogs.Any(l => l.ScheduleId == schedule.Id && l.TargetRenewalDate.Date == targetRenewalDate.Date))
                {
                    continue;
                }

                sub.RecordReminderDispatched(schedule.Id, targetRenewalDate);
                requiresSave = true;

                // Pre-Debit Notification override for vaulted subscriptions
                if (!string.IsNullOrEmpty(sub.VaultedTokenId))
                {
                    var noticeMessage = $"Notice: Your subscription will be automatically renewed on {sub.NextRenewalDate?.ToString("MMM dd, yyyy")}. Your saved payment method will be charged.";
                    sub.SendOneOffReminder(null, noticeMessage, schedule.Channel);
                }
                else
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
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
