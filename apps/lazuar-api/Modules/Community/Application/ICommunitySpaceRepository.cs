using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application;

public interface ICommunitySpaceRepository
{
    Task<CommunitySpace?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    void Add(CommunitySpace space);
    void Remove(CommunitySpace space);
    Task SaveChangesAsync(CancellationToken ct = default);
}
