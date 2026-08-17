---
number: "217"
id: B03-C29
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 217 — B03-C29 — `current_period_end` in dunning copy is `NextBillingDate`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C29 — P2 — `current_period_end` in dunning copy is `NextBillingDate`

Dispatcher 88–90; portal SQL aliases `NextBillingDate as CurrentPeriodEnd`. For PAST_DUE that is the missed date. Templates that say “renews on” are a day-0 lie. Honesty, not overcharge.

---

