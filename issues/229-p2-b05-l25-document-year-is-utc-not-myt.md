---
number: "229"
id: B05-L25
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 229 — B05-L25 — Document year is UTC, not MYT

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L25 — P2 — Document year is UTC, not MYT

`DocumentSeries.Prefix` uses `DateTime.UtcNow`. Consolidation periods are MYT. A 1 Jan 02:00 MYT sale can be `RCPT-2025-#####` and fall in the 2026-01 consolidation month. Ugly, not a cent-wrong.

---

