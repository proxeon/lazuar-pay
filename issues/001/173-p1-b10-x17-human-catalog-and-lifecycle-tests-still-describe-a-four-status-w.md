---
number: "173"
id: B10-X17
severity: P1
status: resolved
resolved_branch: fix/173-trialing-catalog
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 173 — B10-X17 — Human catalog and lifecycle tests still describe a four-status world

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/173-trialing-catalog`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X17 — P1 — Human catalog and lifecycle tests still describe a four-status world

`apps/lazuar-docs/docs/reference/events.md` line 46: `subscription.activated` = “First paid period or recovery that lands `ACTIVE`.” After Wave 3 a trial start emits `event_type=subscription.activated` and `data.status=TRIALING`, `amount=0`. Grep of `apps/lazuar-docs/**/*.md` for `TRIALING` is empty.

`SubscriptionLifecycleWebhookTests.Payload_FiveEventTypes_ShareRequiredFields` parametrizes only `ACTIVE | PAST_DUE | CANCELED | SUSPENDED`. There is no case that `ActivateTrial` and asserts `data.status == TRIALING`.

Generated clients **do** include `TRIALING` after `cbe17c2` (`packages/api-types-ts/src/index.ts` 2798; C# enum member `TRIALING = 4`). 008 H4 is **half-closed**: clients caught up; catalog and the webhook test suite did not.

Ops picker hint still says “New **paid** subscription” (out of this slice’s primary trees, cited as contract honesty).

