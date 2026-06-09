// apps/lazuar-api/Modules/One/Application/Queries/Agent/GetWorkspaceDetailsAgentQuery.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;

namespace Modules.One.Application.Queries.Agent;

[AgentTool("Get the core details of the current organization/workspace, including its public tenant slug (which is used in URLs) and active status.", "low", "SUPER_ADMIN", "ADMIN")]
public record GetWorkspaceDetailsAgentQuery(Guid OrganizationId) : IQuery<AgentWorkspaceDetailsResult>;

public record AgentWorkspaceDetailsResult(string WorkspaceId, string Name, string Slug, bool IsActive);

public class GetWorkspaceDetailsAgentQueryHandler : IQueryHandler<GetWorkspaceDetailsAgentQuery, AgentWorkspaceDetailsResult>
{
    private readonly IOneQueryService _queryService;

    public GetWorkspaceDetailsAgentQueryHandler(IOneQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<AgentWorkspaceDetailsResult> Handle(GetWorkspaceDetailsAgentQuery request, CancellationToken cancellationToken)
    {
        var workspace = await _queryService.GetWorkspaceByIdAsync(request.OrganizationId);
        
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        return new AgentWorkspaceDetailsResult(
            workspace.Id.ToString(),
            workspace.Name,
            workspace.Slug,
            workspace.IsActive
        );
    }
}
