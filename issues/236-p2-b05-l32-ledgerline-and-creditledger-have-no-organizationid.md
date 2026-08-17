---
number: "236"
id: B05-L32
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 236 — B05-L32 — `LedgerLine` and `CreditLedger` have no `OrganizationId`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L32 — P2 — `LedgerLine` and `CreditLedger` have no `OrganizationId`

Not `IMustHaveTenant`. No global filter on the child table. `GetLedgerEntriesAsync` loads lines by `LedgerEntryId = ANY(@EntryIds)` after the header query filtered by org — safe on that path. A future raw `FROM billing.LedgerLines` without a join is a cross-tenant read. `CreditLedgers` history is loaded by `TenantCreditBalanceId` from an org-scoped wallet — safe on that path.

Admin document download (`AdminLedgerEndpoints:36-46`) does not load the ledger row at all; it presigns `vault/{ctx.TenantId}/documents/{id}.pdf`. Guessing another org’s entry id looks in **your** prefix. Not an IDOR on their PDF. Guessing your own missing id returns a signed 404-from-R2.

---

