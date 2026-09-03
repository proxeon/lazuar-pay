//! Port of `Checkouts/CheckoutTransitions.cs`.
//!
//! Issue 002 (issues/001): checkout status transitions are compare-and-set at the
//! database. The original blind tracked writes were last-writer-wins — the TTL expiry
//! sweep could overwrite a just-committed "paid" to "expired" (freeing capacity for a
//! delivered order and arming the late-pay refund path against a fulfilled checkout),
//! and the fulfiller could symmetrically stamp "paid" over a committed "expired".
//!
//! Every transition issues
//! `UPDATE public.checkouts SET "Status" = $to WHERE "Id" = $id AND "Status" = $from`
//! and treats 0 affected rows as "another writer moved it — not ours to change".
//!
//! The C# version also needed EF "tracker hygiene" (syncing + backdating the original
//! value) so a later SaveChanges could not blindly rewrite Status; the Rust port has
//! no change tracker at all — this module is the ONLY code that writes the Status
//! column, enforced by keeping the SQL private here (D007).

use uuid::Uuid;

/// CAS transition from "open" to `status`. Returns false when another writer
/// already moved the row off "open".
pub fn try_leave_open(
    conn: &mut postgres::Client,
    checkout_id: Uuid,
    status: &str,
) -> Result<bool, postgres::Error> {
    try_transition(conn, checkout_id, "open", status)
}

/// CAS transition from `from` to `to`. Issue 004 uses the failed→open direction
/// to re-open a past_due subscription's checkout for retry.
pub fn try_transition(
    conn: &mut postgres::Client,
    checkout_id: Uuid,
    from: &str,
    to: &str,
) -> Result<bool, postgres::Error> {
    let affected = conn.execute(
        "UPDATE public.checkouts SET \"Status\" = $1 WHERE \"Id\" = $2 AND \"Status\" = $3",
        &[&to, &checkout_id.to_string(), &from],
    )?;
    Ok(affected > 0)
}
