---
number: "285"
id: B08-M15
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 285 — B08-M15 — Immediate fail amount is empty; context port cannot carry Gross

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M15 — P2 — Immediate fail amount is empty; context port cannot carry Gross

**Where:** `CommerceSubscriptionCommsContext` (three fields); `GatewayPaymentFailedIntegrationEventHandler` 88–91.

**What:** Catalog does not print amount. Custom templates get `""`. Port would have to grow (or fail-mail should call the same Gross helper cancel should call).

Tests lock the update-payment URL and “no `{{` leftovers” (`GatewayPaymentFailedEmailHandlerTests` 60–76). They do not assert amount. Empty replace leaves no `{{amount}}` if the catalog omits the tag — the test cannot see the hole.

---

