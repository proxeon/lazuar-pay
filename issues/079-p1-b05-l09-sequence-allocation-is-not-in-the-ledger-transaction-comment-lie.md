---
number: "079"
id: B05-L09
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/079-sequence-in-ledger-transaction
---

# 079 — B05-L09 — Sequence allocation is not in the ledger transaction; comment lies

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/079-sequence-in-ledger-transaction`

Sequence increment uses BillingDbContext. Payment and refund persist wrap sequence + SaveChanges in IBillingTransactional.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L09 — P1 — Sequence allocation is not in the ledger transaction; comment lies

See §8. Own Dapper connection. Comment at `:26-27` claims gap-free rollbacks. Gaps are the actual behaviour. Two increments on retry after a failed `SaveChanges`.

---

