---
number: "149"
id: B09-U20
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 149 — B09-U20 — Legal/privacy/landing still sell WhatsApp, communities, courses

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U20 — Legal/privacy/landing still sell WhatsApp, communities, courses (P1)

**Where:** `legal/privacy/page.tsx` 30, 41; `legal/terms/page.tsx` 20, 33; `app/page.tsx` 14; contrast `BillingSettingsPage.tsx` 149 and `Messaging__WhatsAppEnabled=false`.  
**Walk:** Buyer reads privacy, thinks WhatsApp will fire. It will not.

