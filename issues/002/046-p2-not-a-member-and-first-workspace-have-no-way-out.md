---
number: "046"
id: PAY-MERCH-012
severity: P2
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 046 — “Not a member” / first-workspace create have no way out

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B11
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`OrgLayout` error is a centered `<p>` — no switcher, no Home, no Sign out. A typo in `/o/{uuid}` traps the session.

Chrome-less `/workspaces/new` (zero tenants) has a header and the form. No Sign out. First-time Ada on the wrong account cannot leave without clearing sessionStorage.

## Related files

- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx` **53–60**.
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspacePage.tsx`
- `apps/lazuar-pay-merchant/src/lib/homePath.ts`

## Reproduction

Open `/o/not-a-uuid/overview` while signed in. Red “Not a member of this org”. No escape.

## Blast radius

Support footgun, not money.

## Suggested fix

Both states need Sign out + “Switch workspace” (link to `/` so `HomePage` re-runs whoami). Do not mount the full sky rail for a membership miss if that implies the org exists.

## Tests

- Missing: error chrome includes Sign out.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B11
- `plans/019-evals/07-identity-authz-cors.md` §B3 related stale org
