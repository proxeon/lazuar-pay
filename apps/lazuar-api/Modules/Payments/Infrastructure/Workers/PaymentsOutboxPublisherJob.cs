using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Payments.Infrastructure.Workers;

public class PaymentsOutboxPublisherJob : OutboxPublisherJob<PaymentsDbContext>
{
    public PaymentsOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<PaymentsOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger) 
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
