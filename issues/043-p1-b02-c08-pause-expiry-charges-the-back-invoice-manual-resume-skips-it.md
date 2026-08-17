---
number: "043"
id: B02-C08
severity: P1
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/043-pause-expiry-skip-back-invoice
---

# 043 — B02-C08 — Pause expiry charges the back invoice; manual resume skips it

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/043-pause-expiry-skip-back-invoice`

Pause expiry skips the back invoice and rolls `NextBillingDate` the same way as manual resume.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C08 — P1 — Pause expiry charges the back invoice; manual resume skips it

**Evidence.** Resume handler 128–136 pushes `NextBillingDate` to now+interval when it is already past. Job pause skip (201–206) and claim exclude do **not** roll the date. When `CollectionPausedUntil` becomes ≤ now, the old due is claimed and charged.

**Repro.** ACTIVE due yesterday, pause until tomorrow. Wait (or set the pause date in the past). Job charges. Contrast: same setup, click Resume today, next bill is +1 interval, no charge this cycle.

**Blast radius.** Every collection holiday that ends by the clock rather than the button. W3-LP-057 sold “does not roll” as if both paths agreed.

**Tests.** Domain resume pushes when given a next. Job pause tests assert the date **stays in the past**. They never expire the pause and watch a charge.

**Fix direction.** Pick one product rule and implement both sides. Skip-the-invoice: on pause skip or on expire, set `NextBillingDate = max(CollectionPausedUntil, AdvanceFrom(old, interval))` and `failedIds`. Collect-the-invoice: resume must not jump the clock; charge on resume/expiry. Document it on the ops button.

---

