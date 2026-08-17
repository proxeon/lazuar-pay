---
number: "286"
id: B08-M16
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 286 — B08-M16 — Tax Invoice / Credit Note email uses Official Receipt copy

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M16 — P2 — Tax Invoice / Credit Note email uses Official Receipt copy

**Where:** `DocumentPublishedIntegrationEventHandler.cs` 38–59; catalog has neither name (`DefaultMessageTemplates.cs` 23–87).

**What:** Fallback is intentional in code. Subject is still “Your official receipt from {business}.” W4-LP-100 fixed the **PDF** disclaimer. The email still says receipt. Event has no amount (`DocumentPublishedIntegrationEvent.cs` 10–18).

---

