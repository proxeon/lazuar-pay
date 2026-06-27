using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.Repositories;

public class CommunitySpaceRepository : ICommunitySpaceRepository
{
    private readonly CommunityDbContext _context;

    public CommunitySpaceRepository(CommunityDbContext context)
    {
        _context = context;
    }

    public void Add(CommunitySpace space) => _context.CommunitySpaces.Add(space);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}
