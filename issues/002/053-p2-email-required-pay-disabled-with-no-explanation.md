---
number: "053"
id: PAY-CO-005
severity: P2
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 053 — Email-required Pay is disabled with no explanation

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`emailBlocked` disables the button. `startPay`’s `'email is required'` only runs if they click, which a disabled button cannot. Placeholder `customer@example.com` is now blocked (016 hole closed) but the Label is still “Email” with no `*`, no `required`, no helper “CHIP needs an email (not customer@example.com).”

Buyer types the Hub placeholder or leaves the box empty and stares at a grey Pay.

## Related files

- `apps/lazuar-pay-checkout/src/App.tsx` **280–329**, **339–342**.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs` **35–36**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`

## Reproduction

CHIP pay link. Empty email. Pay disabled. No alert.

## Blast radius

UX on every non-Stripe, non-Test link. Host 400 is never reached for those cases (good) but the UI is mute.

## Suggested fix

When `email_required`, mark the Label, `aria-required`, `required` on the input, helper text. If value is the placeholder, `role="alert"` “Use your real email.” Keep `usableEmail` matching `BuyerEmail.IsUsable`. Do not RFC-5322-theatre beyond `type="email"` unless the rails do.

## Tests

- Existing: locks `customer@example.com` / `usableEmail`.
- Missing: helper copy when `email_required`.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B7
