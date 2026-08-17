---
number: "133"
id: B08-M10
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 133 — B08-M10 — Cancel (and wiki) still speak list price after Gross

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M10 — P1 — Cancel (and wiki) still speak list price after Gross

**Where:** `SubscriberQueryService.cs` 96–103; `LifecycleEventHandlers.cs` 63–72; wiki lines 93–94; `ISubscriberQueryService` comment “list price.”

**What:** 5 seats × RM 99 snapshot, SST on, AUTO_CHARGE RM 534.60. Day-0 dunning now prints `534.60`. Cancel custom `{{amount}}` prints `99.00`. Wiki tells the merchant both tags are list price.

**Why it matters:** The Gross fix was applied to the producer that 008 named and not to the other two subscription mail producers, and the wiki was not updated. Merchants will “fix” dunning by editing the template toward the wiki and make it wrong again.

Immediate-fail empty amount is the third sibling (B08-M15). Default cancel body does not print money, so default tenants only see the wiki lie until they customize.

---

