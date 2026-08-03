using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnOutboxPublisherJob : OutboxPublisherJob<LhdnDbContext>
{
    public LhdnOutboxPublisherJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LhdnOutboxPublisherJob> logger,
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
