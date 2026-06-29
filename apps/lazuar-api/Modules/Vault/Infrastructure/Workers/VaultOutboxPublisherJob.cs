using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Vault.Infrastructure.Workers;

public class VaultOutboxPublisherJob : OutboxPublisherJob<VaultDbContext>
{
    public VaultOutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger<VaultOutboxPublisherJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
