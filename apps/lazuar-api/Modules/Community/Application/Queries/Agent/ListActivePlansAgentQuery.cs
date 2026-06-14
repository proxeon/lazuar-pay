using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List all active subscription plans to find a Plan ID, Slug, or check available capacity.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record ListActivePlansAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentPlanResult>>;

public record AgentPlanResult(
    string PlanId,
    string Slug,
    string Name,
    double Price,
    string Interval,
    string Audience,
    int? MaxCapacity,
    int EnrolledCount,
    int? SpotsRemaining,
    bool IsFull);

public class ListActivePlansAgentQueryHandler : IQueryHandler<ListActivePlansAgentQuery, IEnumerable<AgentPlanResult>>
{
    private readonly ICommunityQueryService _queryService;

    public ListActivePlansAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentPlanResult>> Handle(ListActivePlansAgentQuery request, CancellationToken cancellationToken)
    {
        var plans = await _queryService.GetAdminPlansAsync(request.OrganizationId);

        return plans
            .Where(p => p.Is_active)
            .Select(p => new AgentPlanResult(
                p.Id,
                p.Slug,
                p.Name,
                p.Price,
                p.Interval,
                p.Audience,
                p.Max_capacity,
                p.Enrolled_count,
                p.Spots_remaining,
                p.Is_full))
            .ToList();
    }
}
