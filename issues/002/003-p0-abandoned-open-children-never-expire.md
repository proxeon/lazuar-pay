---
number: "003"
id: PAY-OCC-003
severity: P0
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 003 — Abandoned `open` children never expire

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B4 (also `01-pay-host-seams.md` B3, `03-checkout-frontend.md` B6)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Occupancy counts `open` **or** `paid`. Nothing in the Pay host writes `status = "expired"`. Start and `MintOrResume` only **read** `"paid" or "expired"` as terminal. Cancel URL bounces the buyer to `/c/{linkToken}` without changing status. There is no worker, no TTL, no merchant “release seat”.

Buyer A clicks Pay, hops to CHIP, closes the tab. Seat stays `open` with a `PspRedirectUrl`. Remaining drops. Buyer B sees “Link is full” / 409 while **nobody has paid**. The checkout SPA still has an expired Card; it never appears because the host never emits `expired`.

Test rail hides this: start fulfills immediately, so `open` is brief.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` **5–6** — `CountsTowardCapacity` = `"open" or "paid"`.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **116–119**, **228–230** — refuse start when already `paid` or `expired`.
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` **26–29**, **37** — writes `paid` only.
- `apps/lazuar-pay-checkout/src/App.tsx` **210–224** — expired Card (costume until the host writes the status).
- Grep of `apps/lazuar-pay/src` for `status = "expired"` / `"expired"` writers: none.

## Reproduction

1. Mint a CHIP (or Stripe) pay link `max_payers: 1`.
2. Start with a usable email. Do **not** complete the processor page. Close the tab.
3. Second phone opens the same link (new `slot_key`).
4. GET `status: "full"`, SPA “Link is full”. `paid_count = 0`, `taken_count = 1`.

## Blast radius

Default “1 person only” links on every live rail. One abandoned PSP tab closes the product. Unlimited links still accumulate ghost `open` children (lists, journals later if they somehow pay).

## Suggested fix

Product choice, then code (do not leave A in code and B in the dialog — see 004):

- **Recommended:** occupy `open` as a **reservation with a TTL**. Expire unpaid `open` children older than N minutes (`expired`). Late webhook after expire: 409 / ignore, do not pay a released seat. Occupancy then recovers.
- Or occupy only `paid` (two people can both reach the PSP on max=1 — usually wrong).
- Or let merchant / buyer release a seat.

Whatever you pick, **write `expired` for real**. Do not have the SPA invent expiry. Do not expire `paid`.

## Tests

- Existing: occupancy tests use Test rail (start = paid). CHIP `Same_slot_start_twice` observes `open` but does not walk away.
- Missing: CHIP start without webhook on `max_payers=1` → after TTL, second slot 200. SPA expired Card appears when host says `expired`. Late webhook on expired child does not mint a second `RCPT-` if the seat was reissued (define the rule in the test name).

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B4
- `plans/019-evals/01-pay-host-seams.md` §B3
- `plans/019-evals/03-checkout-frontend.md` §B6
