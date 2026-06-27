using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Vault.Infrastructure.EventHandlers;

public class FulfillmentRequestedIntegrationEventHandler : IIntegrationEventHandler<FulfillmentRequestedIntegrationEvent>
{
    private readonly VaultDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEventBus _eventBus;

    public FulfillmentRequestedIntegrationEventHandler(
        VaultDbContext dbContext,
        ICrmQueryService crmQueryService,
        [FromKeyedServices("VaultEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(FulfillmentRequestedIntegrationEvent @event)
    {
        if (!@event.InternalTargetApp.Equals("VAULT", StringComparison.OrdinalIgnoreCase))
            return;

        var productId = Guid.Parse(@event.Payload.GetProperty("product_id").GetString()!);
        var status = @event.Payload.GetProperty("status").GetString()!;

        if (status != "COMPLETED" && status != "ACTIVE")
            return;

        var asset = await _dbContext.VaultAssets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ProductId == productId);

        if (asset == null) return;

        var clientProfileId = Guid.Parse(@event.Payload.GetProperty("client_profile_id").GetString()!);
        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);

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
