using System.Threading;
using System.Threading.Tasks;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Application;

public interface ILedgerRepository
{
    Task<bool> HasEntryBeenProcessedAsync(string referenceType, string referenceId, CancellationToken ct = default);
    void Add(LedgerEntry entry);
    void AddDeferredRevenue(DeferredRevenueSchedule schedule);
    Task SaveChangesAsync(CancellationToken ct = default);
}
