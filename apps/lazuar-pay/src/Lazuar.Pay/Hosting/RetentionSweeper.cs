using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Hosting;

/// <summary>
/// plans/031/03: the append-only webhook/audit tables grow forever — psp_webhook_events in
/// particular takes one row per delivered PSP event and its composite key is scanned by the
/// dedupe lookup on every inbound webhook. This sweep prunes the four non-ledger,
/// append-only tables in small batched deletes (one statement per batch, oldest first).
///
/// Never a purge candidate: charges, refunds, journal entries/lines, documents, document
/// sequences, payers, subscriptions, checkouts, payment links, org settings, gateway
/// credentials, webhook endpoints, and idempotency keys — the ledger and its replay
/// protection are kept for the life of the system.
///
/// The dedupe-vs-retention tradeoff is deliberate: deleting an old psp_webhook_events row
/// means a redelivery of that exact event id could be re-processed instead of answered as
/// a duplicate. That is safe because the default window (90 days) is ~30x Stripe's retry
/// horizon, and fulfillment stays idempotent without the dedupe row — the CAS off "open",
/// the unique charges.CheckoutId index, and the filtered late_pay refund index all hold.
/// </summary>
public sealed class RetentionSweeper(PayDbContext db, IConfiguration config)
{
    public const int DefaultBatch = 10_000;

    /// <summary>Runs every table's sweep; returns the total rows deleted.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            // The InMemory provider has no SQL engine; the worker is not registered in
            // Testing anyway (same rule as the other hosted services).
            return 0;
        }

        var total = 0;
        total += await SweepPspWebhookEventsAsync(ct);
        total += await SweepOneWebhookEventsAsync(ct);
        total += await SweepOrgWebhookDeliveriesAsync(ct);
        total += await SweepAuditEventsAsync(ct);
        return total;
    }

    // Per-table retention days. A value of 0 (or negative) disables that table's sweep —
    // the ops escape hatch if a receiver needs longer history than the default.

    private async Task<int> SweepPspWebhookEventsAsync(CancellationToken ct)
    {
        var days = Days("PspWebhookEvents", 90);
        if (days <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var batch = BatchSize();
        var deleted = 0;
        while (true)
        {
            // Composite PK: delete by row-value equality over the key columns picked
            // oldest-first. Small transactions keep locks short on the hot dedupe index.
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.psp_webhook_events AS t
                USING (
                    SELECT "OrgId", "Provider", "EventId"
                    FROM public.psp_webhook_events
                    WHERE "ReceivedAt" < {cutoff}
                    ORDER BY "ReceivedAt"
                    LIMIT {batch}
                ) AS pick
                WHERE (t."OrgId", t."Provider", t."EventId") = (pick."OrgId", pick."Provider", pick."EventId")
                """, ct);
            deleted += n;
            if (n < batch)
            {
                return deleted;
            }
        }
    }

    private async Task<int> SweepOneWebhookEventsAsync(CancellationToken ct)
    {
        var days = Days("OneWebhookEvents", 90);
        if (days <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var batch = BatchSize();
        var deleted = 0;
        while (true)
        {
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.one_webhook_events AS t
                USING (
                    SELECT "Id"
                    FROM public.one_webhook_events
                    WHERE "ReceivedAt" < {cutoff}
                    ORDER BY "ReceivedAt"
                    LIMIT {batch}
                ) AS pick
                WHERE t."Id" = pick."Id"
                """, ct);
            deleted += n;
            if (n < batch)
            {
                return deleted;
            }
        }
    }

    private async Task<int> SweepOrgWebhookDeliveriesAsync(CancellationToken ct)
    {
        // Longer window than the dedupe tables: payloads are the only delivery history a
        // receiver can reconcile against, and 180 days also retires the undeliverable tail
        // (rows whose 5xx backoff otherwise retries every 5 minutes forever).
        var days = Days("OrgWebhookDeliveries", 180);
        if (days <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var batch = BatchSize();
        var deleted = 0;
        while (true)
        {
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.org_webhook_deliveries AS t
                USING (
                    SELECT "Id"
                    FROM public.org_webhook_deliveries
                    WHERE "CreatedAt" < {cutoff}
                    ORDER BY "CreatedAt"
                    LIMIT {batch}
                ) AS pick
                WHERE t."Id" = pick."Id"
                """, ct);
            deleted += n;
            if (n < batch)
            {
                return deleted;
            }
        }
    }

    private async Task<int> SweepAuditEventsAsync(CancellationToken ct)
    {
        // Thin rows and the closest thing to a compliance record in the system — the
        // longest window. Actor/payload enrichment is tracked separately (plans/031/05).
        var days = Days("AuditEvents", 730);
        if (days <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var batch = BatchSize();
        var deleted = 0;
        while (true)
        {
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.audit_events AS t
                USING (
                    SELECT "Id"
                    FROM public.audit_events
                    WHERE "At" < {cutoff}
                    ORDER BY "At"
                    LIMIT {batch}
                ) AS pick
                WHERE t."Id" = pick."Id"
                """, ct);
            deleted += n;
            if (n < batch)
            {
                return deleted;
            }
        }
    }

    private int Days(string key, int fallback) =>
        config.GetValue($"Pay:Retention:{key}Days", fallback);

    private int BatchSize() =>
        Math.Max(1, config.GetValue("Pay:Retention:BatchSize", DefaultBatch));
}

/// <summary>
/// Background loop for <see cref="RetentionSweeper"/>: first sweep shortly after boot
/// (post-migration), then once a day. Batched deletes are small transactions, so no
/// off-peak scheduler is warranted. Not registered in Testing.
/// </summary>
public sealed class RetentionWorker(IServiceScopeFactory scopes, ILogger<RetentionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var deleted = await scope.ServiceProvider
                    .GetRequiredService<RetentionSweeper>()
                    .SweepAsync(stoppingToken);
                if (deleted > 0)
                {
                    log.LogInformation("retention sweep removed {DeletedRows} expired rows", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "retention sweep failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
