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

        if (existingLog.RemainingAmount <= 0)
        {
            return;
        }

        var pending = string.Equals(
            existingLog.Status, CommerceTransactionLog.StatusRefundPending, StringComparison.OrdinalIgnoreCase);
        var open = string.Equals(existingLog.Status, CommerceTransactionLog.StatusConfirmed, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(existingLog.Status, CommerceTransactionLog.StatusRefundFailed, StringComparison.OrdinalIgnoreCase);
        var inboundSlice = !string.IsNullOrWhiteSpace(@event.RefundId)
                           && string.Equals(
                               existingLog.Status,
                               CommerceTransactionLog.StatusPartiallyRefunded,
                               StringComparison.OrdinalIgnoreCase);

        // Ops path: apply only while REFUND_PENDING so outbox redelivery does not double-add.
        // Dashboard / Radar inbound: log is still CONFIRMED (or a later PARTIAL slice).
        if (!pending && !open && !inboundSlice)
        {
            return;
        }

        var amount = existingLog.RemainingAmount < @event.RefundedAmount
            ? existingLog.RemainingAmount
            : @event.RefundedAmount;
        existingLog.ApplyRefund(amount);
        await _dbContext.SaveChangesAsync();
    }
}
