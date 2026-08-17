---
number: "042"
id: B02-C07
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 042 — B02-C07 — ARPU denominator includes PAST_DUE

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C07 — P1 — ARPU denominator includes PAST_DUE

**Evidence.** Stats.cs 46 and 61. `activeSubs = ACTIVE || PAST_DUE`. `mrr` already zeros PAST_DUE.

**Repro.** Two ACTIVE @ 100 and one PAST_DUE @ 100. MRR = 200. ARPU = 66.66. Honest ARPU on paying actives is 100.

**Blast radius.** Ops dashboard only. Not money movement.

**Fix direction.** Denominator = rows that contributed to MRR (ACTIVE, unpaused, mo/yr). Or show two numbers.

---

