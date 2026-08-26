---
number: "023"
id: B09-U03
severity: P0
status: resolved
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
resolved_branch: fix/023-portal-token-hrefs
---

# 023 — B09-U03 — “Update payment method” from a cookie/tokenless portal interpolates `token=undefined`

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/023-portal-token-hrefs`

Portal and update-payment links encode a real token and never interpolate the string `"undefined"`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U03 — “Update payment method” from a cookie/tokenless portal interpolates `token=undefined` (P0)

**Where:** `portal/page.tsx` 174; `update-payment/[subId]/page.tsx` 16–29; `ArrearsAccess.cs` 20–23.  
**What:** `` `?token=${token}` `` with `token === undefined` is the string `"undefined"`, which passes the `if (!token)` guard and fails HMAC.  
**Walk:** Reach the portal somehow without a real token (you cannot, because of U02 — unless a stale HTML or a future cookie-auth API lands). More importantly, **any** render where `searchParams.token` is missing produces this href. Combined with U01 (success lands tokenless), the next click is 404.  
Reminder-only and “good standing” CTAs also omit the token (`74`, `110`).

