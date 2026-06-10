using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Ops.Infrastructure.Workers;

public class OpsOutboxPublisherJob : OutboxPublisherJob<OpsDbContext>
{
    public OpsOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<OpsOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger) 
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
