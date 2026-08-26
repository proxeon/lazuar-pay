---
number: "136"
id: B09-U07
severity: P1
status: resolved
resolved_branch: fix/136-admin-login-open-redirect
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 136 — B09-U07 — Admin login open redirect

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/136-admin-login-open-redirect`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U07 — Admin login open redirect (P1)

**Where:** `lazuar-admin/src/components/LoginPage.tsx` 26–31.  
**What:** `window.location.href = returnUrl` with no relative-only check. Ops has `isSafeReturnUrl`. Admin does not.  
**Walk:** `https://hub.lazuar.com/admin/login?returnUrl=https://evil.example`.

