---
number: "013"
id: PAY-OCC-006
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 013 — Same-slot start race is a 500, not a resume

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B2 (also `05-payment-links-occupancy.md` B2)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Double-click / two tabs in the **same** browser share a `slot_key`. `MintOrResume` does `FirstOrDefault` by `(PaymentLinkId, SlotKey)` then insert. Concurrent same-slot starts: both miss, one insert wins, the other `SaveChanges` throws `DbUpdateException`. `Start` only catches `InvalidOperationException` and `StripeException` → **unhandled 500**.

The unique index exists **only on Npgsql**. On InMemory both inserts can succeed (no index) — tests would green a double seat if they ever ran parallel. Sequential `Same_slot_start_twice_does_not_take_two_seats` is green.

001’s parent lock also serializes this; still catch unique and resume so a 500 cannot leak.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **219–264** — lookup then insert; no catch.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **194–202** — catch list does not include `DbUpdateException`.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **43–48** — Npgsql-only unique.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` **147–172** — sequential same slot.

## Reproduction

Two concurrent `POST /start` with the **same** `slot_key` on Postgres. One 200 + redirect; the other 500.

## Blast radius

Double-tap Pay on a slow phone. Buyer sees a 500 instead of “Continue to processor”. Seat may already exist; they can retry and resume **if** they retry after the winner commits.

## Suggested fix

Catch unique violation, re-load the existing row, resume (same as the hit path). Put the unique index on **all** providers tests use, or stop using InMemory for occupancy. 001’s `FOR UPDATE` on the parent also serializes same-slot.

## Tests

- Missing: concurrent same-slot starts → both 200, same `redirect_url`, `taken_count = 1`, PSP HTTP 1. Postgres.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B2
- `plans/019-evals/05-payment-links-occupancy.md` §B2
