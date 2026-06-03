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

    public void Add(CommunitySubscription subscription)
    {
        _context.Subscriptions.Add(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
