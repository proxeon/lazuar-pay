using BuildingBlocks.Application;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public record SendTenantNotificationCommand(Guid TenantId, string Message) : ICommand;

public class SendTenantNotificationCommandHandler : ICommandHandler<SendTenantNotificationCommand>
{
    private readonly ITenantQueryService _tenantQueryService;
    private readonly IMessagingService _messagingService;

    public SendTenantNotificationCommandHandler(ITenantQueryService tenantQueryService, IMessagingService messagingService)
    {
        _tenantQueryService = tenantQueryService;
        _messagingService = messagingService;
    }

    public async Task Handle(SendTenantNotificationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantQueryService.GetTenantByIdAsync(request.TenantId);
        if (tenant == null || !tenant.IsActive)
        {
            throw new InvalidOperationException("Tenant is not active or does not exist.");
        }

        await _messagingService.SendMessageAsync(tenant.Slug, $"[System Alert for {tenant.Name}]: {request.Message}");
    }
}
