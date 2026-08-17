using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Billing.Application;

/// <summary>
/// Optional unit-of-work for Billing. NSubstitute fakes omit this so handlers
/// fall back to unwrapped work. The real repository opens a DB transaction.
/// </summary>
public interface IBillingTransactional
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
