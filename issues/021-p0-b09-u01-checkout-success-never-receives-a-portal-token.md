---
number: "021"
id: B09-U01
severity: P0
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 021 — B09-U01 — Checkout success never receives a portal token

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U01 — Checkout success never receives a portal token (P0)

**Where:** `CheckoutSuccessView.tsx` 50–52, 162–166, 191; `PublicCheckoutEndpoints.cs` 114–118; `public-routes.tsp` 122–126.  
**What:** Status poller always returns `Token = null`. Success CTA, timeout CTA, and custom-success `returnHref` go to `/{slug}/portal` without `?token=`.  
**Walk:** Pay → COMPLETED → “Go to dashboard” → magic-link form (or cookie 404, B09-U02). The buyer just paid and cannot open the portal from the page the product sent them to.  
**Not a missing route.** The UI calls a contract that documents “does not mint portal tokens” and then branches on `response.token`.

