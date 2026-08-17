using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Modules.Payments.Application.Ports;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Domain.Entities;

namespace Modules.Payments.Infrastructure.Repositories;

public class TenantPaymentConfigRepository : ITenantPaymentConfigRepository
{
    private readonly PaymentsDbContext _context;

    public TenantPaymentConfigRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<TenantPaymentConfiguration?> GetByTenantAndGatewayAsync(Guid tenantId, string gatewayType, CancellationToken ct = default)
    {
        return await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters() // Bypass tenant isolation so the creator can read the system's public keys
            .FirstOrDefaultAsync(c => c.OrganizationId == tenantId && c.GatewayType == gatewayType.ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<TenantPaymentConfiguration>> GetAllByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.OrganizationId == tenantId)
            .ToListAsync(ct);
    }
}

public class PaymentWebhookLogRepository : IPaymentWebhookLogRepository
{
    private readonly PaymentsDbContext _context;

    public PaymentWebhookLogRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentWebhookLog?> GetByEventIdAsync(string eventId, string provider, Guid organizationId, CancellationToken ct = default)
    {
        return await _context.PaymentWebhookLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.EventId == eventId && l.Provider == provider && l.OrganizationId == organizationId,
                ct);
    }

    public async Task<PaymentWebhookLog?> GetByBusinessKeyAsync(string businessKey, string provider, Guid organizationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(businessKey))
        {
            return null;
        }

        return await _context.PaymentWebhookLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.BusinessKey == businessKey && l.Provider == provider && l.OrganizationId == organizationId,
                ct);
    }

    public async Task<OutboxRequeueResult> TryRequeueDeadOutboxAsync(Guid outboxId, CancellationToken ct = default)
    {
        var message = await _context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == outboxId, ct);
        if (message is null)
        {
            return OutboxRequeueResult.Missing;
        }

        if (!string.Equals(message.Status, MessageProcessingStatus.Dead, StringComparison.Ordinal))
        {
            return OutboxRequeueResult.AlreadyActive;
        }

        message.Status = MessageProcessingStatus.Pending;
        message.ProcessedAt = null;
        message.NextAttemptAt = null;
        message.AttemptCount = 0;
        await _context.SaveChangesAsync(ct);
        return OutboxRequeueResult.Requeued;
    }

    public void Add(PaymentWebhookLog log)
    {
        _context.PaymentWebhookLogs.Add(log);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
