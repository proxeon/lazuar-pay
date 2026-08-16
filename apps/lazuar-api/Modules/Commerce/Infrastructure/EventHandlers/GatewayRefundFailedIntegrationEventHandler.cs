using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class GatewayRefundFailedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundFailedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;

    public GatewayRefundFailedIntegrationEventHandler(CommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayRefundFailedIntegrationEvent @event)
    {
        var log = await _dbContext.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.OrganizationId == @event.OrganizationId && l.Id == @event.PaymentRecordId);

        if (log == null)
        {
            return;
        }

        if (!string.Equals(log.Status, CommerceTransactionLog.StatusRefundPending, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        log.MarkRefundFailed();
        await _dbContext.SaveChangesAsync();
    }
}
