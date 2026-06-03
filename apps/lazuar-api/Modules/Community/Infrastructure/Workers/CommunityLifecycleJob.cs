using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Community.Domain.Events;

namespace Modules.Community.Infrastructure.Workers;

/// <summary>
/// Reclaims background job logic from the old Messaging module.
/// Transitions Community subscriptions based on domain rules.
/// </summary>
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
        // Poll every 6 hours
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

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
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

        // 3. Trigger 3-Day Renewal Reminders (Domain Event -> Outbox -> Integration Event)
        var renewalTargetDate = now.AddDays(3).Date;
        var upcomingRenewals = await db.Subscriptions
            .Where(s => s.Status == "ACTIVE" && s.NextRenewalDate != null && s.NextRenewalDate.Value.Date == renewalTargetDate)
            .ToListAsync(ct);

        foreach (var sub in upcomingRenewals)
        {
            // Publish via Domain Event to ensure it enters the Outbox transactionally
            await mediator.Publish(new SubscriptionRenewalDueDomainEvent(
                sub.Id, sub.OrganizationId, sub.ClientProfileId, sub.NextRenewalDate!.Value), ct);
        }

        if (overdue.Any() || pastDue.Any())
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
