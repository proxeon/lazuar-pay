using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantUpdatedIntegrationEventHandler : IIntegrationEventHandler<TenantUpdatedIntegrationEvent>
{
    private readonly MessagingDbContext _context;

    public TenantUpdatedIntegrationEventHandler(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(TenantUpdatedIntegrationEvent @event)
    {
        var inboxMessage = new InboxMessage
        {
            Id = @event.Id,
            Type = typeof(TenantUpdatedIntegrationEvent).AssemblyQualifiedName ?? typeof(TenantUpdatedIntegrationEvent).FullName!,
            Data = JsonSerializer.Serialize(@event)
        };

        await _context.InboxMessages.AddAsync(inboxMessage);
        await _context.SaveChangesAsync();
    }
}
