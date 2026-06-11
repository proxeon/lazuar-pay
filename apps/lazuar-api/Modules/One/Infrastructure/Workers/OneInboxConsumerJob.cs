using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.One.Infrastructure.Workers;

public class OneInboxConsumerJob : InboxConsumerJob<OneDbContext>
{
    public OneInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<OneInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
