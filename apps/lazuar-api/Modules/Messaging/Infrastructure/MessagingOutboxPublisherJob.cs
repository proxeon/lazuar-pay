using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure;

namespace Modules.Messaging.Infrastructure;

public class MessagingOutboxPublisherJob : OutboxPublisherJob<MessagingDbContext>
{
    public MessagingOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<MessagingOutboxPublisherJob> logger) 
        : base(scopeFactory, logger)
    {
    }
}
