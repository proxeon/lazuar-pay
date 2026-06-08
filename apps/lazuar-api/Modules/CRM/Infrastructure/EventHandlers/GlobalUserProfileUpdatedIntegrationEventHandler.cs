using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.One.Contracts;

namespace Modules.CRM.Infrastructure.EventHandlers;

public class GlobalUserProfileUpdatedIntegrationEventHandler : IIntegrationEventHandler<GlobalUserProfileUpdatedIntegrationEvent>
{
    private readonly CrmDbContext _dbContext;

    public GlobalUserProfileUpdatedIntegrationEventHandler(CrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GlobalUserProfileUpdatedIntegrationEvent @event)
    {
        var profiles = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .Where(p => p.GlobalUserId == @event.UserId)
            .ToListAsync();

        if (!profiles.Any()) return;

        foreach (var profile in profiles)
        {
            profile.FullName = @event.Name;
            profile.Email = @event.Email;
        }

        await _dbContext.SaveChangesAsync();
    }
}
