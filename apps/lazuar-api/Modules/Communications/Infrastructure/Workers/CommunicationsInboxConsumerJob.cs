using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Communications.Infrastructure.Workers;

public class CommunicationsInboxConsumerJob : InboxConsumerJob<CommunicationsDbContext>
{
    public CommunicationsInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<CommunicationsInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
