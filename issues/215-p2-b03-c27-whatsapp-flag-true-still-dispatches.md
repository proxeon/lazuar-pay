---
number: "215"
id: B03-C27
severity: P2
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 215 — B03-C27 — WhatsApp flag true still “dispatches”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C27 — P2 — WhatsApp flag true still “dispatches”

Demote when **false** is correct and tested (`PastDue_WhatsAppOnlyNoEmailBody_RecordsLogWithoutPublish`). When `Messaging:WhatsAppEnabled=true`, ALL/WHATSAPP pass through; Messaging’s console stub sends. README honesty says WhatsApp is not shipping. Flipping the flag in prod is a lie, not a default-on bug.

Communications **Payment Failed** template uses `template.Channel` and does not demote in the Commerce dispatcher (separate handler). Messaging still skips WA when the flag is false.

---

