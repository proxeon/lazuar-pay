---
number: "002"
id: PAY-OCC-002
severity: P0
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 002 — Seat reserved before start can succeed (email/PSP 400 occupies)

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B3 (also `01-pay-host-seams.md`)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`MintOrResume` commits an `open` child checkout **before** the rest of `Start` can fail. Occupancy then counts that row.

Fails that happen **after** the insert:

- email required (CHIP / Billplz / Xendit / Razorpay — `PayProviders.RequiresEmail` is true for all except Stripe and Test)
- rail not configured / 503
- Billplz `"callback base not public"`
- Stripe rejected the org key

A raw `POST /v1/pay/{token}/start` with a valid `slot_key` and empty email on CHIP returns **400 after occupying the only seat**. Other buyers GET `full`. The 400 caller can retry **the same slot**; a new browser cannot.

The SPA blocks empty email client-side, so the hosted page hides this. The public API does not.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **96–148** — `MintOrResume` then copy name/email then `RequiresEmail` 400.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **205–264** — insert `Status = "open"` and `SaveChangesAsync` before the caller continues.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs` **35–36** — `RequiresEmail` is not Stripe and not Test.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs` — usable-email rule (placeholder refused).
- `apps/lazuar-pay-checkout/src/App.tsx` **119–122** — SPA blocks click; does not protect the API.

## Reproduction

1. Mint a CHIP pay link `max_payers: 1`.
2. `POST /v1/pay/{token}/start` with `{ "slot_key": "slot-aaaa-1" }` and no email.
3. Host 400 `"email is required"`.
4. `GET /v1/pay/{token}` with a **different** slot → `status: "full"`, remaining 0, `paid_count` 0.

Same shape: valid email, Billplz with localhost `Pay:PublicBaseUrl` → 400 after the seat is taken.

## Blast radius

Default capacity is **one person**. One bad start (missing email, bad callback base, 503 rail) closes the link with RM 0 collected. Merchant table shows `1 / 1` and `full` while `paid_count = 0`.

## Suggested fix

Do not persist a seat until Start can succeed:

1. Validate email / paused / provider **before** insert, **or**
2. Insert inside the same transaction as the first successful persist of `PspRedirectUrl`, and roll back the child on 400/503, **or**
3. Mark the failed child `expired` immediately so occupancy does not count it (see 003 for TTL of abandoned starts).

Same-slot retry must still resume. New slots must not see a ghost `open` row after a 400.

Do not “fix” this only in the SPA.

## Tests

- Existing: `Start_link_without_slot_key_is_400` (never mints). CHIP start tests use a usable email.
- Missing: CHIP (or Billplz) `max_payers=1`, start **without** email → 400 **and** second slot still 200 / remaining 1. Billplz localhost callback 400 **and** seat free.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B3
- `plans/019-evals/01-pay-host-seams.md` (start after mint)
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-1 (related occupancy grain)
