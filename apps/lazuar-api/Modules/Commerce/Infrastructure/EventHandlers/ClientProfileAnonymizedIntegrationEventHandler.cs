using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.EventHandlers;

/// <summary>
/// GDPR fan-out: cancel active commerce subscriptions for an anonymized client profile.
/// </summary>
public class ClientProfileAnonymizedIntegrationEventHandler : IIntegrationEventHandler<ClientProfileAnonymizedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ClientProfileAnonymizedIntegrationEventHandler> _logger;

    public ClientProfileAnonymizedIntegrationEventHandler(
        CommerceDbContext dbContext,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ILogger<ClientProfileAnonymizedIntegrationEventHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(ClientProfileAnonymizedIntegrationEvent @event)
    {
        var subscriptions = await _dbContext.Subscriptions
            .IgnoreQueryFilters()
            .Where(s =>
                s.OrganizationId == @event.OrganizationId
                && s.ClientProfileId == @event.ClientProfileId
                && s.Status != "CANCELED")
            .ToListAsync();

        if (subscriptions.Count == 0)
        {
            _logger.LogInformation(
                "ClientProfileAnonymized: no cancellable subscriptions for profile {ProfileId} org {OrgId}.",
                @event.ClientProfileId, @event.OrganizationId);
            return;
        }

        var productIds = subscriptions.Select(s => s.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var subscription in subscriptions)
        {
            if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED" or "TRIALING" or "PENDING"))
            {
                // Still cancel any non-terminal status we do not model strictly.
                if (subscription.Status == "CANCELED") continue;
            }

            subscription.Cancel();

            products.TryGetValue(subscription.ProductId, out var product);
            var fulfillmentTargets = product?.FulfillmentTargets.ToList() ?? [];

            await _eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                fulfillmentTargets));

            _logger.LogInformation(
                "ClientProfileAnonymized: canceled subscription {SubscriptionId} for profile {ProfileId}.",
                subscription.Id, @event.ClientProfileId);
        }

        await _dbContext.SaveChangesAsync();
    }
}
