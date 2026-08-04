using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.Repositories;

public class LedgerRepository : ILedgerRepository
{
    private readonly BillingDbContext _context;

    public LedgerRepository(BillingDbContext context)
    {
        _context = context;
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
