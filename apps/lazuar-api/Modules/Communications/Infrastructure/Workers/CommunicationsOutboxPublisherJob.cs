using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Communications.Infrastructure.Workers;

public class CommunicationsOutboxPublisherJob : OutboxPublisherJob<CommunicationsDbContext>
{
    public CommunicationsOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<CommunicationsOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
