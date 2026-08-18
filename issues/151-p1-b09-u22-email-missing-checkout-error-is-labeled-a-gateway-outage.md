---
number: "151"
id: B09-U22
severity: P1
status: resolved
resolved_branch: fix/151-email-missing-not-gateway
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 151 — B09-U22 — Email-missing checkout error is labeled a gateway outage

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/151-email-missing-not-gateway`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U22 — Email-missing checkout error is labeled a gateway outage (P1)

**Where:** `errors.ts` 23–28; `i18n.test.mjs` 135–137; `messages.ts` `error.gatewayDown`.  
**Walk:** Merchant forgot Resend. Buyer thinks Billplz is down.

