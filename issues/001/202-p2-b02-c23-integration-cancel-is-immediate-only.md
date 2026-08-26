---
number: "202"
id: B02-C23
severity: P2
status: resolved
resolved_branch: fix/202-integration-cancel-immediate-only
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 202 — B02-C23 — Integration cancel is immediate only

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/202-integration-cancel-immediate-only`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C23 — P2 — Integration cancel is immediate only

**Evidence.** `IntegrationSubscriptionEndpoints.cs` 87: `AtPeriodEnd: false`. Honest vs the contract (no body). Do not document “integrator can schedule period-end.”

---

