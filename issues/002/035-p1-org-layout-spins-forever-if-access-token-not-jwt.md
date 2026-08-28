---
number: "035"
id: PAY-MERCH-001
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 035 — Org layout spins forever if `access_token` is not a JWT

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B1 (also `07-identity-authz-cors.md` B5)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`RequireAuth` lets any OIDC `isAuthenticated` user through. `OrgLayout` takes `pickApiBearerToken` (JWT-like `access_token` only). If Zitadel issues an opaque or JWE access token, the token is `undefined`, the whoami effect returns immediately, and the page never leaves “Loading workspace…”. `HomePage` / `CreateWorkspacePage` at least `signinRedirect()` on `!token`. Org routes do not.

API-provisioned SPA apps request JWT access tokens, so new `type=spa` dogfood is fine. An old opaque Zitadel app livelocks. Do not heal by sending `id_token` (`bearerToken.test.ts` forbids it).

## Related files

- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx` **33–69**.
- `apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.ts` **14–18**.
- `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx` **16–18**.

## Reproduction

Authenticate with a non-JWT access_token. Open `/o/{orgId}/overview`. Infinite “Loading workspace…”.

## Blast radius

Mis-provisioned OIDC clients. Looks like Pay is down.

## Suggested fix

Treat missing JWT as signed-out: `signinRedirect()`, or a banner + Retry. Optionally fail `RequireAuth` when `pickApiBearerToken` is empty so `/o/*` never mounts. Never fall back to `id_token`.

## Tests

- Existing: picker never returns `id_token`.
- Missing: OrgLayout / RequireAuth behaviour when access_token is opaque (component test or a lock that OrgLayout redirects).

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B1
- `plans/019-evals/07-identity-authz-cors.md` §B5
