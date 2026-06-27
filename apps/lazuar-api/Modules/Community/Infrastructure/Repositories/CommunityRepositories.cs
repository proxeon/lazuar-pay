using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.Repositories;

public class CommunityBroadcastRepository : IBroadcastCampaignRepository
{
    private readonly CommunityDbContext _context;
    public CommunityBroadcastRepository(CommunityDbContext context) => _context = context;

    public void Add(BroadcastCampaign campaign) => _context.BroadcastCampaigns.Add(campaign);
    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}
