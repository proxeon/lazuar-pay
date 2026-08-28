---
number: "055"
id: PAY-CO-007
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 055 — Verifying timeout does not restart; Pay form stays unreachable

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B9 (also `10-honesty-bugs-gaps.md` P1-12)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

After `n >= 15`, interval dies, `verifyTimedOut` true. Refresh does one GET and does not `setInterval` again. There is no “Back to pay” that strips `?status=verifying`. Query is sticky.

Late webhook (Billplz tunnel down, wrong `whsec`, CHIP PEM): 30s of Confirming, then “Not paid yet” forever unless they Refresh at the right moment. If they never actually paid (Billplz/Razorpay cancel that still hit the success URL), they cannot click Pay without editing the URL.

016 stuck pixel is **mostly FIXED** (timeout + Refresh). The cul-de-sac remains.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **99–115**, **242–277**.

## Reproduction

Return `?status=verifying` on an `open` checkout. Wait 30s. Refresh once while still unpaid. No further polls. No Pay button.

## Blast radius

Every delayed / failed webhook. Billplz localhost dogfood.

## Suggested fix

Refresh should reset `n` and restart the 15-tick loop. After timeout, if still `open`, offer “Return to pay” that `history.replaceState` / `location.assign` without the query (cancel semantics: not paid). Keep “success URL is not paid.” Do not auto-show the Pay form on timeout while the query is present if returning from success must not look like first visit. A labeled escape is honest.

## Tests

- Existing: locks verifying ≠ paid, setInterval, timeout UI strings.
- Missing: Refresh restarts poll; return-to-pay exists.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B9
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-12
