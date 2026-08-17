---
number: "044"
id: B02-C09
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 044 — B02-C09 — HasOpenDispute is set and billing ignores it

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C09 — P1 — HasOpenDispute is set and billing ignores it

**Evidence.** `MarkHasOpenDispute` exists. `CommerceGatewayDisputeCreatedHandler` calls it when metadata resolves a sub (tests: `HasOpenDispute.Should().BeTrue()`). Claim SQL and `canCharge` do not read the flag. There is no `ClearHasOpenDispute`.

**Repro.** ACTIVE Stripe vaulted, dispute event with `subscription_id`. Flag true. Due tick. Off-session still publishes.

**Blast radius.** Every card that is in chargeback. Charging again during an OPEN dispute is how you lose the next one. 008 said the flag was dead; the writer is alive, the reader is not.

**Fix direction.** Exclude `HasOpenDispute` from claim (and from `canCharge`) **or** delete the column. If you exclude, add a clear-on-won/lost path or the row is paused forever.

---

