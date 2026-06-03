using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure;

namespace Modules.Tenant.Infrastructure;

public class TenantOutboxPublisherJob : OutboxPublisherJob<TenantDbContext>
{
    public TenantOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<TenantOutboxPublisherJob> logger) 
        : base(scopeFactory, logger)
    {
    }
}
