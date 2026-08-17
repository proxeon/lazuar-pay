---
number: "228"
id: B05-L24
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 228 — B05-L24 — `ValidateBalanced` is a one-sided toy

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L24 — P2 — `ValidateBalanced` is a one-sided toy

Base only. No per-currency. No sign convention. Empty line list sums to 0 (the `$0`-price zero-checkout header). Comments claim 500-year-old certainty. There is no `LedgerEntryBalanceTests`. Coverage is handler composition. The method will not catch B05-L01, L05, L12, L13, L14.

---

