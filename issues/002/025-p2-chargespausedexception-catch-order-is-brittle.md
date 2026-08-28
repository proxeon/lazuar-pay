---
number: "025"
id: PAY-WH-002
severity: P2
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 025 — `ChargesPausedException` catch order is brittle

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B10
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`ChargesPausedException` **is** an `InvalidOperationException`. Handle catches `ChargesPausedException` **before** `InvalidOperationException`, so today pause is HTTP 409 and the event rolls back (not consumed — money-safe). If someone reorders catches, pause becomes HTTP 500 `fulfill failed`. Still not consumed; Stripe retries; worse ops.

Not a live mis-book. A footgun next to 011 (pause never sets the flag on product One).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` **32–35**, **133**.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **161–170**.

## Reproduction

Read the catch blocks. Reorder in a branch; pause tests would 500.

## Blast radius

Future refactor. Live path is correct **if** `ChargesPaused` is set (011).

## Suggested fix

Do not make pause a 200. Prefer a dedicated exception type that does **not** inherit `InvalidOperationException`, or catch pause by type in a filter that cannot be shadowed. Add a comment at the catch. Keep 409 + rollback + no event consume.

## Tests

- Existing: pause 409 without consuming paid event id (`Start_paused_is_403_even_with_stored_url` / webhook pause tests).
- Missing: not required if the type stops inheriting `InvalidOperationException`.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B10
