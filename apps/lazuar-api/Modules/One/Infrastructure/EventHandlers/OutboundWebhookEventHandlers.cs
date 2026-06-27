// apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs
using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Payments.Contracts.Events;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.EventHandlers;

public class OutboundWebhookEventHandlers : 
    IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly OneDbContext _dbContext;

    public OutboundWebhookEventHandlers(OneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        await ProcessEventAsync(@event.OrganizationId, "payment.completed", @event);
    }

    private async Task ProcessEventAsync(Guid organizationId, string eventType, object payload)
    {
        var endpoint = await _dbContext.TenantWebhookEndpoints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.IsActive);

        if (endpoint == null) return;

        var jsonPayload = JsonSerializer.Serialize(new
        {
            id = Guid.CreateVersion7().ToString(),
            event_type = eventType,
            created_at = DateTime.UtcNow,
            data = payload
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        var outbox = new WebhookDeliveryOutbox(organizationId, endpoint.Id, eventType, jsonPayload);
        _dbContext.WebhookDeliveryOutboxes.Add(outbox);
        await _dbContext.SaveChangesAsync();
    }
}
