---
number: "032"
id: PAY-OCC-008
severity: P2
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 032 — Child checkout public tokens are a second pay URL

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B14 (also `05-payment-links-occupancy.md` B10)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Children get a 64-hex `PublicToken`. Public GET tries payment_links first, then checkouts. `POST /v1/pay/{childToken}/start` takes the checkout branch: **no** `slot_key`, **no** occupancy re-check (seat already taken). GET child token returns `CheckoutView` **without** `remaining` / `max_payers`.

Bookmarking the child token bypasses the link’s GET `full` view. Combined with 001, extra children each have a working URL.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **27–47**, **108–128**, **253**.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` `GetByPublicTokenAsync`.

## Reproduction

Start a link slot. Read the child `public_token` from GET org checkouts (031). Open `/c/{childToken}`. Occupancy fields missing. Start without `slot_key` proceeds on the standalone branch.

## Blast radius

Leaked child tokens; confusing buyer URLs. Not a new over-admit by itself (seat already exists).

## Suggested fix

Do not issue child public tokens (pay only via link token + slot), **or** treat child tokens as aliases that still load parent occupancy. Document the namespace: link tokens and checkout tokens share `/v1/pay/{token}`.

## Tests

- Missing: GET child token includes remaining **or** 404s in favor of the link token.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B14
- `plans/019-evals/05-payment-links-occupancy.md` §B10
