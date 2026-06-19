using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Lhdn.Application.Queries.Agent;

namespace Modules.Lhdn.Application.Ports;

public interface ILhdnQueryService
{
    Task<IEnumerable<AgentLhdnSubmissionResult>> GetRecentSubmissionsAsync(Guid organizationId, int limit, CancellationToken ct = default);
}
