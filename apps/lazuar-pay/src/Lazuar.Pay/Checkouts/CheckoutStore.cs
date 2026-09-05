using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Checkouts;

/// <summary>Postgres-backed checkouts. Not a ledger.</summary>
public sealed class CheckoutStore(PayDbContext db)
{
    public async Task<CheckoutSession> CreateAsync(CheckoutSession session, string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingKey = await db.IdempotencyKeys.FindAsync([session.OrgId, idempotencyKey], ct);
            if (existingKey is not null)
            {
                var existing = await db.Checkouts.FindAsync([existingKey.CheckoutId], ct);
                if (existing is not null)
                {
                    if (!SameFingerprint(existing, session))
                    {
                        throw new IdempotencyConflictException();
                    }

                    return Map(existing);
                }
            }
        }

        var row = new CheckoutRow
        {
            Id = session.Id,
            OrgId = session.OrgId,
            Provider = session.Provider,
            ProductId = session.ProductId,
            PaymentLinkId = session.PaymentLinkId,
            SlotKey = session.SlotKey,
            PublicToken = session.PublicToken ?? Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = session.Amount,
            Currency = session.Currency,
            Status = session.Status,
            Interval = session.Interval ?? "one_off",
            SuccessUrl = session.SuccessUrl,
            CancelUrl = session.CancelUrl,
            CreatedAt = session.CreatedAt
        };
        db.Checkouts.Add(row);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            db.IdempotencyKeys.Add(new IdempotencyKeyRow
            {
                OrgId = session.OrgId,
                Key = idempotencyKey,
                CheckoutId = session.Id
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
            {
                entry.State = EntityState.Detached;
            }

            var raced = await db.IdempotencyKeys.FindAsync([session.OrgId, idempotencyKey], ct);
            var existing = raced is null ? null : await db.Checkouts.FindAsync([raced.CheckoutId], ct);
            if (existing is null)
            {
                throw;
            }

            if (!SameFingerprint(existing, session))
            {
                throw new IdempotencyConflictException();
            }

            return Map(existing);
        }

        return Map(row);
    }

    // Issue 006 (issues/003): product and redirect URLs are part of the replay contract —
    // the fingerprint used to compare only amount/currency/provider/interval, so a reused
    // key with a different product silently replayed the ORIGINAL checkout: charges,
    // receipt labels, and PSP metadata all bound to a product the retry never asked for
    // (compare RefundEndpoints, which 409s a same-key different-body replay). Null and
    // empty compare equal so "absent" stays interchangeable across clients.
    static bool SameFingerprint(CheckoutRow existing, CheckoutSession session) =>
        existing.Amount == session.Amount
        && string.Equals(existing.Currency, session.Currency, StringComparison.OrdinalIgnoreCase)
        && string.Equals(existing.Provider, session.Provider, StringComparison.OrdinalIgnoreCase)
        && string.Equals(existing.Interval ?? "one_off", session.Interval ?? "one_off", StringComparison.OrdinalIgnoreCase)
        && SameText(existing.ProductId, session.ProductId)
        && SameText(existing.SuccessUrl, session.SuccessUrl)
        && SameText(existing.CancelUrl, session.CancelUrl);

    static bool SameText(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

    public async Task<CheckoutSession?> GetAsync(string id, CancellationToken ct)
    {
        var row = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    public async Task<CheckoutSession?> GetByPublicTokenAsync(string token, CancellationToken ct)
    {
        var row = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        return row is null ? null : Map(row);
    }

    public static CheckoutSession Map(CheckoutRow row) => new()
    {
        Id = row.Id,
        OrgId = row.OrgId,
        Provider = row.Provider,
        ProductId = row.ProductId,
        PaymentLinkId = row.PaymentLinkId,
        SlotKey = row.SlotKey,
        PublicToken = row.PublicToken,
        Amount = row.Amount,
        Currency = row.Currency,
        Status = row.Status,
        Interval = row.Interval,
        SuccessUrl = row.SuccessUrl,
        CancelUrl = row.CancelUrl,
        CreatedAt = row.CreatedAt,
        PayerName = row.PayerName,
        PayerEmail = row.PayerEmail
    };
}

public sealed class IdempotencyConflictException() : InvalidOperationException("idempotency key reused with a different body");
