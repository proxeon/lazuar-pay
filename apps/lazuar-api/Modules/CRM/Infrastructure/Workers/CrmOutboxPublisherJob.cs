using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.CRM.Infrastructure.Workers;

public class CrmOutboxPublisherJob : OutboxPublisherJob<CrmDbContext>
{
    public CrmOutboxPublisherJob(
        IServiceScopeFactory scopeFactory,
        ILogger<CrmOutboxPublisherJob> logger,
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
