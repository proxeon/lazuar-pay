using System;
using System.Collections.Generic;
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
    public CommunityPlanRepository(CommunityDbContext context) => _context = context;

    public async Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<CommunityPlan?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default) =>
        await _context.Plans.FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Slug == slug, ct);

    public async Task<bool> IsSlugUniqueAsync(Guid organizationId, string slug, CancellationToken ct = default) =>
        !await _context.Plans.AnyAsync(p => p.OrganizationId == organizationId && p.Slug == slug, ct);

    public void Add(CommunityPlan plan) => _context.Plans.Add(plan);
}

public class CommunitySubscriptionRepository : ICommunitySubscriptionRepository
{
    private readonly CommunityDbContext _context;
    public CommunitySubscriptionRepository(CommunityDbContext context) => _context = context;

    public async Task<CommunitySubscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Check EF Core's active memory tracker first to support pre-save Domain Events
        var local = _context.Subscriptions.Local.FirstOrDefault(s => s.Id == id);
        if (local != null) return local;

        // Fallback to querying the actual database
        return await _context.Subscriptions.Include(s => s.PaymentRecords).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<CommunitySubscription?> GetActiveByProfileIdAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default) =>
        await _context.Subscriptions
            .Where(s => s.OrganizationId == organizationId && s.ClientProfileId == clientProfileId && (s.Status == "ACTIVE" || s.Status == "PAST_DUE"))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<Guid>> GetSubscriptionIdsByProfileIdAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default) =>
        await _context.Subscriptions.IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId && s.ClientProfileId == clientProfileId && s.Status != "CANCELLED" && s.Status != "BANNED" && s.Status != "EXPIRED")
            .Select(s => s.Id).ToListAsync(ct);

    public void Add(CommunitySubscription subscription) => _context.Subscriptions.Add(subscription);
    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}

public class CommunityReminderScheduleRepository : ICommunityReminderScheduleRepository
{
    private readonly CommunityDbContext _context;
    public CommunityReminderScheduleRepository(CommunityDbContext context) => _context = context;

    public async Task<CommunityReminderSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.ReminderSchedules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(CommunityReminderSchedule schedule) => _context.ReminderSchedules.Add(schedule);
    public void Remove(CommunityReminderSchedule schedule) => _context.ReminderSchedules.Remove(schedule);
    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}

public class CommunityCouponRepository : ICommunityCouponRepository
{
    private readonly CommunityDbContext _context;
    public CommunityCouponRepository(CommunityDbContext context) => _context = context;

    public async Task<CommunityCoupon?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<CommunityCoupon?> GetByCodeAsync(Guid organizationId, string code, CancellationToken ct = default)
    {
        var normalizedCode = code.ToUpperInvariant().Trim();
        return await _context.Coupons.FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Code == normalizedCode, ct);
    }

    public void Add(CommunityCoupon coupon) => _context.Coupons.Add(coupon);
    public void Update(CommunityCoupon coupon) => _context.Coupons.Update(coupon);
    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}

public class CommunityBroadcastRepository : IBroadcastCampaignRepository
{
    private readonly CommunityDbContext _context;
    public CommunityBroadcastRepository(CommunityDbContext context) => _context = context;

    public void Add(BroadcastCampaign campaign) => _context.BroadcastCampaigns.Add(campaign);
    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}
