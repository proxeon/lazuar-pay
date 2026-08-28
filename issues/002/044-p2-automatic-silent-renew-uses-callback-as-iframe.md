---
number: "044"
id: PAY-MERCH-010
severity: P2
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 044 — `automaticSilentRenew` uses `/callback` as the iframe target

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`getOidcConfig` sets `automaticSilentRenew: true` and no `silent_redirect_uri`. `CallbackPage` on `isAuthenticated` runs `takeReturnTo()` (destructive) and `<Navigate>`. A silent-renew iframe that loads `/callback` can eat `returnTo` and run a client-side navigate inside the iframe.

Not the local first-login path.

## Related files

- `apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`
- `apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`

## Reproduction

Leave a dashboard tab open until silent renew. Deep-link `returnTo` missing; nested navigates in iframe.

## Blast radius

Flaky renew, lost deep-link (047).

## Suggested fix

Add a **minimal** `silent-renew.html` (or a route that does not `Navigate`) and set `silent_redirect_uri`. Or `automaticSilentRenew: false` until that page exists. Do not reuse `CallbackPage`.

## Tests

- Missing: callback page must not be the silent redirect (lock `silent_redirect_uri` ≠ `/callback`).

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B9
