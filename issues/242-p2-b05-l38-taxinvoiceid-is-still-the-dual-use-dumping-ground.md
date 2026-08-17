---
number: "242"
id: B05-L38
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 242 — B05-L38 — `TaxInvoiceId` is still the dual-use dumping ground

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L38 — P2 — `TaxInvoiceId` is still the dual-use dumping ground

UUID overwrite after validate. Consolidation ref overwrite after batch. `CustomerDocumentNumber` is the real commercial number. Lookup still searches `TaxInvoiceId`. `FirstOrDefault` on multiple matches has no type preference (`LedgerLhdnLookup`). A cancel whose internal id collides with a UUID-shaped `TaxInvoiceId` on the wrong row is theoretical.

---

