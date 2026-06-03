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

    public async Task<TenantPaymentConfiguration?> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.TenantPaymentConfigurations
            .FirstOrDefaultAsync(c => c.OrganizationId == tenantId && c.IsActive, ct);
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
