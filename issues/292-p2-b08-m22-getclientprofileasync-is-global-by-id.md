---
number: "292"
id: B08-M22
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 292 — B08-M22 — `GetClientProfileAsync` is global-by-id

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M22 — P2 — `GetClientProfileAsync` is global-by-id

**Where:** `CrmQueryService.cs` 54–61.

**What:** `IgnoreQueryFilters`, no `OrganizationId`. A leaked UUID is a PII read. Callers today pass ids from their own tenant rows.

---

