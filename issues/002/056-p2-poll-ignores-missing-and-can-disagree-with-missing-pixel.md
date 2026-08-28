---
number: "056"
id: PAY-CO-008
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 056 — Poll ignores missing and can disagree with the missing pixel

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B10
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Poll deps do not include `error`. Initial GET 404 → `error='missing'`, missing Card, **and** poll starts if `verifying`. Poll 404s are ignored. If a later poll somehow 200s, `setPay` runs but order 1 still shows missing (`error === 'missing'` is first and never cleared).

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **99–115**, **162–176**.

## Reproduction

Open `/c/dead?status=verifying`. Missing Card + 15 useless 404s.

## Blast radius

Low. Dead tokens under verifying query.

## Suggested fix

If boot GET is 404, do not start the poll. If a poll GET is 404, set `error='missing'` and clear the interval. If boot GET succeeds, clear `error`.

## Tests

- Missing: verifying + 404 does not poll.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B10
