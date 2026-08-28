---
number: "012"
id: PAY-CHIP-001
severity: P0
status: resolved
source: plans/019-evals/06-rails-webhooks-fulfillment.md
head: "9f04ad58"
---

# 012 — CHIP paid join is metadata-only

- **Severity:** P0 (if metadata is stripped; P1 if CHIP always echoes)
- **Status:** open
- **Source:** `plans/019-evals/06-rails-webhooks-fulfillment.md` B7
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Handle already has a fallback join: if `parsed.CheckoutId` is empty, look up `checkouts` by `(OrgId, Provider, ProviderSessionId == parsed.HostedSessionId)`. **Only Razorpay sets `HostedSessionId`.** CHIP paid with stripped metadata → 400 `"checkout not found"`, **event not inserted**, CHIP retries forever, buyer paid, no `RCPT-`. Same class as 016 P0-C (Razorpay notes-only), now on CHIP.

Xendit invoice id, Billplz bill id, Stripe session id are in the same boat if metadata/`client_reference_id` is missing — Handle’s session join is dead for them too.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **101–107** — session join.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs` — paid path sets `CheckoutId` from metadata; does not set `HostedSessionId`.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` — stores `ProviderSessionId` = purchase id.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs`, `Billplz/BillplzWebhook.cs`, `Stripe/StripeWebhook.cs` — contrast Razorpay.
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayWebhook.cs` **85–107** — sets `HostedSessionId` (`plink_`).
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/PspParseResult.cs` — `HostedSessionId` field.

## Reproduction

CHIP `purchase.paid` (or equivalent) **without** metadata checkout id, with `id` = the stored `ProviderSessionId`. Expect today: 400 checkout not found, no event row, no receipt. CHIP retries.

Happy-path CHIP tests stamp metadata so they never see this.

## Blast radius

Buyer paid on CHIP, merchant has no Official Receipt, PSP retries until they give up. Lived CHIP payloads that omit custom metadata (dashboard-created purchases, some webhook versions) are the risk. If CHIP always echoes what `ChipHosted` sent, this is latent until a payload without metadata arrives.

## Suggested fix

Set `HostedSessionId = purchaseId` on CHIP paid (`ProviderRef` already is). Same one-liner for Xendit `invoiceId`, Billplz `billId`, Stripe `session.Id`. Parsers stay static. No interface method. No factory.

Keep 400 when **both** checkout id and session id miss. Do not invent a checkout.

## Tests

- Missing: CHIP paid **without** metadata checkout_id still pays via `purch_1` == `ProviderSessionId`. One `RCPT-`. Replay duplicate.
- Same shape later for Xendit/Billplz/Stripe if those parsers stay empty on `HostedSessionId`.

## Source reports

- `plans/019-evals/06-rails-webhooks-fulfillment.md` §B7
- `plans/019-evals/00-evaluation.md` §5 P1 CHIP join
