using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;
using Modules.One.Contracts;

namespace Modules.One.Application.Queries.Agent;

[AgentTool("List all provisioned app modules (e.g., COMMUNITY, VAULT) and their current active/inactive status for the workspace.", "CORE", "low", "SUPER_ADMIN", "ADMIN")]
public record ListAppEntitlementsAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentEntitlementResult>>;

public record AgentEntitlementResult(string AppId, bool IsActive);

public class ListAppEntitlementsAgentQueryHandler : IQueryHandler<ListAppEntitlementsAgentQuery, IEnumerable<AgentEntitlementResult>>
{
    private readonly IOneQueryService _queryService;

    public ListAppEntitlementsAgentQueryHandler(IOneQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentEntitlementResult>> Handle(ListAppEntitlementsAgentQuery request, CancellationToken cancellationToken)
    {
        var entitlements = await _queryService.GetWorkspaceEntitlementsAsync(request.OrganizationId);
        return entitlements.Select(e => new AgentEntitlementResult(e.AppId, e.IsActive)).ToList();
    }
}
