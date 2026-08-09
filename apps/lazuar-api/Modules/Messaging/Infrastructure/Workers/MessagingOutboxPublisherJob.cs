using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure;

namespace Modules.Messaging.Infrastructure.Workers;

public class MessagingOutboxPublisherJob : OutboxPublisherJob<MessagingDbContext>
{
    public MessagingOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<MessagingOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
