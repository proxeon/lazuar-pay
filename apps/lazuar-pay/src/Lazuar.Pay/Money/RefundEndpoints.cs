using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

internal static class RefundEndpoints
{
    public static void MapRefunds(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId}/refunds", Create);
        app.MapGet("/v1/orgs/{orgId}/refunds", List);
    }

    static async Task<IResult> Create(
        string orgId,
        CreateRefundRequest? body,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        ProcessorRemote remote,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var checkoutId = body?.CheckoutId?.Trim();
        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            return PayErrors.Status(400, "Bad Request", "checkout_id is required");
        }

        var idempotency = request.Headers["Idempotency-Key"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotency))
        {
            idempotency = body?.IdempotencyKey?.Trim() ?? "";
        }

        if (!string.IsNullOrWhiteSpace(idempotency))
        {
            var existing = await db.Refunds.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.IdempotencyKey == idempotency, ct);
            if (existing is not null)
            {
                if (existing.CheckoutId != checkoutId
                    || (body?.Amount is decimal amt && amt != existing.Amount))
                {
                    return PayErrors.Status(409, "Conflict", "idempotency key reused with a different body");
                }

                return Results.Json(View(existing), OneClient.Json);
            }
        }

        var charge = await db.Charges.FirstOrDefaultAsync(x => x.OrgId == orgId && x.CheckoutId == checkoutId, ct);
        if (charge is null)
        {
            return PayErrors.Status(404, "Not Found", "charge not found");
        }

        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null || checkout.OrgId != orgId)
        {
            return PayErrors.Status(404, "Not Found", "checkout not found");
        }

        // Issue 001: the refund id doubles as the processor idempotency key. When the caller
        // supplies an Idempotency-Key we derive it deterministically from (org, key), so a
        // retry of the same logical refund reuses the original processor key instead of
        // minting a fresh one — a fresh key was how a retry after a lost response could
        // refund the same money twice at the processor.
        var refundId = string.IsNullOrWhiteSpace(idempotency)
            ? Guid.NewGuid().ToString("N")
            : StableRefundId(orgId, idempotency);
        decimal remaining;
        decimal amount;
        RefundRow row;

        // Reserve before money moves. The pending row counts against the refundable remainder
        // and the charge row is locked, so two concurrent writers (same or different
        // idempotency keys) cannot both pass the check across replicas.
        var npgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
        await using (var reserveTx = await db.Database.BeginTransactionAsync(ct))
        {
            if (npgsql)
            {
                // ToListAsync + Single(): SingleAsync would compose over non-composable FOR UPDATE SQL.
                charge = (await db.Charges.FromSqlInterpolated(
                    $"SELECT * FROM public.charges WHERE \"Id\" = {charge.Id} FOR UPDATE").ToListAsync(ct)).Single();
            }

            if (charge.Status is "refunded")
            {
                return await ReplayOrAsync(db, orgId, checkoutId, idempotency, body?.Amount,
                    PayErrors.Status(409, "Conflict", "already refunded"), ct);
            }

            var reserved = await db.Refunds
                .Where(x => x.ChargeId == charge.Id && (x.Status == "succeeded" || x.Status == "pending"))
                .SumAsync(x => x.Amount, ct);
            remaining = charge.Amount - reserved;
            if (remaining <= 0)
            {
                return await ReplayOrAsync(db, orgId, checkoutId, idempotency, body?.Amount,
                    PayErrors.Status(409, "Conflict", "already refunded"), ct);
            }

            amount = body?.Amount ?? remaining;
            if (amount <= 0 || amount > remaining)
            {
                return PayErrors.Status(400, "Bad Request", "amount must be within the refundable remainder");
            }

            row = new RefundRow
            {
                Id = refundId,
                OrgId = orgId,
                CheckoutId = checkoutId,
                ChargeId = charge.Id,
                Amount = amount,
                Currency = charge.Currency,
                Status = "pending",
                Provider = charge.Provider,
                ProviderRef = charge.ProviderRef,
                Reason = "merchant",
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotency) ? null : idempotency,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Refunds.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotency))
            {
                // Issue 012: two concurrent partial refunds with the same Idempotency-Key both
                // pass the read-side pre-check; the loser hits the filtered unique index here.
                // That loser is a replay by contract — roll back, drop the tracked duplicate,
                // and answer with the winner's row instead of a raw 500.
                await reserveTx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return await ReplayOrAsync(db, orgId, checkoutId, idempotency, body?.Amount,
                    PayErrors.Status(500, "Internal Server Error", "refund reservation conflict"), ct);
            }

            await reserveTx.CommitAsync(ct);
        }

        try
        {
            await remote.RefundChargeAsync(charge, checkout, amount, refundId, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Unsupported/unconfigured rail: nothing could have moved at the processor, so
            // releasing the reservation (failed) is safe.
            row.Status = "failed";
            await db.SaveChangesAsync(ct);
            return PayErrors.Status(400, "Bad Request", ex.Message);
        }
        catch (ProcessorRejectedException)
        {
            // Definitive processor no (<500 response): no money moved, so the reservation can
            // be released safely.
            row.Status = "failed";
            await db.SaveChangesAsync(ct);
            return PayErrors.Status(502, "Bad Gateway", "processor rejected the refund");
        }
        catch (Exception)
        {
            // Issue 001: ambiguous outcome — the response was lost after the processor may
            // have executed the refund (timeout, connection reset, PSP 5xx). Booking "failed"
            // here used to release the refundable remainder, and the retry's fresh processor
            // idempotency key let the processor execute it again — a single intended refund
            // moving money twice. The row stays pending instead: capacity remains reserved,
            // same-key retries replay this row, and ops reconcile against the processor
            // before releasing it.
            return PayErrors.Status(502, "Bad Gateway", "refund outcome unknown — held pending for reconciliation");
        }

        // Settle inside a fresh transaction that re-locks the charge and recomputes the
        // refund total from persisted rows (issue 010). The reserve-time `remaining` snapshot
        // is stale by now — a concurrent partial refund may have committed while we were at
        // the processor — and the old unlocked last-writer-wins status write could permanently
        // mislabel a fully refunded charge as "partially_refunded".
        string number;
        await using (var settleTx = await db.Database.BeginTransactionAsync(ct))
        {
            if (npgsql)
            {
                charge = (await db.Charges.FromSqlInterpolated(
                    $"SELECT * FROM public.charges WHERE \"Id\" = {charge.Id} FOR UPDATE").ToListAsync(ct)).Single();
            }

            // Pending rows count as refunded for status purposes, mirroring the reservation
            // semantics: a pending row reserves capacity, so the charge must not read as more
            // refundable than it is.
            var refundedTotal = await db.Refunds
                .Where(x => x.ChargeId == charge.Id && (x.Status == "succeeded" || x.Status == "pending"))
                .SumAsync(x => x.Amount, ct);
            charge.Status = refundedTotal >= charge.Amount ? "refunded" : "partially_refunded";
            row.Status = "succeeded";

            var entryId = Guid.NewGuid().ToString("N");
            db.JournalEntries.Add(new JournalEntryRow
            {
                Id = entryId,
                OrgId = orgId,
                CheckoutId = checkoutId,
                Currency = charge.Currency,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.JournalLines.Add(new JournalLineRow
            {
                Id = Guid.NewGuid().ToString("N"),
                EntryId = entryId,
                Account = "revenue",
                Dc = "D",
                Amount = amount
            });
            db.JournalLines.Add(new JournalLineRow
            {
                Id = Guid.NewGuid().ToString("N"),
                EntryId = entryId,
                Account = "cash",
                Dc = "C",
                Amount = amount
            });

            var year = MalaysiaTime.Year(DateTimeOffset.UtcNow);
            number = await DocumentNumbers.AllocateAsync(db, orgId, "REF", year, ct);
            db.Documents.Add(new DocumentRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = orgId,
                CheckoutId = checkoutId,
                Number = number,
                Title = "Refund",
                CreatedAt = DateTimeOffset.UtcNow
            });

            // row was inserted pending in the reservation transaction above; it flips to
            // succeeded only now that the processor accepted the refund.
            db.AuditEvents.Add(new AuditEventRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = orgId,
                Action = "refund.created",
                At = DateTimeOffset.UtcNow
            });
            await OutboundWebhookEnqueue.TryAddAsync(
                db,
                orgId,
                refundId,
                PayWebhookEnvelope.RefundCreated,
                new
                {
                    refund_id = refundId,
                    checkout_id = checkoutId,
                    charge_id = charge.Id,
                    amount,
                    currency = charge.Currency,
                    number,
                    provider = charge.Provider
                },
                ct);
            await db.SaveChangesAsync(ct);
            await settleTx.CommitAsync(ct);
        }

        return Results.Json(View(row, number), OneClient.Json, statusCode: 201);
    }

    /// <summary>
    /// Deterministic refund id per (org, idempotency key). SHA-256 → Guid: retries of the same
    /// logical refund reuse both the row id and the processor idempotency key (issue 001).
    /// </summary>
    static string StableRefundId(string orgId, string idempotency)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("lazuar-refund:" + orgId + ":" + idempotency));
        return new Guid(hash.AsSpan(0, 16)).ToString("N");
    }

    static async Task<IResult> List(
        string orgId,
        int? limit,
        string? after,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var take = PayList.Clamp(limit);
        var q = db.Refunds.AsNoTracking().Where(x => x.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(after))
        {
            // Issue 015: org-scope the cursor row (see PaymentLinkEndpoints.List).
            var cursor = await db.Refunds.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Id == after, ct);
            if (cursor is not null)
            {
                q = q.Where(x => x.CreatedAt < cursor.CreatedAt
                    || (x.CreatedAt == cursor.CreatedAt && x.Id.CompareTo(cursor.Id) < 0));
            }
        }

        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        string? next = null;
        if (rows.Count > take)
        {
            rows = rows.Take(take).ToList();
            next = rows[^1].Id;
        }

        return Results.Json(new { items = rows.Select(r => View(r)), next_cursor = next }, OneClient.Json);
    }

    /// <summary>
    /// Two concurrent requests with the same Idempotency-Key serialize on the charge lock; the
    /// loser reaches an already-reserved charge. That loser is a replay, so it gets the original
    /// row, not a 409.
    /// </summary>
    static async Task<IResult> ReplayOrAsync(
        PayDbContext db,
        string orgId,
        string checkoutId,
        string idempotency,
        decimal? amount,
        IResult fallback,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotency))
        {
            return fallback;
        }

        var existing = await db.Refunds.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == orgId && x.IdempotencyKey == idempotency, ct);
        if (existing is null)
        {
            return fallback;
        }

        if (existing.CheckoutId != checkoutId || (amount is decimal amt && amt != existing.Amount))
        {
            return PayErrors.Status(409, "Conflict", "idempotency key reused with a different body");
        }

        return Results.Json(View(existing), OneClient.Json);
    }

    static object View(RefundRow row, string? number = null) => new
    {
        id = row.Id,
        org_id = row.OrgId,
        checkout_id = row.CheckoutId,
        charge_id = row.ChargeId,
        amount = row.Amount,
        currency = row.Currency,
        status = row.Status,
        provider = row.Provider,
        reason = row.Reason,
        number,
        created_at = row.CreatedAt
    };
}

public sealed class CreateRefundRequest
{
    public string? CheckoutId { get; set; }
    public decimal? Amount { get; set; }
    public string? IdempotencyKey { get; set; }
}
