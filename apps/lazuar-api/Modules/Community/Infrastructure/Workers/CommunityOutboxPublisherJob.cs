using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Community.Infrastructure.Workers;

public class CommunityOutboxPublisherJob : OutboxPublisherJob<CommunityDbContext>
{
    public CommunityOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<CommunityOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
