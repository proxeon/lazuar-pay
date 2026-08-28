---
number: "049"
id: PAY-HOST-004
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 049 — CORS allow-list is laptop-only

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B2 (also `07-identity-authz-cors.md` B10)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`Program.cs` `WithOrigins` is eight localhost/127.0.0.1 URLs on 5178/5179/4178/4179. `CorsTests` lock those and deny 3003/3004. There is no `Pay:CorsOrigins` config. Checkout `fetch` is cross-origin to `VITE_PAY_API_URL`. Production (or phone-via-LAN, or `https://pay.example`) GET is a CORS failure → 048.

018 adding 4179 fixed **preview** dogfood. It did not make CORS configurable. One’s rule (empty CORS fails boot) is the opposite of Pay’s silent hardcoded list. Merchant `POST /tenants` depends on **One** CORS; Pay whoami/money depends on **Pay** CORS. Two allowlists. Pay’s cannot change without a code edit.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` **58–72**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay-checkout/src/App.tsx` — `fetch` to payApi.

## Reproduction

Serve checkout from `https://checkout.example`. Origin not in the list. Browser blocks GET `/v1/pay/…`.

## Blast radius

Any deployed checkout/merchant origin. Denying 3003/3004 must stay.

## Suggested fix

Config list `Pay:CorsOrigins` (comma-separated). Development default = the eight laptop URLs. Production must include the checkout origin(s). Keep denying 3003/3004. Add a CorsTest that a configured extra origin is allowed. Never `AllowAnyOrigin` with credentials.

066: current tests only hit `/health`.

## Tests

- Existing: 5178/5179/4178/4179 allow; 3003/3004 deny on `/health`.
- Missing: extra configured origin; `/v1/pay` GET/POST/OPTIONS (066).

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B2
- `plans/019-evals/07-identity-authz-cors.md` §B10
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-10
