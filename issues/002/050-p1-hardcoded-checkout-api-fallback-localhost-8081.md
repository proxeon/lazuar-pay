---
number: "050"
id: PAY-CO-002
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 050 — Hardcoded checkout API fallback `http://localhost:8081`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B3
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'`. Vite inlines at build. A `pnpm build` without the env, then any static host, sends every buyer browser to the developer’s laptop. Combined with 049, even a correct API host fails CORS if the SPA origin is not localhost. No Dockerfile / CI production env for checkout.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **8**.
- `apps/lazuar-pay-checkout` `.env.example` if present.
- Merchant `payApi.ts` has the same default (whoami) — merchant is staff laptop-shaped; checkout is the buyer.

## Reproduction

Build checkout without `VITE_PAY_API_URL`. Host the `dist/` on any HTTPS origin. Network panel: requests to `http://localhost:8081`.

## Blast radius

Every production buyer page shipped without the env. Mixed-content if the page is HTTPS.

## Suggested fix

Fail the production build if `VITE_PAY_API_URL` is unset (do not default). Keep the laptop default only in `.env.example` / Vite `dev`. Strip trailing slashes. Document that this value is public (8081 origin), never a secret.

## Tests

- Missing: production build without env fails (script). Locks can grep that PROD does not use the fallback.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B3
