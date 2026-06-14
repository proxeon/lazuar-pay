using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Infrastructure; 

namespace Modules.Lhdn.Application.Queries.Agent;

[AgentTool("List the most recent LHDN e-Invoice submissions, including their validation statuses, UUIDs, and error messages. Used for monitoring pilot clients.", "LHDN", "low", "SUPER_ADMIN", "ADMIN")]
public record ListLhdnSubmissionsAgentQuery(Guid OrganizationId, int Limit = 20) : IQuery<IEnumerable<AgentLhdnSubmissionResult>>;

public record AgentLhdnSubmissionResult(
    string DocumentId,
    string InternalReference,
    string Status,
    string? LhdnUuid,
    string? ErrorMessage,
    string CreatedAt);

public class ListLhdnSubmissionsAgentQueryHandler : IQueryHandler<ListLhdnSubmissionsAgentQuery, IEnumerable<AgentLhdnSubmissionResult>>
{
    private readonly LhdnDbContext _dbContext;

    public ListLhdnSubmissionsAgentQueryHandler(LhdnDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<AgentLhdnSubmissionResult>> Handle(ListLhdnSubmissionsAgentQuery request, CancellationToken ct)
    {
        var safeLimit = request.Limit > 100 ? 100 : request.Limit;

        var documents = await _dbContext.TaxDocuments
            .Where(d => d.OrganizationId == request.OrganizationId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(safeLimit)
            .ToListAsync(ct);

        return documents.Select(d => new AgentLhdnSubmissionResult(
            d.Id.ToString(),
            d.InternalReferenceId,
            d.ValidationStatus,
            d.LhdnUuid,
            d.ErrorMessage,
            d.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        )).ToList();
    }
}
