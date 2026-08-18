---
number: "184"
id: B01-C14
severity: P2
status: resolved
resolved_branch: fix/184-checkout-already-subscribed
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 184 — B01-C14 — Public checkout does not call `HasActiveSubscriptionAsync`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/184-checkout-already-subscribed`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C14 — Public checkout does not call `HasActiveSubscriptionAsync`

**Severity:** P2  
**One-sentence fault:** Manual enroll rejects a second ACTIVE/TRIALING row for the same client+product; hosted checkout will happily create a second subscription on a second paid session.

**Evidence.** `ICommerceRepository.HasActiveSubscriptionAsync` exists and is used by `CreateManualSubscriberCommandHandler` (82–85). Grep of `InitiateCheckoutCommandHandler` and the open-checkout webhook: no call.

**Reproduction in words.** Buyer pays monthly twice (two tabs, two emails that resolve to one CRM profile, or one email after the first COMPLETED). Two ACTIVE rows, two fulfillment events, two renewal clocks.

**Blast radius.** Double access / double charge next cycle. Some merchants want this (two seats as two subs). Quantity exists for seats, so a second sub is usually an accident.

**Why tests missed it.** No “already subscribed” initiate test.

**Fix direction.** Product decision: reject, or attach to the existing sub. If reject, do it after CRM resolve using the same helper as manual enroll.

---

