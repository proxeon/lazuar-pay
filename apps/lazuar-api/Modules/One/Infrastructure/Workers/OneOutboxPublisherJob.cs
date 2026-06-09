using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.One.Infrastructure.Workers;

public class OneOutboxPublisherJob : OutboxPublisherJob<OneDbContext>
{
    public OneOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<OneOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger) 
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
