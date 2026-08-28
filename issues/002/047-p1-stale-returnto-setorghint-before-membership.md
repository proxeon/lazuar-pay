---
number: "047"
id: PAY-MERCH-013
severity: P1
status: resolved
source: plans/019-evals/07-identity-authz-cors.md
head: "9f04ad58"
---

# 047 — Stale `returnTo` / `setOrgHint` before membership

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/07-identity-authz-cors.md` B3 (also `02-merchant-frontend.md` last workspace)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`CallbackPage` honors `lazuar-pay-merchant:returnTo` over `dashboardPath`. `OrgLayout` writes `ORG_HINT_KEY` for the URL org **before** `tenants.find`. A staff member who lost org A, still has org B, and logs in from a bookmarked `/o/A/overview` gets an error page (046), not B. Hint is now A (not a member). Next visit to `/` recovers via `dashboardPath` (hint not in list → `tenants[0]`). The login completion itself is wrong.

`HomePage` uses `dashboardPath` correctly **after** whoami. Deep links skip that.

## Related files

- `apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx` **38–39**.
- `apps/lazuar-pay-merchant/src/lib/homePath.ts`
- `apps/lazuar-pay-merchant/src/lib/sessionKeys.ts`

## Reproduction

Bookmark `/o/{removed-org}/overview`. Login. Error page. Hint poisoned. `/` later opens another org.

## Blast radius

Ex-members, removed workspaces, shared laptops.

## Suggested fix

Do not `setOrgHint` until membership is confirmed. If `returnTo` org is not in `tenants`, ignore it and use `dashboardPath`. 046 still needs an escape hatch.

## Tests

- Existing: home redirects into last org (`locks.test.ts` homePath).
- Missing: last org not in tenants → first tenant / create, not the stale id.

## Source reports

- `plans/019-evals/07-identity-authz-cors.md` §B3 §G5
- `plans/019-evals/02-merchant-frontend.md` last workspace
