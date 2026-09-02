using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Money;
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaConfirm(PayDbContext db, SolanaRpc rpc, IFulfillPaid fulfillment, IConfiguration config, ProcessorRemote remote)
{
    public async Task<IResult> ConfirmAsync(CheckoutRow checkout, string signature, CancellationToken ct)
    {
        if (!PayProviders.IsSolana(checkout.Provider ?? ""))
        {
            return PayErrors.Status(400, "Bad Request", "provider mismatch");
        }

        if (string.IsNullOrWhiteSpace(signature) || !SolanaBase58.TryDecode(signature, out _))
        {
            return PayErrors.Status(400, "Bad Request", "signature is required");
        }

        var orgId = checkout.OrgId;
        if (await db.PspWebhookEvents.FindAsync([orgId, PayProviders.Solana, signature], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }

        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }

        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == PayProviders.Solana, ct);
        if (cred is null)
        {
            return PayErrors.Status(400, "Bad Request", "rail not configured");
        }

        var cluster = SolanaCluster.FromConfig(config);
        if (!SolanaCluster.MatchesVault(cluster, cred.Environment))
        {
            return PayErrors.Status(400, "Bad Request", "solana cluster mismatch");
        }

        JsonDocument doc;
        try
        {
            doc = await rpc.GetTransactionAsync(signature, ct);
        }
        catch (SolanaRpcThrottledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        using (doc)
        {
            var mismatch = SolanaTx.Validate(doc, checkout, cred, signature, cluster);
            if (mismatch is not null)
            {
                return PayErrors.Status(400, "Bad Request", mismatch);
            }
        }

        if (checkout.Status is "expired" or "failed")
        {
            await using var lateTx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow
                {
                    OrgId = orgId,
                    Provider = PayProviders.Solana,
                    EventId = signature,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
                await lateTx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await lateTx.RollbackAsync(ct);
                return Results.Ok(new { duplicate = true });
            }

            return Results.Json(new { refunded = false, reason = "late_pay_manual" }, OneClient.Json);
        }

        if (checkout.Status != "open")
        {
            return PayErrors.Status(409, "Conflict", "Checkout is not open");
        }

        FulfillOutcome outcome;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow
            {
                OrgId = orgId,
                Provider = PayProviders.Solana,
                EventId = signature,
                ReceivedAt = DateTimeOffset.UtcNow
            });
            outcome = await fulfillment.FulfillPaidAsync(checkout.Id, PayProviders.Solana, signature, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            // Same rule as the PSP webhook path: only "duplicate" when the winner really paid.
            db.ChangeTracker.Clear();
            var fresh = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == checkout.Id, ct);
            if (fresh?.Status == "paid")
            {
                return Results.Ok(new { duplicate = true });
            }

            return PayErrors.Status(500, "Internal Server Error", "fulfill conflict");
        }
        catch (ChargesPausedException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(500, "Internal Server Error", "fulfill failed");
        }

        // Over-capacity link child: the pending refund row was booked in the transaction.
        // Solana has no refund API, so this stays pending as the ops marker.
        if (outcome.PendingLateRefundId is not null)
        {
            await remote.SettlePendingRefundAsync(
                outcome.PendingLateRefundId, checkout, signature, outcome.LateRefundAmountMinor, ct);
        }

        return Results.Json(new { ok = true }, OneClient.Json);
    }

    public async Task ConfirmOpenByReferenceAsync(CancellationToken ct)
    {
        var ttl = PaymentLinkOccupancy.ReservationTtl(config);
        var cutoff = DateTimeOffset.UtcNow - ttl;
        var npgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
        while (true)
        {
            var open = await ClaimOpenAsync(npgsql, ct);
            if (open.Count == 0)
            {
                return;
            }

            foreach (var row in open)
            {
                if (row.CreatedAt < cutoff)
                {
                    await FailWatchTimeoutAsync(row, ct);
                    continue;
                }

                await ConfirmSignaturesAsync(row, ct);
            }

            if (!npgsql)
            {
                return;
            }
        }
    }

    async Task<List<CheckoutRow>> ClaimOpenAsync(bool npgsql, CancellationToken ct)
    {
        if (!npgsql)
        {
            return await db.Checkouts
                .Where(x => x.Provider == PayProviders.Solana && x.Status == "open" && x.PspRedirectUrl != null && x.ProviderSessionId != null)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);
        }

        var stamp = DateTimeOffset.UtcNow;
        var lease = stamp.AddSeconds(-2);
        var provider = PayProviders.Solana;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE public.checkouts AS c
            SET "WatchClaimedAt" = {stamp}
            FROM (
                SELECT "Id"
                FROM public.checkouts
                WHERE "Provider" = {provider}
                  AND "Status" = 'open'
                  AND "PspRedirectUrl" IS NOT NULL
                  AND "ProviderSessionId" IS NOT NULL
                  AND ("WatchClaimedAt" IS NULL OR "WatchClaimedAt" < {lease})
                ORDER BY "CreatedAt", "Id"
                LIMIT 50
                FOR UPDATE SKIP LOCKED
            ) AS pick
            WHERE c."Id" = pick."Id"
            """,
            ct);
        return await db.Checkouts
            .Where(x => x.WatchClaimedAt == stamp && x.Provider == PayProviders.Solana && x.Status == "open")
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    async Task ConfirmSignaturesAsync(CheckoutRow row, CancellationToken ct)
    {
        JsonDocument sigs;
        try
        {
            sigs = await rpc.GetSignaturesForAddressAsync(row.ProviderSessionId!, ct);
        }
        catch (SolanaRpcThrottledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        using (sigs)
        {
            if (!sigs.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in result.EnumerateArray())
            {
                var sig = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.TryGetProperty("signature", out var s) ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(sig))
                {
                    continue;
                }

                var outcome = await ConfirmAsync(row, sig, ct);
                if (outcome is not IStatusCodeHttpResult { StatusCode: 400 })
                {
                    return;
                }
            }
        }
    }

    public async Task FailWatchTimeoutAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (checkout.Status != "open")
        {
            return;
        }

        var linkChild = checkout.PaymentLinkId is not null;
        var eventId = linkChild ? "expired:" + checkout.Id : "watch_timeout:" + checkout.Id;
        if (await db.PspWebhookEvents.FindAsync([checkout.OrgId, PayProviders.Solana, eventId], ct) is not null)
        {
            return;
        }

        await using var failTx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow
            {
                OrgId = checkout.OrgId,
                Provider = PayProviders.Solana,
                EventId = eventId,
                ReceivedAt = DateTimeOffset.UtcNow
            });
            checkout.Status = linkChild ? "expired" : "failed";
            await OutboundWebhookEnqueue.TryAddAsync(
                db,
                checkout.OrgId,
                eventId,
                linkChild ? PayWebhookEnvelope.Expired : PayWebhookEnvelope.Failed,
                linkChild
                    ? (object)new { checkout_id = checkout.Id, payment_link_id = checkout.PaymentLinkId, reason = "watch_timeout" }
                    : (object)new
                    {
                        checkout_id = checkout.Id,
                        reason = "watch_timeout",
                        provider = PayProviders.Solana
                    },
                ct);
            await db.SaveChangesAsync(ct);
            await failTx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await failTx.RollbackAsync(ct);
        }
    }
}
