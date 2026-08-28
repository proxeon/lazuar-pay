---
number: "022"
id: PAY-HOST-003
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 022 — `CheckoutUrls.Base` throw in `MintOrResume` is uncaught 500

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B10
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`MintOrResume` calls `CheckoutUrls.Base(config, env)` **before** the `Start` try/catch around `CreateHostedUrlAsync`. `Base` throws `Pay:CheckoutBaseUrl is required` outside Testing when config is empty. Production without `Pay__CheckoutBaseUrl` → unhandled **500** on first **payment-link** start.

One-off checkouts with merchant `success_url` set never hit `Base` until a rail that calls `CheckoutUrls.Success` with a blank checkout success URL. Development JSON has the localhost default, so laptop payment-links work.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs` **18–31**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **244**, **169–202** — Base before try; try only wraps rail HTTP.
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json` **12** — laptop default.
- `apps/lazuar-pay/.env.example` — `Pay__CheckoutBaseUrl`.

## Reproduction

Production (or any non-Testing) host without `Pay:CheckoutBaseUrl`. Start a payment-link token. 500 instead of 503 problem JSON.

## Blast radius

First production pay-link buyer. Laptop is fine. Kernel one-off checkouts with explicit success_url may dodge it.

## Suggested fix

Validate `Pay:CheckoutBaseUrl` at boot outside Testing (same as WrapKey). In `MintOrResume`, map the throw to 503 problem JSON like Start already does for “callback base”. Do not hard-code `http://localhost:5179` in Production.

## Tests

- Missing: production-missing `Pay:CheckoutBaseUrl` is 503 on payment-link start, not 500.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B10
- `plans/019-evals/08-contracts-spec-honesty.md` CheckoutBaseUrl
