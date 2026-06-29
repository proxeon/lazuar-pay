using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Contracts.Events;

namespace Modules.Community.Infrastructure.EventHandlers;

public class SubscriptionSuspendedIntegrationEventHandler : IIntegrationEventHandler<SubscriptionSuspendedIntegrationEvent>
{
    private readonly CommunityDbContext _dbContext;

    public SubscriptionSuspendedIntegrationEventHandler(CommunityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(SubscriptionSuspendedIntegrationEvent @event)
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

        if (member != null)
        {
            member.UpdateStatus("SUSPENDED");
            await _dbContext.SaveChangesAsync();
        }
    }
}
