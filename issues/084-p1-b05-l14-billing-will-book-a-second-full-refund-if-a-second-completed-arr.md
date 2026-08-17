---
number: "084"
id: B05-L14
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/084-refund-cap-original
---

# 084 — B05-L14 — Billing will book a second full refund if a second Completed arrives

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/084-refund-cap-original`

A second `GatewayRefundCompleted` is capped so refund journals cannot exceed the original sale.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L14 — P1 — Billing will book a second full refund if a second Completed arrives

Per-attempt key includes `event.Id` (new v7 every publish). Commerce remaining is the only cap. Mark-refunded + a later inbound refund (if we ever allow-list it) would be two Completeds. Two ops clicks cannot happen (`ALREADY_REFUNDED`). Stripe `pending` treated as success plus a later dashboard confirm is one Completed from us; a later inbound event would be a second if allow-listed. Today inbound is dropped (B05-L15), so the latent double-contra is “any second Completed with a new Guid”.

`TwoAttempts_TwoLedgerRows` celebrates this grain. It does not assert `sum(refund) <= originalPaid`.

---

