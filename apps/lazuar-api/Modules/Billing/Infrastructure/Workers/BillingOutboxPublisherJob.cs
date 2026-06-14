using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Billing.Infrastructure.Workers;

public class BillingOutboxPublisherJob : OutboxPublisherJob<BillingDbContext>
{
    public BillingOutboxPublisherJob(
        IServiceScopeFactory scopeFactory, 
        ILogger<BillingOutboxPublisherJob> logger, 
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
