using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class GatewayRefundCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;

    public GatewayRefundCompletedIntegrationEventHandler(CommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var existingLog = await _dbContext.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.OrganizationId == @event.OrganizationId 
                && l.ExternalReference == @event.PaymentRecordId.ToString());

        if (existingLog != null)
        {
            existingLog.TransitionToRefunded();
            await _dbContext.SaveChangesAsync();
        }
    }
}
