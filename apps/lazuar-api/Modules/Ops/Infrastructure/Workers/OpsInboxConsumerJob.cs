using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Ops.Infrastructure.Workers;

public class OpsInboxConsumerJob : InboxConsumerJob<OpsDbContext>
{
    public OpsInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<OpsInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
