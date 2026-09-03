//! Port of `Money/DocumentNumbers.cs`.
//!
//! RCPT-/REF- numbering. The (OrgId, Number) unique index on documents is the
//! backstop, but relying on it means the loser of a race rolls back the whole
//! fulfillment — a real payment acked as lost. Allocation is one atomic upsert;
//! the C# read-modify-write fallback existed only for the InMemory test provider,
//! which the port does not carry (D008) — the SQL path is the only path.

use postgres::Client;

/// Allocate the next number in an org's series: `REF-2026-00042`.
pub fn allocate(
    tx: &mut postgres::Transaction,
    org_id: &str,
    series: &str,
    year_myt: i32,
) -> Result<String, postgres::Error> {
    // INSERT..ON CONFLICT..RETURNING — one round trip, race-free by the engine.
    // (No ToListAsync wrapper: composing operators over INSERT..RETURNING make EF
    // wrap the statement in a subquery, which Postgres rejects — the C# comment.)
    let row = tx.query_one(
        "INSERT INTO public.document_sequences AS s (\"OrgId\", \"Series\", \"YearMyt\", \"LastN\") \
         VALUES ($1, $2, $3, 1) \
         ON CONFLICT (\"OrgId\", \"Series\", \"YearMyt\") \
         DO UPDATE SET \"LastN\" = s.\"LastN\" + 1 \
         RETURNING s.\"LastN\"",
        &[&org_id, &series, &year_myt],
    )?;
    let n: i32 = row.get(0);
    Ok(format!("{series}-{year_myt}-{n:05}"))
}

/// Transaction-scoped convenience: run inside an open transaction.
pub fn allocate_in(tx: &mut postgres::Transaction, org_id: &str, series: &str, year_myt: i32) -> Result<String, postgres::Error> {
    allocate(tx, org_id, series, year_myt)
}

/// Client-level helper allocating within its own transaction.
pub fn allocate_new(conn: &mut Client, org_id: &str, series: &str, year_myt: i32) -> Result<String, postgres::Error> {
    let mut tx = conn.transaction()?;
    let number = allocate(&mut tx, org_id, series, year_myt)?;
    tx.commit()?;
    Ok(number)
}
