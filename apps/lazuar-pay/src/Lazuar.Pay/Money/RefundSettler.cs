using Lazuar.Pay.Data;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

/// <summary>
/// plans/031/02: re-attempts stripe late_pay refund settlements that the webhook path
/// could not complete (lost response, PSP 5xx, crash between commit and settle). The claim
/// is a SKIP LOCKED lease bounded by <see cref="RefundSchedule"/>: stripe only (its refund
/// call is idempotency-key safe and documented — CHIP's refund docs promise no idempotency
/// semantics, so CHIP stays manual), <c>late_pay</c> only (merchant rows have journal/REF
/// side effects that only RefundEndpoints' settle transaction may write), and rows older
/// than the 24 h idempotency-key window are never claimed — they exit via the resolve
/// endpoint as manual reconciliations.
/// </summary>
public sealed class RefundSettler(PayDbContext db, ProcessorRemote remote)
{
    public const int MaxBatch = 20;

    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var npgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
        List<RefundRow> claimed;
        if (npgsql)
        {
            var leaseUntil = now.AddSeconds(60);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.refunds AS r
                SET "NextAttemptAt" = {leaseUntil}
                FROM (
                    SELECT "Id" FROM public.refunds
                    WHERE "Status" = 'pending' AND "Provider" = 'stripe' AND "Reason" = 'late_pay'
                      AND "AttemptCount" < {RefundSchedule.MaxAttempts}
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                      AND "CreatedAt" > {now.Subtract(RefundSchedule.ClaimWindow)}
                    ORDER BY "CreatedAt"
                    LIMIT {MaxBatch}
                    FOR UPDATE SKIP LOCKED
                ) AS pick
                WHERE r."Id" = pick."Id"
                """, ct);
            claimed = await db.Refunds
                .Where(x => x.Status == "pending" && x.Provider == PayProviders.Stripe
                    && x.Reason == "late_pay" && x.NextAttemptAt == leaseUntil)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
        }
        else
        {
            claimed = await db.Refunds
                .Where(x => x.Status == "pending" && x.Provider == PayProviders.Stripe
                    && x.Reason == "late_pay" && x.AttemptCount < RefundSchedule.MaxAttempts
                    && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
                    && x.CreatedAt > now.Subtract(RefundSchedule.ClaimWindow))
                .OrderBy(x => x.CreatedAt)
                .Take(MaxBatch)
                .ToListAsync(ct);
        }

        // Per-row persistence: whatever happened to this row survives even if the next
        // row throws (same rule as OutboundWebhookDispatch).
        foreach (var row in claimed)
        {
            await SettleOneAsync(row, ct);
            await db.SaveChangesAsync(ct);
        }

        var (pendingStripe, pendingManual, oldestSeconds) = await SnapshotAsync(ct);
        RefundMetrics.Publish(pendingStripe, pendingManual, oldestSeconds);
        return claimed.Count;
    }

    private async Task SettleOneAsync(RefundRow row, CancellationToken ct)
    {
        var checkout = await db.Checkouts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == row.CheckoutId, ct);
        if (checkout is null)
        {
            row.AttemptCount += 1;
            row.LastError = "checkout missing";
            row.NextAttemptAt = DateTimeOffset.UtcNow.Add(RefundSchedule.Backoff(row.AttemptCount));
            return;
        }

        // ToMinor(row.Amount) reproduces the original attempt's parameters exactly —
        // stripe success rows store FromMinor(AmountMinor), and Stripe rejects a reused
        // idempotency key with different params.
        var outcome = await remote.RefundLateAsync(
            checkout, row.ProviderRef, MoneyMath.ToMinor(row.Amount), row.Id, ct);
        row.AttemptCount += 1;
        switch (outcome)
        {
            case RefundLateOutcome.Settled:
                row.Status = "succeeded";
                row.NextAttemptAt = null;
                row.LastError = null;
                await OutboundWebhookEnqueue.TryAddAsync(
                    db,
                    row.OrgId,
                    row.Id,
                    PayWebhookEnvelope.RefundCreated,
                    new
                    {
                        refund_id = row.Id,
                        checkout_id = row.CheckoutId,
                        charge_id = row.ChargeId,
                        amount = row.Amount,
                        currency = row.Currency,
                        provider = row.Provider,
                        number = (string?)null
                    },
                    ct);
                break;
            case RefundLateOutcome.Rejected:
                // Definitive processor no: nothing moved, so failed is safe and final.
                row.Status = "failed";
                row.NextAttemptAt = null;
                break;
            default:
                row.LastError = "settle outcome unknown";
                row.NextAttemptAt = DateTimeOffset.UtcNow.Add(RefundSchedule.Backoff(row.AttemptCount));
                break;
        }
    }

    private async Task<(int Stripe, int Manual, double OldestSeconds)> SnapshotAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var pending = await db.Refunds.AsNoTracking()
            .Where(x => x.Status == "pending")
            .Select(x => new { x.Provider, x.CreatedAt })
            .ToListAsync(ct);
        var stripe = pending.Count(x => x.Provider == PayProviders.Stripe);
        var oldest = pending.Count == 0
            ? 0
            : now.Subtract(pending.Min(x => x.CreatedAt)).TotalSeconds;
        return (stripe, pending.Count - stripe, oldest);
    }
}

/// <summary>
/// Background loop for <see cref="RefundSettler"/>. Fast after productive batches so a
/// backlog drains, 15 s otherwise; every tick also refreshes the pending-refund metrics.
/// </summary>
public sealed class RefundSettleWorker(IServiceScopeFactory scopes, ILogger<RefundSettleWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(15);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processed = await scope.ServiceProvider
                    .GetRequiredService<RefundSettler>()
                    .ProcessBatchAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(processed > 0 ? 2 : 15);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "refund settle worker");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
