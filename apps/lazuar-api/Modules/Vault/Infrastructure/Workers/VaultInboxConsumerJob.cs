using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Vault.Infrastructure.Workers;

public class VaultInboxConsumerJob : InboxConsumerJob<VaultDbContext>
{
    public VaultInboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger<VaultInboxConsumerJob> logger, DatabaseJobTrigger jobTrigger)
        : base(scopeFactory, logger, jobTrigger)
    {
    }
}
