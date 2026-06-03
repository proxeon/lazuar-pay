using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Application;

public class TenantCreatedIntegrationEventHandler : IIntegrationEventHandler<TenantCreatedIntegrationEvent>
{
    private readonly MessagingDbContext _context;

    public TenantCreatedIntegrationEventHandler(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(TenantCreatedIntegrationEvent @event)
    {
        var inboxMessage = new InboxMessage
        {
            Id = @event.Id,
            Type = typeof(TenantCreatedIntegrationEvent).AssemblyQualifiedName ?? typeof(TenantCreatedIntegrationEvent).FullName!,
            Data = JsonSerializer.Serialize(@event)
        };

        await _context.InboxMessages.AddAsync(inboxMessage);
        await _context.SaveChangesAsync();
    }
}
