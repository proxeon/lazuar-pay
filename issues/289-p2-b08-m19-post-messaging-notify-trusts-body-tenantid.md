---
number: "289"
id: B08-M19
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 289 — B08-M19 — `POST /messaging/notify` trusts body.TenantId

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M19 — P2 — `POST /messaging/notify` trusts body.TenantId

**Where:** `Endpoints.cs` 23–27; `SendTenantNotificationCommand` / handler 22–29.

**What:** OrgAdmin of tenant A can pass tenant B’s id. Sink is `ConsoleMessagingService` with B’s slug. Authz test only checks the policy name, not the id binding.

---

