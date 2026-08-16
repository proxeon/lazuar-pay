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
        // Payment logs store GatewayTransactionId in ExternalReference (not PaymentRecordId).
        var paymentRecordId = @event.PaymentRecordId.ToString();
        var existingLog = await _dbContext.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.OrganizationId == @event.OrganizationId
                && (l.ExternalReference == @event.GatewayTransactionId
                    || l.ExternalReference == paymentRecordId
                    || l.Id == @event.PaymentRecordId));

        if (existingLog == null)
        {
            return;
        }

        // Pending is the apply lock. Mark-refunded already applied; redelivery is a no-op.
        if (!string.Equals(existingLog.Status, CommerceTransactionLog.StatusRefundPending, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        existingLog.ApplyRefund(@event.RefundedAmount);
        await _dbContext.SaveChangesAsync();
    }
}
