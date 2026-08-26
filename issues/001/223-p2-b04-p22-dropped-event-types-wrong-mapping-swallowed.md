---
number: "223"
id: B04-P22
severity: P2
status: resolved
resolved_branch: fix/223-leftover-event-maps
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 223 — B04-P22 — Dropped event types (wrong mapping / swallowed)

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/223-leftover-event-maps`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P22 — P2 — Dropped event types (wrong mapping / swallowed)

| Source | Mapped? | Effect |
|--------|---------|--------|
| CHIP `purchase.preauthorized` | passthrough | B04-P01 |
| CHIP `payment.refunded` | passthrough | B04-P13 |
| Stripe `charge.refunded` / `refund.*` | passthrough | B04-P13 |
| Stripe `setup_intent.succeeded` | passthrough | B04-P20 |
| Stripe `checkout.session.async_payment_*` | passthrough | latent if APMs added |
| Stripe `charge.dispute.created` without `Dispute` object | passthrough | lost dispute |
| Razorpay `payment.authorized` | passthrough | unpaid if no auto-capture |
| Razorpay `refund.*` | passthrough | B04-P13 |
| Xendit `PENDING` | passthrough | ignored |
| Billplz any non-paid | `PAYMENT_FAILED` | B04-P08 if late |

Parse exceptions in CHIP / Billplz / Razorpay / Xendit are caught and returned `Verified=false` (retry). Stripe non-`StripeException` is not caught (500). Handler does not distinguish “bad signature” from “malformed JSON we already verified” — both 500.

Dispute vs refund vs fail: Stripe dispute is the only inbound dispute. It is **not** mapped as a refund (correct; `e18edbe` stopped Commerce booking chargebacks as refunds — other slice). No rail maps a chargeback as `PAYMENT_FAILED`.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`ProcessGatewayWebhookCommandHandler` only persists and publishes when `EventType` is one of `PAYMENT_COMPLETED`, `PAYMENT_FAILED`, `DISPUTE_CREATED`, `DISPUTE_CLOSED`, `REFUND_COMPLETED`. Everything else is a verified ACK 200 with no log. Adapters therefore “drop” money-adjacent processor events by returning the raw processor type (passthrough). The 17 Aug audit table listed CHIP preauthorized / CHIP `payment.refunded` / Stripe refunds / Stripe `setup_intent.succeeded` / Stripe async payment / Stripe dispute without a `Dispute` object / Razorpay `payment.authorized` / Razorpay refunds / Xendit `PENDING` / Billplz any non-paid as `PAYMENT_FAILED`. Parse exceptions on CHIP / Billplz / Razorpay / Xendit still become `Verified=false` (500 + retry). Stripe still only catches `StripeException`. The handler still does not distinguish a bad signature from malformed JSON that already verified.

### Still present?
**PARTIAL**

The handler allow-list grew (065 / 085 / dispute-close work):

```83:90:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
        if (parsedResult.EventType != "PAYMENT_COMPLETED"
            && parsedResult.EventType != "DISPUTE_CREATED"
            && parsedResult.EventType != "DISPUTE_CLOSED"
            && parsedResult.EventType != "PAYMENT_FAILED"
            && parsedResult.EventType != "REFUND_COMPLETED")
        {
            return;
        }
```

| Source | Now | Evidence |
|--------|-----|----------|
| CHIP `purchase.preauthorized` + recurring token | **mapped** `PAYMENT_COMPLETED` (005) | `ChipCollectGatewayAdapter.cs:156-161`; test `ParseWebhook_PreauthorizedRecurringToken_IsPaymentCompletedWithVault` |
| CHIP `purchase.preauthorized` auth-hold (no token) | still passthrough | `ParseWebhook_PreauthorizedAuthHold_IsNotPaymentCompleted` **locks** this |
| CHIP `payment.refunded` | still passthrough (no CHIP refund mapper) | else-branch `ChipCollectGatewayAdapter.cs:167-170`; still subscribed at `UpdatePaymentConfigCommandHandler.cs:133` |
| Stripe `charge.refunded` / `refund.*` succeeded | **mapped** `REFUND_COMPLETED` (085) | `TryMapRefundCompleted` `StripeGatewayAdapter.cs:293-294, 410-456`; test `ParseWebhook_RefundUpdatedSucceeded_IsRefundCompleted` |
| Stripe `refund.updated` pending | passthrough (intentional after 070/085) | `ParseWebhook_RefundUpdatedPending_IsNotCompleted` |
| Stripe `setup_intent.succeeded` | still passthrough | falls through to `StripeGatewayAdapter.cs:296`; see 221 |
| Stripe `checkout.session.async_payment_*` | still passthrough | same line 296; no test |
| Stripe `charge.dispute.created` **with** `Dispute` | **mapped** (created/updated/closed) | `StripeGatewayAdapter.cs:233-291` |
| Stripe `charge.dispute.created` without `Dispute` object | still passthrough | `is Dispute` guard fails → line 296 |
| Razorpay `payment.authorized` | still passthrough | only `payment.captured` completes (`RazorpayGatewayAdapter.cs:77-79`) |
| Razorpay `refund.*` | still passthrough | no refund mapper |
| Razorpay `invoice.expired` | **ignored** (not `PAYMENT_FAILED`) after 069 | `ParseWebhook_InvoiceExpired_IsIgnoredNotPaymentFailed` |
| Xendit `PENDING` | still passthrough | `MapStatus` returns null (`XenditGatewayAdapter.cs:415-433`) |
| Billplz any non-paid | still `PAYMENT_FAILED` | `BillplzGatewayAdapter.cs:232-234`; test `ParseWebhook_Unpaid_IsPaymentFailed_WithBillId`. Late fail-after-pay is now ignored at the handler (065) |

Parse: CHIP/Billplz/Razorpay/Xendit outer `catch` → `Verified=false`. Stripe `catch (StripeException)` only (`StripeGatewayAdapter.cs:298-302`) — non-`StripeException` still 500s the endpoint. `!Verified` still throws (`ProcessGatewayWebhookCommandHandler.cs:78-80`) for both bad HMAC and “missing currency / missing id” after a good signature (072 fail-closed). `Handle_UnknownEventType_Returns_NoLog_NoPublish` still locks swallow of `charge.succeeded`.

### Related files
- `apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` — allow-list + 500 on `!Verified`.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — refund + dispute maps; setup/async still raw type.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — preauthorized token path; refunds unmapped.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` — captured/failed only.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs` — `MapStatus`.
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` — unpaid → failed.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ProcessGatewayWebhookCommandHandlerTests.cs` — `Handle_RefundCompleted_Publishes_GatewayRefundCompleted`, `Handle_UnknownEventType_Returns_NoLog_NoPublish`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/{Stripe,ChipCollect,Razorpay,Billplz,Xendit}GatewayAdapterTests.cs`.

### Tests
- Existing tests that touch this path: listed in the table. `Handle_UnknownEventType_Returns_NoLog_NoPublish` (`charge.succeeded`). CHIP `ParseWebhook_PreauthorizedAuthHold_IsNotPaymentCompleted` locks the money-safe drop. Stripe refund succeeded/pending. Razorpay invoice.expired ignored. Billplz unpaid = failed.
- Whether any test would fail if the **remaining** holes are still there: **no** — several tests **assert** the remaining drops.
- What a first regression test should assert (pick the highest-value leftover): CHIP `payment.refunded` with a stable purchase id maps to `REFUND_COMPLETED` and the handler publishes `GatewayRefundCompleted` (handler already knows that type). Second: `setup_intent.succeeded` extracts PM (221). Third: Stripe `ConstructEvent` success + later `JsonException` is HTTP 400, not 500; missing `Stripe-Signature` stays 500/retry.

### Reproduction today
Arrange: tenant webhook secrets configured. Act/assert per leftover:
1. POST CHIP body `event_type=payment.refunded`, valid RSA — HTTP 200, no `PaymentWebhookLog`, no `GatewayRefundCompleted`.
2. POST Stripe `setup_intent.succeeded` — 200, no publish (221).
3. POST Stripe `checkout.session.async_payment_succeeded` — 200, no publish.
4. Razorpay `payment.authorized` (auto-capture off) — 200, checkout stays unpaid until `payment.captured`.
5. Xendit `status=PENDING` — 200, ignored.
6. Billplz `paid=false` after a paid bill — parsed `PAYMENT_FAILED` but handler 065 ignores if `PAYMENT_COMPLETED:{billId}` exists.
7. Stripe library throws a non-`StripeException` — uncaught 500.

### Blast radius
Leftovers are rail-specific. CHIP dashboard refunds never enter Billing (still 070/085 gap on that rail). Stripe dashboard refunds **do** enter after 085. Razorpay authorized-without-capture looks unpaid until capture — expected if the account is manual-capture, surprising if someone believed `authorized` meant paid. Async Stripe APMs are latent (sessions are `card` only today). Auth-hold CHIP preauthorized staying unmapped is **correct** for money (005 only maps token vault). Ops: silent ACK, no log, hard to debug. Not a double-charge. PII: raw bodies are not stored on passthrough (no log row).

### Suggested fix
Map only processor events that have a Payments EventType and a consumer: CHIP `payment.refunded` → `REFUND_COMPLETED` (same shape as Stripe 085; do not invent a second refund EventType). Keep CHIP preauthorized-without-token unmapped. Add `setup_intent.succeeded` as vault backup (221). Leave `payment.authorized` and Xendit `PENDING` as passthrough (do not fulfill uncaptured / unpaid). Billplz unpaid-as-failed can stay; 065 already stops late fail-after-pay. Split HTTP: `Verified=false` because bad signature → 500 (retry); verified-but-unusable payload (missing id/currency) → 400 / ACK so the gateway stops. Catch non-`StripeException` around `ConstructEvent` and return `Verified=false`. No TypeSpec regen. No homemade e-mandate. No `subscription.updated`.

### Evaluation notes
Duplicates: 005 (CHIP `$0` vault — done), 069 (Razorpay expire), 070 / 085 (Stripe inbound refunds), 065 (late fail), 221 (`setup_intent.succeeded`), 220 (CHIP still subscribes `payment.refunded`). Severity still **P2** for the leftover catalog; Stripe refund + CHIP vault were the P1-shaped rows and they moved. Not blocked. Residual after 161-200 fail-closed: 072 missing-currency is `Verified=false` → 500 retry storm (same “bad signature vs bad body” hole the audit called out).


