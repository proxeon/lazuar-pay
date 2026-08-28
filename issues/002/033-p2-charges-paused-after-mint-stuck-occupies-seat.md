---
number: "033"
id: PAY-OCC-009
severity: P2
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 033 — Charges-paused after mint stuck-occupies the seat

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B9
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Mint checks paused **before** insert. If the org is paused **later**: Start 403, webhook 409 `ChargesPausedException`, child remains `open`, seat held. GET still shows the pay form for the slot owner (`GetLink` does not read `ChargesPaused`). They cannot pay; nobody else can take the seat.

Depends on 011 actually setting the flag.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **121–125**, **213–216**.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **126–130**, **161–164**.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **51–77** — GET no pause read.

## Reproduction

Start CHIP (open child). Then pause org (once 011 works). Same slot GET still “Pay”. Start 403. Second slot 409 full.

## Blast radius

Paused shops with in-flight starts. Seats frozen until unpause or 003 TTL.

## Suggested fix

On pause: expire `open` children **or** GET `status: "paused"` for everyone. 003 TTL also releases them. Do not fulfill paused (already 409).

## Tests

- Existing: start paused is 403 even with stored URL.
- Missing: occupancy after pause — remaining recovers **or** GET says paused, not full.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B9
