using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Community.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.Repositories;

public class CommunityPlanRepository : ICommunityPlanRepository
{
    private readonly CommunityDbContext _context;

    public CommunityPlanRepository(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<CommunityPlan?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default)
    {
        return await _context.Plans.FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Slug == slug, ct);
    }

    public async Task<bool> IsSlugUniqueAsync(Guid organizationId, string slug, CancellationToken ct = default)
    {
        return !await _context.Plans.AnyAsync(p => p.OrganizationId == organizationId && p.Slug == slug, ct);
    }

    public void Add(CommunityPlan plan)
    {
        _context.Plans.Add(plan);
    }
}

public class CommunitySubscriptionRepository : ICommunitySubscriptionRepository
{
    private readonly CommunityDbContext _context;

    public CommunitySubscriptionRepository(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task<CommunitySubscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .Include(s => s.PaymentRecords)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<CommunitySubscription?> GetActiveByProfileIdAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default)
    {
        return await _context.Subscriptions
            .Where(s => s.OrganizationId == organizationId 
                     && s.ClientProfileId == clientProfileId 
                     && (s.Status == "ACTIVE" || s.Status == "PAST_DUE"))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public void Add(CommunitySubscription subscription)
    {
        _context.Subscriptions.Add(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}

public class CommunityReminderScheduleRepository : ICommunityReminderScheduleRepository
{
    private readonly CommunityDbContext _context;

    public CommunityReminderScheduleRepository(CommunityDbContext context)
    {
        _context = context;
    }

    public async Task<CommunityReminderSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ReminderSchedules.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public void Add(CommunityReminderSchedule schedule)
    {
        _context.ReminderSchedules.Add(schedule);
    }

    public void Remove(CommunityReminderSchedule schedule)
    {
        _context.ReminderSchedules.Remove(schedule);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
