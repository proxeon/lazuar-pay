using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application;

public interface IBroadcastCampaignRepository
{
    void Add(BroadcastCampaign campaign);
    Task SaveChangesAsync(CancellationToken ct = default);
}
