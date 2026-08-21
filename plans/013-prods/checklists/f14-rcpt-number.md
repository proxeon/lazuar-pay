# F14 — `RCPT-` number in the same transaction

**Track:** Fulfillment · **Depends:** F13, D27  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-DOC-001, NP-DOC-002  
**Goal:** Allocate `RCPT-{MalaysiaTime year}-#####` in the same TX as the journal. Never a UUID.

---

## F14.1 Allocate

- [x] Read `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` and `…/MalaysiaTime.cs` — **read, do not copy project**
- [x] Year from MalaysiaTime (`Asia/Kuala_Lumpur` / Windows `Singapore Standard Time`), not UTC, not machine local
- [x] Pin NYE: `2025-12-31 18:00 UTC` → prefix `RCPT-2026`
- [x] Format `{prefix}-{value:D5}` e.g. `RCPT-2026-00001`. Per `(org_id, prefix)`
- [x] Increment in the **same** transaction as the journal (failed persist rolls the increment back)

## F14.2 Never UUID

- [x] Never `Guid.ToString` / `ToString("N")` / slice of journal id as the number
- [x] If allocate fails: leave the string **`PENDING`**, not a UUID
- [x] `PENDING` is a bug fallback, not the happy path

## F14.3 Must not

- [x] No `INV-` / `CN-` / `QT-` / `SAAS-` in this handler
- [x] No MediatR `GenerateNextSequenceNumberCommand`

## F14.4 Exit

- [x] Happy path number is `RCPT-` + MYT year + 5 digits
- [x] Unblocked for F15 and F20
