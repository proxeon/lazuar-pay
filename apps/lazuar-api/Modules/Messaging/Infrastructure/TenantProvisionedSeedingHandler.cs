using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure;

public class TenantProvisionedSeedingHandler : INotificationHandler<TenantProvisionedIntegrationEvent>
{
    private readonly MessagingDbContext _context;
    private readonly ILogger<TenantProvisionedSeedingHandler> _logger;

    public TenantProvisionedSeedingHandler(MessagingDbContext context, ILogger<TenantProvisionedSeedingHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(TenantProvisionedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Messaging] Provisioning triggers seeding sequence for Tenant: {TenantId} ({Name})", notification.TenantId, notification.Name);

        try
        {
            // Execute automated database seeding inside the Messaging boundary context
            await AutomationRuleSeeder.SeedDefaultRulesAsync(notification.TenantId, _context);
            
            _logger.LogInformation("[Messaging] Successfully seeded templates and automation rules for Tenant: {TenantId}", notification.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Messaging] Failed to execute database seeding sequence for Tenant: {TenantId}", notification.TenantId);
            throw;
        }
    }
}
