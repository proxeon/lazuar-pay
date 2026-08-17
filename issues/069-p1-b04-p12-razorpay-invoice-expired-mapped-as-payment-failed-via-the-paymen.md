---
number: "069"
id: B04-P12
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 069 — B04-P12 — Razorpay `invoice.expired` mapped as payment-failed via the payment entity

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P12 — P1 — Razorpay `invoice.expired` mapped as payment-failed via the payment entity

**Where.** `IsPaymentFailedEvent` includes `invoice.expired` (`301-302`). `MapPaymentFailed` reads `payload.payment.entity` (`327-330`).

**What.** Expire payloads without a payment entity and without `X-Razorpay-Event-Id` are `Verified=false` → 500 → retry storm. With the header, we publish `PAYMENT_FAILED` for a registration-link / invoice expiry that may not be a payment. Dropped type `payment.authorized` is the complementary hole (auto-capture off).

