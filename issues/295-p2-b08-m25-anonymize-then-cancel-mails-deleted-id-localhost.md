---
number: "295"
id: B08-M25
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 295 — B08-M25 — Anonymize then cancel mails `deleted_{id}@localhost`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M25 — P2 — Anonymize then cancel mails `deleted_{id}@localhost`

**Where:** order in §4.14 step 6; `LifecycleEventHandlers` 46–48.

**What:** Not a PII leak (dummy + real address suppressed). It is a wasted Resend call and a FAILED delivery-log row that support will read as “we emailed the deleted user.”

---

