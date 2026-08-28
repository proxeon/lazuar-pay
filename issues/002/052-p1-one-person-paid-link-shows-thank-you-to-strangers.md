---
number: "052"
id: PAY-CO-004
severity: P1
status: resolved
source: plans/019-evals/03-checkout-frontend.md
head: "9f04ad58"
---

# 052 — One-person paid link shows “Payment received” to strangers

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/03-checkout-frontend.md` B5 (also `05-payment-links-occupancy.md` B5)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`GetLink`: if not mine and `MaxPayers == 1 && paid >= 1`, return `CheckoutView` of the **paid row**. SPA paints “Payment received” / “Thank you.” `PaymentLinkTests.One_person_link_shows_paid_without_slot_after_pay` locks GET-without-slot as `paid` — that is the original payer returning without a key, **and** every other browser.

Max>1 full correctly paints “Link is full”. Max=1 is the dishonest special case.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **66–71**.
- `apps/lazuar-pay-checkout/src/App.tsx` **188–207**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` **191–202**.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **98–101** — remaps full+paid → paid in the **staff** table (different surface).

## Reproduction

Pay a 1-person Test/CHIP link. Forward the WhatsApp URL to a second phone. Second phone: “Thank you. The merchant will see an Official Receipt…”

## Blast radius

Privacy / honesty. Stranger thinks they paid. Merchant support: “I didn’t pay but it said thank you.”

## Suggested fix

Host: if `mine` is null and paid, return `LinkView` with `status: "paid"` **without** implying this browser is the payer, **or** `status: "already_paid"`. SPA: if paid but `started` is false **and** slot did not match (`mine: false`), paint “This link is already paid” / not “Thank you.” Keep the original payer’s slot → “Payment received.” Test both.

The existing test that GET-without-slot is `paid` must be split (payer vs stranger).

## Tests

- Existing test **locks the lie** for GET without slot.
- Missing: slot of payer vs a fresh slot.

## Source reports

- `plans/019-evals/03-checkout-frontend.md` §B5
- `plans/019-evals/05-payment-links-occupancy.md` §B5
