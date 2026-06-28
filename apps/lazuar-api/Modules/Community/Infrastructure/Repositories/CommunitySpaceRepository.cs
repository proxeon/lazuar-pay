using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

    public async Task<CommunitySpace?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.CommunitySpaces
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);
    }

    public void Add(CommunitySpace space) => _context.CommunitySpaces.Add(space);

    public void Remove(CommunitySpace space) => _context.CommunitySpaces.Remove(space);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}
