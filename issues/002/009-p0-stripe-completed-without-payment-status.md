---
number: "009"
id: PAY-STRIPE-001
severity: P0
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 009 — Stripe `checkout.session.completed` is treated as paid without `payment_status`

- **Severity:** P0 (method-mix dependent)
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Live ignore is only `mode == "setup"` or `AmountTotal` null/0. The parser never reads `session.PaymentStatus`. Stripe can emit `checkout.session.completed` with `payment_status=unpaid` for delayed methods (bank debit, some wallets), then `checkout.session.async_payment_succeeded`.

Pay would **fulfill the unpaid completed** (amount still matches) and **ignore** the later succeeded because `Type` is not `checkout.session.completed`. Hermetic fixtures inject `"payment_status":"paid"` but the C# never looks at it.

Hub also skipped `payment_status`; 016 listed it as steal-next. Still next.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs` **46–75** — type filter; setup/zero ignore; no `PaymentStatus`.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` — fulfill whatever parse returns as not ignored.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs` — paid fixtures include `"payment_status":"paid"` as unused JSON.
- Hub judgment (do not copy): `StripeGatewayAdapter` also skipped this belt.

## Reproduction

Deliver a Stripe-shaped event `checkout.session.completed` with `amount_total` matching, `mode=payment`, `payment_status=unpaid`, `client_reference_id` = checkout id. Host mints Official Receipt. Follow-up `checkout.session.async_payment_succeeded` is ignored (`IgnoreReason` = event type).

Cards that set `payment_status=paid` on completed are fine (happy path tests).

## Blast radius

Delayed Stripe methods on a BYOK account. Book unpaid, or never book the real paid follow-up if you “fix” completed by ignoring unpaid **without** adding `async_payment_succeeded`. Cards/wallets that complete as paid in one event are unaffected.

## Suggested fix

- Ignore unless `session.PaymentStatus` is `paid` (or `no_payment_required` if you ever charge zero — already ignored via amount 0).
- Add a second **paid** type arm: `checkout.session.async_payment_succeeded` with the same amount/currency/`client_reference_id` rules.
- Do **not** add `payment_intent.succeeded` (second grain for the same Checkout Session). Event id stays Stripe `evt_`.

## Tests

- Missing: unpaid `checkout.session.completed` → `{ ignored: ... }`, checkout still `open`, no document.
- Missing: `async_payment_succeeded` after unpaid completed → one `RCPT-`.
- Keep setup-not-paid / zero-amount ignores.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B4
- `plans/019-evals/00-evaluation.md` §5 item 4
