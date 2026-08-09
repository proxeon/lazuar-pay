using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Application.Queries.Agent;

[AgentTool("List the most recent LHDN e-Invoice submissions, including their validation statuses, UUIDs, and error messages. Used for monitoring pilot clients.", "LHDN", "low", "SUPER_ADMIN", "ADMIN")]
public record ListLhdnSubmissionsAgentQuery(Guid OrganizationId, int Limit = 20) : IQuery<IEnumerable<AgentLhdnSubmissionResult>>;

public record AgentLhdnSubmissionResult(
    string DocumentId,
    string InternalReference,
    string Status,
    string? LhdnUuid,
    string? LongId,
    string? ErrorMessage,
    string CreatedAt);

public class ListLhdnSubmissionsAgentQueryHandler : IQueryHandler<ListLhdnSubmissionsAgentQuery, IEnumerable<AgentLhdnSubmissionResult>>
{
    private readonly ILhdnQueryService _queryService;

    public ListLhdnSubmissionsAgentQueryHandler(ILhdnQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentLhdnSubmissionResult>> Handle(ListLhdnSubmissionsAgentQuery request, CancellationToken ct)
    {
        return await _queryService.GetRecentSubmissionsAsync(request.OrganizationId, request.Limit, ct);
    }
}
