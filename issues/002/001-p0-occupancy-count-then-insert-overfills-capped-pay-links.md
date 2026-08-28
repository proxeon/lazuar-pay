---
number: "001"
id: PAY-OCC-001
severity: P0
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 001 — Occupancy count-then-insert overfills capped pay links

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` (also `01-pay-host-seams.md` B1, `10-honesty-bugs-gaps.md` P0-1)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Capped payment links (`max_payers = 1` default, or any N) are not a real cap under concurrency. `MintOrResume` counts occupying children (`open` or `paid`), then inserts a new `open` checkout, with **no transaction**, **no `SELECT … FOR UPDATE` on `payment_links`**, and **no unique constraint on the Nth seat**.

Two browsers with two `slot_key`s hitting `POST /v1/pay/{token}/start` at the same time both see `taken = 0` (or `N-1`), both insert, both call `CreateHostedUrlAsync`. Unique `(PaymentLinkId, SlotKey)` does not conflict. Postgres accepts both. Two hosted sessions, two Plane B fulfills, two Official Receipts on a “1 person only” link.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **236–264** — `CountAsync` then `Checkouts.Add` then `SaveChangesAsync`. No lock.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` **5–12** — `CountsTowardCapacity` / `IsFull`. Helpers only; no persistence.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **43–48** — unique `(PaymentLinkId, SlotKey)` **only when provider is Npgsql**. Caps `N` are not constrained.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs` **69–75** — filtered unique index; no occupancy check.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` **121–145** — `Two_people_can_pay_a_link_of_two` is **sequential**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` — InMemory; filtered unique index is not installed.

## Reproduction

1. Writer mints a pay link `max_payers: 1`, provider `stripe` (or `chip`).
2. Two machines (two `slot_key`s) `POST /v1/pay/{token}/start` at the same millisecond.
3. Both 200 with `redirect_url`. Both complete the PSP.
4. Two `RCPT-` rows. List shows `taken_count = 2` on `max_payers = 1`.

Hermetic sequential tests stay green.

## Blast radius

Every capped pay link on a live rail. Money: extra processor charges, extra Official Receipts. Test rail makes it worse because start **is** fulfill (see 006). Unlimited links are not this bug.

## Suggested fix

Serialize occupancy on the **parent** row in one transaction:

1. `SELECT … FROM payment_links WHERE id = $id FOR UPDATE` (or `UPDATE … WHERE remaining > 0`).
2. Re-count occupying children.
3. Insert the child **or** 409 `"This pay link is full"`.
4. Commit. Call PSP HTTP **after** the seat is committed.

Catch unique/capacity violations → 409, never 500. Do not “fix” this only in the Vite `slot_key` generator — two browsers are enough.

Alternatively a seat table `UNIQUE (payment_link_id, seat_n)` with `seat_n` in `1..max`.

Do not add MediatR, an outbox, or `IEnumerable<IHostedRail>`.

## Tests

- Existing: `Two_people_can_pay_a_link_of_two` (sequential). `Same_slot_start_twice_does_not_take_two_seats` (CHIP, sequential).
- Missing: two concurrent `POST /start`, `max_payers=1`, different slots, FakePspHandler; **documents ≤ 1**; **PSP HTTP ≤ 1**. InMemory cannot prove `FOR UPDATE` — Postgres (Testcontainers) or an explicit lock seam in Testing.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B1
- `plans/019-evals/01-pay-host-seams.md` §B1
- `plans/019-evals/10-honesty-bugs-gaps.md` §P0-1
- `plans/019-evals/00-evaluation.md` §5 item 1
