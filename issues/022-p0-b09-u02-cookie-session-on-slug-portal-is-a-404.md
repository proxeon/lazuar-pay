---
number: "022"
id: B09-U02
severity: P0
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 022 — B09-U02 — Cookie session on `/{slug}/portal` is a 404

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U02 — Cookie session on `/{slug}/portal` is a 404 (P0)

**Where:** `portal/page.tsx` 24–45; `PublicPortalEndpoints.cs` 36–37.  
**What:** FE treats `/one/auth/me` success as enough to skip the magic-link form, then calls GET portal with `token: ""`. API is token-only. Unauthorized → `notFound()`.  
**Walk:** Merchant opens their own portal to preview. Buyer who has a product cookie from checkout. Both get a localized 404 instead of the form or the dashboard.  
**008** described cookie sessions as a live path. They are not, at this HEAD, after `9b531d2` required tokens on arrears and the portal GET stayed token-only.

