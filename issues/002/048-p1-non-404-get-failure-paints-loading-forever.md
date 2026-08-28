---
number: "048"
id: PAY-CO-001
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 048 — Non-404 GET failure paints Loading… forever

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B1
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Render order: `error === 'missing'` first, then `if (!pay) return Loading…`. Boot GET sets `error` to `'status 500'` / `'Failed to fetch'` / `'error'` and leaves `pay` null. Buyer stares at “Loading…”. 014 and 016 recorded this. 018 wrapped it in a Card and did not add an error pixel.

404 is honest (“Link not found”). Dead 8081, CORS (049), GET 500 are not.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **74–97**, **162–186**.

## Reproduction

Stop 8081. Open `/c/{any}`. Loading… forever. No Retry.

## Blast radius

First production CORS miss (049+050). Looks like a hung phone.

## Suggested fix

After boot GET fails with anything other than 404, paint a Card: “Can’t reach Pay”, host `detail` if any, Retry that re-runs `load()`. Do not say “sign in”. Do not send Bearer. Keep 404 as “Link not found”.

## Tests

- Existing locks do not cover the Loading graveyard.
- Missing: non-404 error Card (component or grep for the title).

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B1
