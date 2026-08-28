---
number: "008"
id: PAY-TEST-003
severity: P1
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 008 — Test webhook mints a new EventId when `id` is missing

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B3
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

If JSON has no string `id`, `TestWebhook.Parse` sets `eventId = "test:" + Guid.NewGuid()`. Every retry is a new grain. After the first pay, Fulfillment no-ops because status ≠ `open`, but each retry **inserts another** `psp_webhook_events` row and returns `{ ok: true }`. Duplicate detection never fires.

Same class as old Razorpay “fallback id is the payment id / a constant” holes; Test invents a unique id on purpose.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs` **24–30** — Guid fallback.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **90–93** — `FindAsync([orgId, name, parsed.EventId])`.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **81–85** — PK `(OrgId, Provider, EventId)`.

## Reproduction

POST twice with `{"checkout_id":"<id>"}` and **no** `id`. Two event rows, one receipt (second fulfill no-ops). `{ ok: true }` both times, never `{ duplicate: true }`.

## Blast radius

Noise in `psp_webhook_events`, broken replay semantics, and (with 006) an unbounded insert DoS on the Test path. Not a second receipt by itself (fulfill guards `open`).

## Suggested fix

Missing `id` → `PspVerifyException("missing event id")`, same as Razorpay failed-without-id. Do not mint a Guid. Require `id` together with 007’s amount/currency.

## Tests

- Missing: Test webhook without `id` is 400. Replay of the **same** `id` is `{ duplicate: true }` after paid.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B3
