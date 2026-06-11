using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application;

public interface ICommunityPlanRepository
{
    Task<CommunityPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommunityPlan?> GetBySlugAsync(Guid organizationId, string slug, CancellationToken ct = default);
    Task<bool> IsSlugUniqueAsync(Guid organizationId, string slug, CancellationToken ct = default);
    void Add(CommunityPlan plan);
}

public interface ICommunitySubscriptionRepository
{
    Task<CommunitySubscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommunitySubscription?> GetActiveByProfileIdAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default);
    Task<IEnumerable<Guid>> GetSubscriptionIdsByProfileIdAsync(Guid organizationId, Guid clientProfileId, CancellationToken ct = default);
    void Add(CommunitySubscription subscription);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICommunityReminderScheduleRepository
{
    Task<CommunityReminderSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(CommunityReminderSchedule schedule);
    void Remove(CommunityReminderSchedule schedule);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICommunityCouponRepository
{
    Task<CommunityCoupon?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommunityCoupon?> GetByCodeAsync(Guid organizationId, string code, CancellationToken ct = default);
    void Add(CommunityCoupon coupon);
    void Update(CommunityCoupon coupon);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IBroadcastCampaignRepository
{
    void Add(BroadcastCampaign campaign);
    Task SaveChangesAsync(CancellationToken ct = default);
}
