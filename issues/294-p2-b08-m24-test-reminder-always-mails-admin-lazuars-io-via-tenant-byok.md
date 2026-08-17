---
number: "294"
id: B08-M24
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 294 — B08-M24 — Test reminder always mails `admin@lazuars.io` via tenant BYOK

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M24 — P2 — Test reminder always mails `admin@lazuars.io` via tenant BYOK

**Where:** `SendTestReminderCommandHandler.cs` 168–176; `TemplateEndpoints.cs` 114–120.

**What:** Tenant’s domain sends to Lazuar staff. Preview mocks (Ahmad / Founders Mastermind / portal.lazuar.com). WhatsApp test would console-log `+60123456789` if the flag were on.

---

