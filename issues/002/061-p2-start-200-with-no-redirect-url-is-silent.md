---
number: "061"
id: PAY-CO-013
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 061 — Start 200 with no `redirect_url` is silent

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B15
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`if (body.redirect_url) window.location.assign(...)` else return. Host success always includes it. Test always has it. Malformed proxy / spec drift: button re-enables, form stays, no alert. Buyer not treated as paid (good) but gets no error.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **153–156**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **192**.
- `packages/pay-spec/main.tsp` `StartPayResponse.redirect_url: string`.

## Reproduction

Proxy strips `redirect_url` on 200. Click Pay. Nothing.

## Blast radius

Low unless a gateway sits in front. Spec generated clients assume the field.

## Suggested fix

`else setError('Processor did not return a pay URL')`. Do not invent a URL. Do not treat as paid.

## Tests

- Missing: 200 empty body sets an alert.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B15
