---
number: "010"
id: PAY-FULFILL-001
severity: P0
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 010 — Concurrent fulfill can double-book one checkout / collide `RCPT-`

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B5 (also `10-honesty-bugs-gaps.md` P1-5)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Fulfillment’s only seat lock is `if (checkout.Status != "open") return;` in memory, then `SaveChanges`. Two concurrent paid **grains** (two event ids for the same checkout — CHIP two purchases after 014’s persist miss, or Stripe completed + a badly added second type) can both see `open`, both insert `charges`, both bump `document_sequences.LastN`, both insert `documents`.

- `charges` PK is `Id` only.
- `documents` PK is `Id` only.
- `document_sequences` PK is `(OrgId, Series, YearMyt)` with `LastN` mutated in place — two concurrent fulfills can mint the **same** `RCPT-…` number.

Webhook unique `(OrgId, Provider, EventId)` stops **the same** PSP delivery, not two different event ids. The TX around insert+fulfill does **not** `SELECT … FOR UPDATE` the checkout row. InMemory ignores transactions (077).

This is **per checkout** double-book, not 001’s per-link over-admit. Both can happen on the same money.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` **26–37**, **102–120** — status guard; sequence `LastN += 1`; `RCPT-{year}-{LastN}`.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **143–154** — BeginTransaction, add event, fulfill, commit. No checkout CAS.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **86–117** — charges/documents/sequences keys.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs` — original table shapes.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` — fulfill-throw probe; not concurrent double grain.

## Reproduction

Two threads (or two event ids) call `FulfillPaidAsync` for the same `open` checkout. On Postgres without a CAS, two charges and either two receipts or two rows with the same `Number`.

## Blast radius

Official Receipt identity and cash/revenue lines. Merchants reconciling `RCPT-` uniqueness. Kernel clients that key fulfillment on receipt number.

## Suggested fix

Without a cathedral:

1. Unique index `charges (CheckoutId)`.
2. Unique index `documents (OrgId, Number)` (and optionally `(CheckoutId)`).
3. In `FulfillPaidAsync`, `UPDATE checkouts SET status='paid' WHERE id=@id AND status='open'` and proceed only if 1 row; else return. Keep that inside the existing `BeginTransaction`. Catch unique on charges as already-paid (HTTP 200 ok/duplicate).
4. Sequence increment in the same TX is enough **if** the checkout CAS holds; unique on number is the belt.

Do not add an outbox or MediatR.

## Tests

- Missing: two concurrent fulfills → distinct `RCPT-` numbers **or** one receipt + duplicate; unique index trip. Postgres, not InMemory.
- Existing `Fulfill_throw_returns_5xx_event_not_committed_retry_pays` does not cover two successes.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B5
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-5
- `plans/019-evals/09-tests-inventory.md` InMemory limitations
