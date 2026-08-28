---
number: "014"
id: PAY-START-001
severity: P1
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 014 — PSP HTTP then persist can mint a second hosted session

- **Severity:** P1 (016 P0-A residual)
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B6 (also `10-honesty-bugs-gaps.md` P1-3 / P1-10)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Start creates the processor session **then** `SaveChanges` the `PspRedirectUrl` / `ProviderSessionId`. A comment in source admits it. If persist throws after CHIP/Billplz/Xendit/Razorpay already created a session, retry calls `CreateHostedUrlAsync` again. Stored-URL short-circuit only helps when the first SaveChanges **worked**.

Stripe has an Idempotency-Key on create (`StripeHosted`) — belt for Stripe only. 016 P0-A “every click mints a session” is **FIXED** for the happy replay (`PspRedirectUrl` set). This is the persist-failure cousin.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **151–155** — return stored URL (the 016 fix).
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **168–190** — comment + PSP HTTP then persist.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs` — idempotency key (contrast).
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`, `Billplz/BillplzHosted.cs`, `Xendit/XenditHosted.cs`, `Razorpay/RazorpayHosted.cs`.
- Tests: `Start_twice_returns_same_url_without_second_psp_http` — second call **after** persist succeeded.

## Reproduction

Inject SaveChanges failure after FakePsp success on CHIP start. Retry. `FakePspHandler.SendCount` becomes 2. Two CHIP purchases; one or two receipts depending on 010 / 012.

## Blast radius

CHIP / Billplz / Xendit / Razorpay under DB blip or unique violation after HTTP. Buyer can be charged twice. Stripe mitigated. Test rail does not HTTP a PSP.

## Suggested fix

- If `ProviderSessionId` is non-null, return stored URL (or 409) **even when** `PspRedirectUrl` is empty; do not call create again.
- Prefer persist-before-HTTP of a pending row, or send each rail’s idempotency header (Xendit `Idempotency-key`; CHIP if any; Razorpay none → persist first).
- Do not add a factory.

## Tests

- Missing: SaveChanges-fail after FakePsp success, retry send count 1 (CHIP). Name it after the comment in `PublicPayEndpoints`.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B6
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-3 §P1-10
