using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List all active promotional coupons to find a Coupon Code or check usage limits.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record ListCouponsAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentCouponResult>>;

public record AgentCouponResult(
    string CouponId,
    string Code,
    string DiscountType,
    decimal Amount,
    int MaxUses,
    int UsedCount,
    DateTime? ExpiresAt);

public class ListCouponsAgentQueryHandler : IQueryHandler<ListCouponsAgentQuery, IEnumerable<AgentCouponResult>>
{
    private readonly ICommunityQueryService _queryService;

    public ListCouponsAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentCouponResult>> Handle(ListCouponsAgentQuery request, CancellationToken cancellationToken)
    {
        var coupons = await _queryService.GetCouponsAsync(request.OrganizationId);
        return coupons.Select(c => new AgentCouponResult(
            c.Id,
            c.Code,
            c.Discount_type,
            (decimal)c.Amount,
            c.Max_uses,
            c.Used_count,
            c.Expires_at?.UtcDateTime)).ToList();
    }
}
