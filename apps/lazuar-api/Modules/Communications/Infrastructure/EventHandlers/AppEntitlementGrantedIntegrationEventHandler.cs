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

        var hasTemplates = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == @event.TenantId);

        if (!hasTemplates)
        {
            var templates = DefaultMessageTemplates.CreateAllForTenant(@event.TenantId).ToList();
            _dbContext.MessageTemplates.AddRange(templates);
            await _eventBus.PublishAsync(new DefaultTemplatesSeededIntegrationEvent(@event.TenantId));
            await _dbContext.SaveChangesAsync();
        }
    }
}
