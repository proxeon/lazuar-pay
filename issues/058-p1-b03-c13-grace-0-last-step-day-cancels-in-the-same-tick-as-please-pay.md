---
number: "058"
id: B03-C13
severity: P1
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/058-grace-zero-next-tick
---

# 058 — B03-C13 — Grace 0 / last-step day cancels in the same tick as “please pay”

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/058-grace-zero-next-tick`

Terminal cancel/suspend waits until the tick after `terminalDay` so day-0 email is not a lie.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C13 — P1 — Grace 0 / last-step day cancels in the same tick as “please pay”

**Evidence.** `GraceZero_DispatchesDayZeroThenCancels` asserts EMAIL **and** `SubscriptionCanceled` in one `RunOnce`. Processor: dispatch loop, then terminal (`PastDueDunningProcessor.cs` 227–244) with no “wait for pay link to exist.”

**Repro.** Default-like campaign with grace 0 and a day-0 EMAIL. Mark PAST_DUE same day. Buyer gets a pay link for a CANCELED sub. Arrears POST then 400 “canceled.”

**Blast.** Recovery email is a lie. Chargeback/support.

**Tests.** The behaviour is **pinned as success**. That is a lying-adjacent test if product intent is “email then wait.”

**Fix direction.** Terminal on the **next** tick after the last comms offset, or require `daysOverdue > terminalDay`, or send a different “we canceled you” template instead of the pay template.

---

