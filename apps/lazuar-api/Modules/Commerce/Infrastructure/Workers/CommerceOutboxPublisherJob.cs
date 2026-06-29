using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Commerce.Infrastructure.Workers;

public class CommerceOutboxPublisherJob : OutboxPublisherJob<CommerceDbContext>
{
    public CommerceOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<CommerceOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
