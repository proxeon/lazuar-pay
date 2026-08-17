using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.Repositories;

public class LedgerRepository : ILedgerRepository, IBillingTransactional
{
    private readonly BillingDbContext _context;

    public LedgerRepository(BillingDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational())
        {
            await action(ct);
            return;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> HasEntryBeenProcessedAsync(string referenceType, string referenceId, CancellationToken ct = default)
    {
        // Workers/event handlers run with empty ambient TenantId (fail-closed filter).
        return await _context.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId, ct);
    }

    public void Add(LedgerEntry entry)
    {
        _context.LedgerEntries.Add(entry);
    }

    public void AddDeferredRevenue(DeferredRevenueSchedule schedule)
    {
        _context.DeferredRevenueSchedules.Add(schedule);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
