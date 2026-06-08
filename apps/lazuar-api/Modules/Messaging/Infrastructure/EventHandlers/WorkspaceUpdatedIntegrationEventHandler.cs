using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class WorkspaceUpdatedIntegrationEventHandler : IIntegrationEventHandler<WorkspaceUpdatedIntegrationEvent>
{
    private readonly MessagingDbContext _context;

    public WorkspaceUpdatedIntegrationEventHandler(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(WorkspaceUpdatedIntegrationEvent @event)
    {
        var inboxMessage = new InboxMessage
        {
            Id = @event.Id,
            Type = typeof(WorkspaceUpdatedIntegrationEvent).AssemblyQualifiedName ?? typeof(WorkspaceUpdatedIntegrationEvent).FullName!,
            Data = JsonSerializer.Serialize(@event)
        };

        await _context.InboxMessages.AddAsync(inboxMessage);
        await _context.SaveChangesAsync();
    }
}
