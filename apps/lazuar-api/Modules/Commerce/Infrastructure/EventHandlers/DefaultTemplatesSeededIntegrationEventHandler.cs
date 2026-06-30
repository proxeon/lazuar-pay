using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Contracts.Commands;
using Modules.Communications.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class DefaultTemplatesSeededIntegrationEventHandler : IIntegrationEventHandler<DefaultTemplatesSeededIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly IMediator _mediator;

    public DefaultTemplatesSeededIntegrationEventHandler(CommerceDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task HandleAsync(DefaultTemplatesSeededIntegrationEvent @event)
    {
        var hasCampaigns = await _dbContext.DunningCampaigns
            .IgnoreQueryFilters()
            .AnyAsync(c => c.OrganizationId == @event.TenantId);

        if (!hasCampaigns)
        {
            await _mediator.Send(new GenerateDefaultDunningCampaignsCommand(@event.TenantId));
        }
    }
}
