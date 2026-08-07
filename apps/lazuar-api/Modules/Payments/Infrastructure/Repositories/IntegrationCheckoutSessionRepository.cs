using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Payments.Application.Ports;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Infrastructure.Repositories;

public class IntegrationCheckoutSessionRepository : IIntegrationCheckoutSessionRepository
{
    private readonly PaymentsDbContext _context;

    public IntegrationCheckoutSessionRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<IntegrationCheckoutSession?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken ct = default)
    {
        return await _context.IntegrationCheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);
    }

    public async Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
        Guid organizationId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        return await _context.IntegrationCheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.OrganizationId == organizationId && s.IdempotencyKey == idempotencyKey,
                ct);
    }

    public void Add(IntegrationCheckoutSession session)
    {
        _context.IntegrationCheckoutSessions.Add(session);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
