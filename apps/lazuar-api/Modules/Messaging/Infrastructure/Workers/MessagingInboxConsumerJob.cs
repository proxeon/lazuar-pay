using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure;

namespace Modules.Messaging.Infrastructure.Workers;

public class MessagingInboxConsumerJob : InboxConsumerJob<MessagingDbContext>
{
    public MessagingInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<MessagingInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
