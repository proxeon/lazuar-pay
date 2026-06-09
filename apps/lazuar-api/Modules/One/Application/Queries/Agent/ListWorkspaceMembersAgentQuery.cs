// apps/lazuar-api/Modules/One/Application/Queries/Agent/ListWorkspaceMembersAgentQuery.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;

namespace Modules.One.Application.Queries.Agent;

[AgentTool("List all staff and admins in the current workspace to find their User ID.", "low", "SUPER_ADMIN", "ADMIN")]
public record ListWorkspaceMembersAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentWorkspaceMemberResult>>;

public record AgentWorkspaceMemberResult(string UserId, string Name, string Email, string Role);

public class ListWorkspaceMembersAgentQueryHandler : IQueryHandler<ListWorkspaceMembersAgentQuery, IEnumerable<AgentWorkspaceMemberResult>>
{
    private readonly IOneQueryService _queryService;

    public ListWorkspaceMembersAgentQueryHandler(IOneQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentWorkspaceMemberResult>> Handle(ListWorkspaceMembersAgentQuery request, CancellationToken cancellationToken)
    {
        var members = await _queryService.GetWorkspaceMembersAsync(request.OrganizationId);
        
        return members.Select(m => new AgentWorkspaceMemberResult(
            m.GlobalUserId.ToString(), 
            m.Name, 
            m.Email, 
            m.Role)).ToList();
    }
}
