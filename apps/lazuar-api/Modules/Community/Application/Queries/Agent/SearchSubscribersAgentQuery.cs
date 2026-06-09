// apps/lazuar-api/Modules/Community/Application/Queries/Agent/SearchSubscribersAgentQuery.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Search for subscribers by name or email to find their Subscription ID.", "low", "SUPER_ADMIN", "ADMIN")]
public record SearchSubscribersAgentQuery(Guid OrganizationId, string SearchTerm) : IQuery<IEnumerable<AgentSubscriberResult>>;

public record AgentSubscriberResult(string SubscriptionId, string ClientProfileId, string Name, string Email, string Status, string PlanName);

public class SearchSubscribersAgentQueryHandler : IQueryHandler<SearchSubscribersAgentQuery, IEnumerable<AgentSubscriberResult>>
{
    private readonly ICommunityQueryService _queryService;

    public SearchSubscribersAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentSubscriberResult>> Handle(SearchSubscribersAgentQuery request, CancellationToken cancellationToken)
    {
        var response = await _queryService.GetSubscribersAsync(request.OrganizationId, 1, 500);
        var term = request.SearchTerm?.ToLowerInvariant() ?? "";
        
        return response.Data
            .Where(s => (s.Customer_name?.ToLowerInvariant().Contains(term) == true) || 
                        (s.Customer_email?.ToLowerInvariant().Contains(term) == true))
            .Select(s => new AgentSubscriberResult(
                s.Id, 
                s.Client_profile_id, 
                s.Customer_name ?? "Unknown", 
                s.Customer_email ?? "Unknown", 
                s.Status, 
                s.Plan_name))
            .ToList();
    }
}
