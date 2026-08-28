---
number: "057"
id: PAY-CO-009
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 057 — `startPay` network throw is unhandled

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B11
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`try/finally` around `fetch` without `catch`. `finally` clears `busy`. Rejection is unhandled. CORS/network on **click**: button re-enables, form unchanged, console error. Buyer clicks again. Double-tab can mint two seats on remaining>1 before `started` is in React state (host occupancy is the real lock; SPA `busy` is UI debounce).

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **117–160**.

## Reproduction

Start Pay with 8081 blocked after GET succeeded. Unhandled rejection. Button clickable again.

## Blast radius

049 CORS on click; flaky networks. Combined with 001.

## Suggested fix

`catch` → `setError` “Can’t reach Pay”. Keep `finally` busy-clear. Same language as 048.

## Tests

- Existing: 400/409/503 mapped; no catch path.
- Missing: grep `catch` in `startPay` or a component test.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B11
