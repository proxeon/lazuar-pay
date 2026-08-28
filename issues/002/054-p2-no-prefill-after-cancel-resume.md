---
number: "054"
id: PAY-CO-006
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 054 — No prefill after cancel / resume

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B8
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

GET returns `payer_name` / `payer_email` after Start persists them. `PayView` omits both. Inputs are `useState('')`. Cancel URL (Stripe/CHIP/Xendit) re-shows the form. `email_required` disables Pay until they retype, even though the **row** already has a usable mailbox and a second start with blank email would keep the stored value (host only writes non-whitespace).

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **10–19**, **303–319**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **274–285**, **130–138**.

## Reproduction

CHIP start, cancel from processor. Form empty. Pay grey until email retyped.

## Blast radius

Honest cancel path friction. Host is looser than the UI on retry.

## Suggested fix

On GET, if `payer_email` / `payer_name` present, prefill unless the user has already typed. Keep `usableEmail` on the prefilled value (placeholder should not prefill-enable). Document that this is not a login.

## Tests

- Missing: PayView types include payer fields; prefill from GET.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B8
