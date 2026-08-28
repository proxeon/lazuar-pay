---
number: "058"
id: PAY-CO-010
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 058 — Path regex has no `$` — extra path segments still pay

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B12
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`tokenFromPath` is `^/c/([^/]+)` **without** `$`. `/c/tok/extra` captures `tok`. 016 claimed the regex was anchored. Merchant and host mint `/c/{token}` with no extra segment, so WhatsApp paste is fine.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **38–41**.

## Reproduction

Open `/c/{validToken}/receipt`. Pay form for that token, not “Link not found.”

## Blast radius

Low unless someone later mounts receipts on `/c/{token}/receipt` and this regex steals the page.

## Suggested fix

`^/c/([^/]+)/?$` (optional trailing slash, reject extra segments). Do not introduce `react-router` to fix this.

## Tests

- Missing: lock the regex includes `$` or `/?$`.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B12
