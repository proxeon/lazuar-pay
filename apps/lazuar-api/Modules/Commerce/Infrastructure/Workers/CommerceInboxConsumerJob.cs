using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Commerce.Infrastructure.Workers;

public class CommerceInboxConsumerJob : InboxConsumerJob<CommerceDbContext>
{
    public CommerceInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<CommerceInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
