---
number: "041"
id: PAY-MERCH-007
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 041 — Buyer copy URL defaults to hardcoded `:5179`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`checkoutOrigin()` is `VITE_CHECKOUT_ORIGIN ?? 'http://localhost:5179'`. Copy/Open on the pay-link table uses that. Host PSP return URLs use `Pay:CheckoutBaseUrl` (Development localhost:5179; required outside Testing — 022). Nothing ties the two configs. A production merchant **build** without the env bakes localhost into every Copy button. 016 hardcoded the same string with no env. 018 added the env **and kept the hardcoded fallback**.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **40–48**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- Checkout Vite consumes `/c/{token}`.

## Reproduction

`pnpm build` merchant without `VITE_CHECKOUT_ORIGIN`. Copy a link. URL is `http://localhost:5179/c/…`. Buyers 404. PSP success can go to a different origin than WhatsApp.

## Blast radius

Deployed dashboard. Local dogfood matches Development `CheckoutBaseUrl`.

## Suggested fix

Fail the mint dialog if `VITE_CHECKOUT_ORIGIN` is empty **in production builds** (`import.meta.env.PROD`). Locally, keep 5179 but print it. Longer-term: payment-link 201 includes `pay_url` from `Pay:CheckoutBaseUrl`; SPA copies **that**.

## Tests

- Missing: production build without env fails, or host `pay_url` is preferred.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B7
- `plans/019-evals/08-contracts-spec-honesty.md` two configs for buyer origin
