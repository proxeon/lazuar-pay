using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List all subscribers enrolled in a specific subscription plan. Use this when you need to see who is in a specific program.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record ListPlanSubscribersAgentQuery(Guid OrganizationId, Guid PlanId) : IQuery<IEnumerable<AgentPlanSubscriberResult>>;

public record AgentPlanSubscriberResult(string SubscriptionId, string ClientProfileId, string Name, string Email, string Status);

public class ListPlanSubscribersAgentQueryHandler : IQueryHandler<ListPlanSubscribersAgentQuery, IEnumerable<AgentPlanSubscriberResult>>
{
    private readonly ICommunityQueryService _queryService;

    public ListPlanSubscribersAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentPlanSubscriberResult>> Handle(ListPlanSubscribersAgentQuery request, CancellationToken cancellationToken)
    {
        var planIdStr = request.PlanId.ToString();

        // Fetch the active subscriber roster. 
        // We use a high limit (500) to ensure we capture the roster in a single pass for the AI context window.
        var response = await _queryService.GetSubscribersAsync(request.OrganizationId, 1, 500);

        return response.Data
            .Where(s => s.Plan_id == planIdStr)
            .Select(s => new AgentPlanSubscriberResult(
                s.Id,
                s.Client_profile_id,
                s.Customer_name ?? "Unknown",
                s.Customer_email ?? "Unknown",
                s.Status))
            .ToList();
    }
}
