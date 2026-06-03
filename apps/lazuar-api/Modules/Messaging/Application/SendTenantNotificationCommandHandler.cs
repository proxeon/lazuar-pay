using BuildingBlocks.Application;

namespace Modules.Messaging.Application;

public record SendTenantNotificationCommand(Guid TenantId, string Message) : ICommand
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

public class SendTenantNotificationCommandHandler : ICommandHandler<SendTenantNotificationCommand>
{
    private readonly ITenantReplicaRepository _tenantReplicaRepository;
    private readonly IMessagingService _messagingService;

    public SendTenantNotificationCommandHandler(ITenantReplicaRepository tenantReplicaRepository, IMessagingService messagingService)
    {
        _tenantReplicaRepository = tenantReplicaRepository;
        _messagingService = messagingService;
    }

    public async Task Handle(SendTenantNotificationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantReplicaRepository.GetByIdAsync(request.TenantId);
        if (tenant == null || !tenant.IsActive)
        {
            throw new InvalidOperationException("Tenant is not active or does not exist inside local replicas.");
        }

        await _messagingService.SendMessageAsync(tenant.Slug, $"[System Alert for {tenant.Name}]: {request.Message}");
    }
}
