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
                    return Map(existing);
                }
            }
        }

        var row = new CheckoutRow
        {
            Id = session.Id,
            OrgId = session.OrgId,
            Provider = session.Provider,
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

        await db.SaveChangesAsync(ct);
        return Map(row);
    }

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
