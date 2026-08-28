using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Money;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaConfirm(PayDbContext db, SolanaRpc rpc, IFulfillPaid fulfillment)
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

        System.Text.Json.JsonDocument doc;
        try
        {
            doc = await rpc.GetTransactionAsync(signature, ct);
        }
        catch (InvalidOperationException ex)
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        using (doc)
        {
            var mismatch = SolanaTx.Validate(doc, checkout, cred, signature);
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
            await fulfillment.FulfillPaidAsync(checkout.Id, PayProviders.Solana, signature, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Results.Ok(new { duplicate = true });
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

        return Results.Json(new { ok = true }, OneClient.Json);
    }

    public async Task ConfirmOpenByReferenceAsync(CancellationToken ct)
    {
        var open = await db.Checkouts
            .Where(x => x.Provider == PayProviders.Solana && x.Status == "open" && x.SolanaPayUrl != null && x.ProviderSessionId != null)
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        foreach (var row in open)
        {
            System.Text.Json.JsonDocument sigs;
            try
            {
                sigs = await rpc.GetSignaturesForAddressAsync(row.ProviderSessionId!, ct);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            using (sigs)
            {
                if (!sigs.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                {
                    continue;
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

                    await ConfirmAsync(row, sig, ct);
                    break;
                }
            }
        }
    }
}
