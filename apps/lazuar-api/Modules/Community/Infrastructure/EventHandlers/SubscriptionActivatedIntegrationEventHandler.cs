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

public class SubscriptionActivatedIntegrationEventHandler : IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>
{
    private readonly CommunityDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEventBus _eventBus;

    public SubscriptionActivatedIntegrationEventHandler(
        CommunityDbContext dbContext,
        ICrmQueryService crmQueryService,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionActivatedIntegrationEvent @event)
    {
        var spaces = await _dbContext.CommunitySpaces
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == @event.OrganizationId)
            .ToListAsync();

        var space = spaces.FirstOrDefault(s => s.ProductIds.Contains(@event.ProductId));

        if (space == null) return;

        var member = await _dbContext.CommunityMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.CommunitySpaceId == space.Id && m.ClientProfileId == @event.ClientProfileId);

        if (member == null)
        {
            member = new CommunityMember(@event.OrganizationId, space.Id, @event.ClientProfileId, "ACTIVE");
            _dbContext.CommunityMembers.Add(member);
        }
        else
        {
            member.UpdateStatus("ACTIVE");
        }

        await _dbContext.SaveChangesAsync();

        if (@event.IsFirstPayment)
        {
            var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
            
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
