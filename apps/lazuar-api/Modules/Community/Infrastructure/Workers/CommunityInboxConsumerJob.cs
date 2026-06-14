using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Community.Infrastructure.Workers;

public class CommunityInboxConsumerJob : InboxConsumerJob<CommunityDbContext>
{
    public CommunityInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<CommunityInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
