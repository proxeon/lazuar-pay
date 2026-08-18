---
number: "198"
id: B02-C19
severity: P2
status: resolved
resolved_branch: fix/198-no-plan-change-on-flagged
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 198 — B02-C19 — Admin can schedule plan/qty on a flagged sub; job discards them

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/198-no-plan-change-on-flagged`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C19 — P2 — Admin can schedule plan/qty on a flagged sub; job discards them

**Evidence.** `ChangePlanCommandHandler` has no `CancelAtPeriodEnd` guard. Portal does. ProcessOne cancels before apply.

**Repro.** Flag period-end. Schedule Pro. Due tick. CANCELED, still on Basic, pending gone.

**Fix direction.** Same portal guard on admin, or apply pending onto the canceled row (usually wrong). Prefer the guard.

---

