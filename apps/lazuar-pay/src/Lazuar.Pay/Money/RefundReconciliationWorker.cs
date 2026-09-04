using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

/// <summary>
/// Reconciles `pending` refunds that have been stuck past a threshold.
/// Issue 001 (issues/001): ambiguous processor outcomes hold the reservation
/// pending as the ops marker — this worker surfaces them so they don't sit
/// silently. It logs a warning per stale row and a summary count.
/// It does NOT auto-retry: the correct action (re-reserve, reverse, escalate)
/// depends on the processor's answer, which only a human or a future
/// reconciliation API can determine.
/// </summary>
internal sealed class RefundReconciliationWorker(
    IServiceScopeFactory scopes,
    ILogger<RefundReconciliationWorker> logger) : BackgroundService
{
    /// Rows pending longer than this are surfaced (default 30 minutes).
    static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
                var cutoff = DateTimeOffset.UtcNow.Subtract(StaleThreshold);

                var stale = await db.Refunds
                    .Where(r => r.Status == "pending" && r.CreatedAt < cutoff)
                    .Select(r => new { r.Id, r.OrgId, r.CheckoutId, r.Amount, r.Currency, r.CreatedAt })
                    .ToListAsync(stoppingToken);

                if (stale.Count > 0)
                {
                    foreach (var row in stale)
                    {
                        logger.LogWarning(
                            "stale pending refund: id={RefundId} org={OrgId} checkout={CheckoutId} " +
                            "amount={Amount} {Currency} created={CreatedAt}",
                            row.Id, row.OrgId, row.CheckoutId, row.Amount, row.Currency, row.CreatedAt);
                    }
                    logger.LogWarning("{Count} pending refund(s) older than {Threshold} — reconciliation required",
                        stale.Count, StaleThreshold);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "refund reconciliation pass failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
