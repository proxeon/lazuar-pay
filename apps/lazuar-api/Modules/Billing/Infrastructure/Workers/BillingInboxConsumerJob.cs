using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Billing.Infrastructure.Workers;

public class BillingInboxConsumerJob : InboxConsumerJob<BillingDbContext>
{
    public BillingInboxConsumerJob(
        IServiceScopeFactory scopeFactory, 
        ILogger<BillingInboxConsumerJob> logger, 
        DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
