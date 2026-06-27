using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.Community.Domain.Entities;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Community.Infrastructure.EventHandlers;

public class FulfillmentRequestedIntegrationEventHandler : IIntegrationEventHandler<FulfillmentRequestedIntegrationEvent>
{
    private readonly CommunityDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEventBus _eventBus;

    public FulfillmentRequestedIntegrationEventHandler(
        CommunityDbContext dbContext,
        ICrmQueryService crmQueryService,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(FulfillmentRequestedIntegrationEvent @event)
    {
        if (!@event.InternalTargetApp.Equals("COMMUNITY", StringComparison.OrdinalIgnoreCase))
            return;

        var productId = Guid.Parse(@event.Payload.GetProperty("product_id").GetString()!);
        var clientProfileId = Guid.Parse(@event.Payload.GetProperty("client_profile_id").GetString()!);
        var status = @event.Payload.GetProperty("status").GetString()!;

        var space = await _dbContext.CommunitySpaces
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ProductId == productId);

        if (space == null) return;

        var member = await _dbContext.CommunityMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.CommunitySpaceId == space.Id && m.ClientProfileId == clientProfileId);

        var isActiveStatus = status == "ACTIVE" || status == "COMPLETED";

        if (member == null)
        {
            member = new CommunityMember(@event.OrganizationId, space.Id, clientProfileId, isActiveStatus ? "ACTIVE" : "REVOKED");
            _dbContext.CommunityMembers.Add(member);
        }
        else
        {
            member.UpdateStatus(isActiveStatus ? "ACTIVE" : "REVOKED");
        }

        await _dbContext.SaveChangesAsync();

        if (@event.EventType == "subscription.activated" || @event.EventType == "order.completed")
        {
            if (@event.Payload.TryGetProperty("is_first_payment", out var isFirstElement) && isFirstElement.GetBoolean())
            {
                var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
                if (profile != null && !string.IsNullOrEmpty(profile.Email))
                {
                    var subject = $"Welcome to {space.Name}!";
                    var body = $@"Hi {profile.Full_name},

You're in! Your payment was successful, and your access is officially active.

Here is everything you need to get started:
1. **Join the Community:** Meet everyone and say hi!
[Join the Telegram Group]({space.TelegramLink})

2. **Weekly Sessions:** Bookmark our live room.
[Save the Zoom Link]({space.ZoomLink})

See you inside,
Lazuar Platform";

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
    }
}
