---
number: "223"
id: B04-P22
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 223 — B04-P22 — Dropped event types (wrong mapping / swallowed)

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

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

