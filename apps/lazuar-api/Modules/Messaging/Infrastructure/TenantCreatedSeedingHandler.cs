using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Infrastructure;

public class TenantCreatedSeedingHandler : INotificationHandler<TenantCreatedIntegrationEvent>
{
    private readonly MessagingDbContext _context;
    private readonly ILogger<TenantCreatedSeedingHandler> _logger;

    public TenantCreatedSeedingHandler(MessagingDbContext context, ILogger<TenantCreatedSeedingHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(TenantCreatedIntegrationEvent notification, CancellationToken cancellationToken)
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
