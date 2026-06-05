using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Payments.Infrastructure.Workers;

public class PaymentsInboxConsumerJob : InboxConsumerJob<PaymentsDbContext>
{
    public PaymentsInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<PaymentsInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger) 
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
