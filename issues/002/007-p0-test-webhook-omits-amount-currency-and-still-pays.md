---
number: "007"
id: PAY-TEST-002
severity: P0
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 007 — Test webhook omits amount and currency and still pays

- **Severity:** P0
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B2
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Handle only mismatch-checks amount/currency when the parser **set** the fields. `TestWebhook.Parse` leaves both null when `amount_total` / `currency` are absent. Payload `{"id":"x","checkout_id":"<open test checkout>"}` books whatever the row says (e.g. RM 10).

The five real rails throw or ignore when currency is missing. Test is the skip.

Combined with 006 this is unauthenticated arbitrary fulfill. Fixing 006 without requiring amount still lets a signed (or local) caller pay a Test checkout without proving the amount.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs` **38–47** — optional `amount_total` / `currency`.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **132–141** — skip check when parsed fields are null.
- `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs` — `ToMinor` / `TryNormalizeCurrency` used by other parsers.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs` **48–54** — fixture **includes** amount+currency, so it does not prove the omit path.

## Reproduction

Open Test checkout for RM 10. POST:

```json
{"id":"evt_omit","checkout_id":"<id>"}
```

Expect today: 200, Official Receipt for 10.00 MYR.

## Blast radius

Test checkouts in any env where Test webhooks are accepted (006). Forged payload need not know the amount.

## Suggested fix

In `TestWebhook.Parse`, **require** `id`, `checkout_id`, `amount_total`, `currency`; throw `PspVerifyException` when missing — same fail-closed as Stripe/CHIP currency. Keep Handle’s null-skip only for parsers that truly have no amount (none of the real five should).

Do not “fix” this by inserting a poison event on mismatch (015). Require the fields so mismatch can fire.

## Tests

- Missing: Test webhook without `amount_total` → 400, no document. Without `currency` → 400. With wrong minor units → 400 (then 015’s consume policy).
- Update `Webhook_pays_open_test_checkout` to keep sending amount+currency.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B2
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-2 parser quote
