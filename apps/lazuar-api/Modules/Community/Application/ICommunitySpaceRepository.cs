using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application;

public interface ICommunitySpaceRepository
{
    void Add(CommunitySpace space);
    Task SaveChangesAsync(CancellationToken ct = default);
}
