using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.One.Domain;
using Modules.Commerce.Contracts.Events;

namespace Modules.One.Infrastructure.EventHandlers;

public class OutboundWebhookEventHandlers : 
    IIntegrationEventHandler<OutboundWebhookRequestedIntegrationEvent>
{
    private readonly OneDbContext _dbContext;

    public OutboundWebhookEventHandlers(OneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(OutboundWebhookRequestedIntegrationEvent @event)
    {
        var endpoint = await _dbContext.TenantWebhookEndpoints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == @event.OrganizationId && e.Url == @event.TargetUrl && e.IsActive);

        if (endpoint == null) return;

        var jsonPayload = JsonSerializer.Serialize(new
        {
            id = Guid.CreateVersion7().ToString(),
            event_type = @event.EventType,
            created_at = DateTime.UtcNow,
            data = @event.Payload
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        var outbox = new WebhookDeliveryOutbox(@event.OrganizationId, endpoint.Id, @event.EventType, jsonPayload);
        _dbContext.WebhookDeliveryOutboxes.Add(outbox);
        await _dbContext.SaveChangesAsync();
    }
}
