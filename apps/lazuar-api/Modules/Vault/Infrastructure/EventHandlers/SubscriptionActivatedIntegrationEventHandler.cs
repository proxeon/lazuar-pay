using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Vault.Infrastructure.EventHandlers;

public class SubscriptionActivatedIntegrationEventHandler : IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>
{
    private readonly VaultDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEventBus _eventBus;

    public SubscriptionActivatedIntegrationEventHandler(
        VaultDbContext dbContext,
        ICrmQueryService crmQueryService,
        [FromKeyedServices("VaultEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionActivatedIntegrationEvent @event)
    {
        if (!@event.IsFirstPayment) return;

        var assets = await _dbContext.VaultAssets
            .IgnoreQueryFilters()
            .Where(a => a.OrganizationId == @event.OrganizationId)
            .ToListAsync();

        var asset = assets.FirstOrDefault(a => a.ProductIds.Contains(@event.ProductId));

        if (asset == null) return;

        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);

        if (profile != null && !string.IsNullOrEmpty(profile.Email))
        {
            var subject = $"Your download is ready: {asset.Name}";
            var body = $@"Hi {profile.Full_name},

Thank you for your purchase! You can access your file securely using the link below:

[Download File]({asset.CloudflareR2Url})

— Lazuar Platform";

            await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                @event.OrganizationId,
                profile.Email,
                profile.Phone,
                subject,
                MarkdownParser.ToHtml(body),
                null,
                "EMAIL"
            ));
        }
    }
}
