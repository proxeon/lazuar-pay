---
number: "283"
id: B08-M13
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 283 — B08-M13 — GlobalUserProfileUpdated overwrites every linked CRM email

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M13 — P2 — GlobalUserProfileUpdated overwrites every linked CRM email

**Where:** `GlobalUserProfileUpdatedIntegrationEventHandler.cs` 20–33.

**What:** All `GlobalUserId == user` rows, every tenant, get `FullName` and `Email` from One. No uniqueness pre-check. Can collide with `(org, newEmail, phone)`. Can change the email anonymize will later scrub logs against (B08-M07).

Guest checkout does not set `GlobalUserId`. Resolve does not either. This fires for Create-linked or subsequently linked profiles.

---

