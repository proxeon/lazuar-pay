# D26 — Journal tables

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-MON-001. `journal_entries` + `journal_lines`. Balanced check is F13.

---

## D26.1 Tables

- [ ] `journal_entries` + `journal_lines` in the D14 schema (same database as checkouts)
- [ ] Header has `org_id` (One tenant id)
- [ ] Lines are double-entry shaped (enough for F13 to balance). Insert-only is fine

## D26.2 Refuse

- [ ] **No** `TaxInvoiceId` dual-use column
- [ ] **No** `billing` schema / `BillingDbContext`
- [ ] No LHDN UUID / VALID / consolidation columns
- [ ] No copy of `billing.LedgerEntries` row-for-row

## D26.3 Not this file

- [ ] Balanced check in **F13** (`ValidateBalanced` **judgment**, not the module)
- [ ] Same TX as paid + `RCPT-` is F, not D26
- [ ] Do not add nine outboxes so “billing can subscribe”

## D26.4 Exit

- [ ] Both tables exist on `lazuar_pay`
- [ ] Unblocked for D27
