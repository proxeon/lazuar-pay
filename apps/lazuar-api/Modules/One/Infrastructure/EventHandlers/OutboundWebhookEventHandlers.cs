using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.EventHandlers;

public class OutboundWebhookEventHandlers :
    IIntegrationEventHandler<OutboundWebhookRequestedIntegrationEvent>
{
    private readonly OneDbContext _dbContext;
    private readonly ILogger<OutboundWebhookEventHandlers> _logger;

    public OutboundWebhookEventHandlers(
        OneDbContext dbContext,
        ILogger<OutboundWebhookEventHandlers> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(OutboundWebhookRequestedIntegrationEvent @event)
    {
        // Fan-out to ALL active workspace endpoints. No product-URL equality gate.
        var endpoints = await _dbContext.TenantWebhookEndpoints
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == @event.OrganizationId && e.IsActive)
            .ToListAsync();

        if (endpoints.Count == 0)
        {
            _logger.LogInformation(
                "Outbound webhook {EventType} for org {OrganizationId}: no active endpoints configured; skipping delivery.",
                @event.EventType,
                @event.OrganizationId);
            return;
        }

        var matching = endpoints.Where(e => e.AcceptsEvent(@event.EventType)).ToList();
        if (matching.Count == 0)
        {
            _logger.LogInformation(
                "Outbound webhook {EventType} for org {OrganizationId}: {EndpointCount} active endpoint(s) but none subscribe to this event; skipping.",
                @event.EventType,
                @event.OrganizationId,
                endpoints.Count);
            return;
        }

        var jsonPayload = JsonSerializer.Serialize(new
        {
            id = Guid.CreateVersion7().ToString(),
            event_type = @event.EventType,
            created_at = DateTime.UtcNow,
            data = @event.Payload
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        foreach (var endpoint in matching)
        {
            var outbox = new WebhookDeliveryOutbox(
                @event.OrganizationId,
                endpoint.Id,
                @event.EventType,
                jsonPayload);
            _dbContext.WebhookDeliveryOutboxes.Add(outbox);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Enqueued outbound webhook {EventType} for org {OrganizationId} to {Count} endpoint(s).",
            @event.EventType,
            @event.OrganizationId,
            matching.Count);
    }
}
