using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class TenantProvisionedIntegrationEventHandler : IIntegrationEventHandler<TenantProvisionedIntegrationEvent>
{
    private readonly MessagingDbContext _context;

    public TenantProvisionedIntegrationEventHandler(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(TenantProvisionedIntegrationEvent @event)
    {
        var inboxMessage = new InboxMessage
        {
            Id = @event.Id,
            Type = typeof(TenantProvisionedIntegrationEvent).AssemblyQualifiedName ?? typeof(TenantProvisionedIntegrationEvent).FullName!,
            Data = JsonSerializer.Serialize(@event)
        };

        await _context.InboxMessages.AddAsync(inboxMessage);
        await _context.SaveChangesAsync();
    }
}
