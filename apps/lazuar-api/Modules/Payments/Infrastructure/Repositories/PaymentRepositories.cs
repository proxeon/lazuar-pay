using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<bool> HasBeenProcessedAsync(string eventId, string provider, CancellationToken ct = default)
    {
        return await _context.PaymentWebhookLogs
            .IgnoreQueryFilters() // Webhooks hit without a logged-in user context
            .AnyAsync(l => l.EventId == eventId && l.Provider == provider, ct);
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
