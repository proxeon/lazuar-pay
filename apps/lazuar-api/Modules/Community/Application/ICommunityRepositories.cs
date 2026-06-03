using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application;

public interface ICommunityPlanRepository
{
    Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsSlugUniqueAsync(Guid organizationId, string slug, CancellationToken ct = default);
    void Add(CommunityPlan plan);
}

public interface ICommunitySubscriptionRepository
{
    Task<CommunitySubscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(CommunitySubscription subscription);
    Task SaveChangesAsync(CancellationToken ct = default);
}
