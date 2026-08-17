---
number: "293"
id: B08-M23
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 293 — B08-M23 — Parser misses string `to`; webhook 200 on suppress failure

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M23 — P2 — Parser misses string `to`; webhook 200 on suppress failure

**Where:** `ResendWebhookParser.ReadRecipient` 47–66; endpoint 160–165.

**What:** If Resend ever sends `"to": "user@example.com"` instead of an array, recipient is null, event acknowledged, no suppress. DB exceptions inside the try are acknowledged too.

---

