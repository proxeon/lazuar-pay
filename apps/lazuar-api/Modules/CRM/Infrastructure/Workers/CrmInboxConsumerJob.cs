using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.CRM.Infrastructure.Workers;

public class CrmInboxConsumerJob : InboxConsumerJob<CrmDbContext>
{
    public CrmInboxConsumerJob(
        IServiceScopeFactory scopeFactory,
        ILogger<CrmInboxConsumerJob> logger,
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
