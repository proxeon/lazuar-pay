---
number: "019"
id: PAY-OCC-007
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 019 — Public `slot_key` is a client-supplied seat; capped links can be griefed

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`NormalizeSlotKey` accepts any trimmed 8–128 character string. `MintOrResume` requires it on link start. There is no rate limit, no cookie binding, no signed slot. The public token **is** the capability. Anyone who knows the pay URL can `POST /start` with `slot-aaaa-01`, `slot-aaaa-02`, … until `IsFull`.

For `max_payers = 1` (default), one unsolicited start fills the link (003 if CHIP, 006 if Test). Unlimited Test links: unbounded children and unbounded receipts.

The SPA mints a UUID in `localStorage` (051). That is a courtesy, not a seat lock.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **219–223**, **324–332** — require / normalize slot.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` **204–215** — without slot_key is 400 only.
- `apps/lazuar-pay-checkout/src/App.tsx` **21–32** — client UUID.

## Reproduction

Mint max=1 CHIP link. From curl, start with `slot-grief-01` and a usable email. Link is full for everyone else without a payment.

## Blast radius

Shared WhatsApp URLs. Griefing and accidental double-open. Combined with 001, grief + race.

## Suggested fix

Server-mint the slot (GET returns a one-time `slot_key` bound to a reservation created under 001’s lock), **or** rate-limit `POST /v1/pay/{token}/start` per token/IP. Cookie is optional; do not treat `localStorage` as auth. For Test, also 006.

Do not require One login on checkout.

## Tests

- Missing: N starts with distinct slots on max=1 fill then 409. Rate-limit test if you add one. GET-minted slot cannot be guessed as a second seat without the returned key.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B7
- `plans/019-evals/05-payment-links-occupancy.md` unique payer
