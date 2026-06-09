using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure;

public class TenantProvisionedSeedingHandler : INotificationHandler<TenantProvisionedIntegrationEvent>
{
    private readonly ILogger<TenantProvisionedSeedingHandler> _logger;

    public TenantProvisionedSeedingHandler(ILogger<TenantProvisionedSeedingHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TenantProvisionedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Messaging] Tenant Replia provisioned: {TenantId} ({Name})", notification.TenantId, notification.Name);
        return Task.CompletedTask;
    }
}
