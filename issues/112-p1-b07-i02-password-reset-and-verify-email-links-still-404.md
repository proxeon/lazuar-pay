---
number: "112"
id: B07-I02
severity: P1
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 112 — B07-I02 — Password-reset and verify-email links still 404

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I02 — P1 — Password-reset and verify-email links still 404

**Where.** `NotificationDispatchDomainEventHandlers.cs:31, 50` use `GetClientBaseUrl()` → `App:ClientUrl`. Portal has no `/reset-password` or `/verify-email` (`apps/lazuar-portal/src/app/` listing). Ops has neither route; `*` would drop the token. `LoginPage.tsx` has no forgot-password link.

**What.** Forgot-password API is real. Reset API is real. Clicking the mail is a buyer 404 (`not-found.tsx`). Token sits in the 404 URL (history, Referer). Verify is worse: even a future page must be logged in as that user (`AuthEndpoints.cs:121–128`), and register never minted a token.

**008.** Open. `297ba98` commit message: “ClientUrl stays portal.” That is correct for checkout. It is wrong for merchant recovery if portal has no pages.

