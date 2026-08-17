---
number: "235"
id: B05-L31
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 235 — B05-L31 — Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L31 — P2 — Credit hold: no unique correlation; `RELEASED` never written; exhaust stays `HELD`

See §7. Two reserves of the same broadcast deduct twice. Domain tests cover consume/release math, not the handler race.

---

