using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnInboxConsumerJob : InboxConsumerJob<LhdnDbContext>
{
    public LhdnInboxConsumerJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LhdnInboxConsumerJob> logger,
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
