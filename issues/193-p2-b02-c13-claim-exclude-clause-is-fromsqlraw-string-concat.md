---
number: "193"
id: B02-C13
severity: P2
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 193 — B02-C13 — Claim exclude clause is FromSqlRaw string concat

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C13 — P2 — Claim exclude clause is FromSqlRaw string concat

**Evidence.** BillingEngineJob.cs 129–131. Values are `Guid`. Not exploitable as injection today.

**Repro.** None that breaks out of a Guid. Hygiene review only.

**Fix direction.** EF parameterized `WHERE NOT IN` or `processedIds` as `Guid[]` bound parameter.

---

