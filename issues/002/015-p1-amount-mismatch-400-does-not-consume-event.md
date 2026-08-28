---
number: "015"
id: PAY-WH-001
severity: P1
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 015 — Amount/currency mismatch 400 does not consume the event

- **Severity:** P1 (P0 if **our** unit map is wrong on a lived payload)
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B8 (016 P0-D residual)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Mismatch 400 happens **before** insert of `psp_webhook_events`. Fail-closed against a hostile payload is correct. If **our parser** invented the mismatch (CHIP `total` treated as minor when the dashboard sent major, etc.), Plane B never consumes the event id, PSP retries until they give up, buyer paid, no receipt.

016 P0-D skip-currency / default-MYR holes on the five PSPs are largely **FIXED** (parsers throw on missing currency). The consume policy is unchanged. Stripe has a mismatch test; CHIP / Billplz / Xendit / Razorpay **do not**.

Do not “fix” this by inserting a poison event on every 400 — that would hide a later parser correction.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **132–141** — mismatch 400, no insert.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` — `Amount_mismatch_does_not_mint_receipt` (Stripe-shaped).
- Rail parsers: `Rails/Chip/ChipWebhook.cs`, `Billplz/BillplzWebhook.cs`, `Xendit/XenditWebhook.cs`, `Razorpay/RazorpayWebhook.cs`, `Stripe/StripeWebhook.cs` — unit comments + fixtures.

## Reproduction

CHIP paid with `total: 10` if the host expects 1000 for RM 10. 400 forever. Checkout stays `open`.

## Blast radius

Lived unit mistakes. Hostile PSP payloads are correctly retried (or they give up) without a false receipt. The hole is **our** map, not theirs.

## Suggested fix

Keep 400 + no insert. Add one mismatch fixture **per name** (F00). Pin lived JSON in comments / FakePsp bodies: CHIP `total: 1000` for RM10, Xendit `paid_amount: 10` major, Billplz `paid_amount=1000` sen, Razorpay `amount: 1000` minor, Stripe `amount_total: 1000`. Do not divide CHIP by 100 because Hub did.

016 P0-D “do not call units production-proven” stays until a lived payload is checked in.

## Tests

- Existing: Stripe-shaped amount mismatch does not mint receipt.
- Missing: one mismatch method per CHIP / Billplz / Xendit / Razorpay / Test (007). Event row absent. Checkout `open`.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B8
- `plans/019-evals/10-honesty-bugs-gaps.md` §016 P0-D
- `plans/019-evals/09-tests-inventory.md` F00 residual
