using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Communications.Contracts.Events;
using Modules.Communications.Domain;
using Modules.One.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class AppEntitlementGrantedIntegrationEventHandler : IIntegrationEventHandler<AppEntitlementGrantedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public AppEntitlementGrantedIntegrationEventHandler(
        CommunicationsDbContext dbContext,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(AppEntitlementGrantedIntegrationEvent @event)
    {
        if (@event.AppId != "COMMUNITY" && @event.AppId != "COMMERCE" && @event.AppId != "VAULT") return;

        var existingNames = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == @event.TenantId)
            .Select(t => t.Name)
            .ToListAsync();

        var missing = DefaultMessageTemplates.All
            .Where(d => existingNames.All(n => !string.Equals(n, d.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0) return;

        foreach (var definition in missing)
        {
            _dbContext.MessageTemplates.Add(DefaultMessageTemplates.CreateEntity(@event.TenantId, definition));
        }

        await _eventBus.PublishAsync(new DefaultTemplatesSeededIntegrationEvent(@event.TenantId));
        await _dbContext.SaveChangesAsync();
    }
}
