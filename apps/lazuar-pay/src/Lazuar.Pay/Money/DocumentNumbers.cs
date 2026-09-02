using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Money;

/// <summary>
/// RCPT-/REF- numbering. The (OrgId, Number) unique index on documents is the backstop, but
/// relying on it means the loser of a race rolls back the whole fulfillment — a real payment
/// acked as lost. Allocation is one atomic upsert on Postgres; the read-modify-write fallback
/// exists only for the InMemory test provider, which has no SQL engine and no concurrency.
/// </summary>
public static class DocumentNumbers
{
    public static async Task<string> AllocateAsync(
        PayDbContext db, string orgId, string series, int year, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            // ToListAsync, not SingleAsync: composing operators over INSERT..RETURNING make
            // EF wrap the statement in a subquery, which Postgres rejects.
            var rows = await db.Database.SqlQuery<int>($"""
                INSERT INTO public.document_sequences AS s ("OrgId", "Series", "YearMyt", "LastN")
                VALUES ({orgId}, {series}, {year}, 1)
                ON CONFLICT ("OrgId", "Series", "YearMyt")
                DO UPDATE SET "LastN" = s."LastN" + 1
                RETURNING s."LastN" AS "Value"
                """).ToListAsync(ct);
            return $"{series}-{year}-{rows.Single():00000}";
        }

        var seq = await db.DocumentSequences.FindAsync([orgId, series, year], ct);
        if (seq is null)
        {
            seq = new DocumentSequenceRow { OrgId = orgId, Series = series, YearMyt = year, LastN = 0 };
            db.DocumentSequences.Add(seq);
        }

        seq.LastN += 1;
        return $"{series}-{year}-{seq.LastN:00000}";
    }
}
