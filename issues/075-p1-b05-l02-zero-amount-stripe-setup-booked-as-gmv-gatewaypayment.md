---
number: "075"
id: B05-L02
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 075 — B05-L02 — `$0` Stripe setup booked as GMV `GATEWAY_PAYMENT`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L02 — P1 — `$0` Stripe setup booked as GMV `GATEWAY_PAYMENT`

**Where.** `StripeGatewayAdapter.CreateCheckoutSessionOptions` (`:454-472`): `amount == 0 && setupFutureUsage` → Checkout `mode = setup`. `ParseWebhookAsync` on `checkout.session.completed` sets `AmountPaid = (session.AmountTotal ?? 0) / 100`, `EventType = PAYMENT_COMPLETED`, `GatewayTransactionId = PaymentIntentId ?? SetupIntentId ?? session.Id`. `ProcessGatewayWebhookCommandHandler` publishes Completed with no amount floor.

`InitiateCheckoutCommandHandler` uses that path for trials and 100% coupons on vaulting rails, and **overwrites** `type` to `"trial"` for trials (`:299`).

`GatewayPaymentCompletedHandler` does not skip `AmountPaid == 0`. It books cash 0 / revenue 0, allocates `RCPT-yyyy-#####`, generates an Official Receipt, marks B2C `PENDING`.

`CommerceCheckoutMetadata.IsCommerceSubscriptionType("trial")` is false. Commerce’s payment handler returns before opening the session. Trial vault webhook: Billing issues a receipt for RM 0; Commerce does not activate from that event.

100% coupon vault keeps `type=commerce_subscription`. Commerce activates **and** Billing issues a RM 0 receipt.

B2C consolidation later sees `PaidAmount = 0` and, if it is the only row, `MarkConsolidationIgnored`. The `RCPT` number is still burned.

**Tests.** No Billing test that `$0` / `type=trial` is skipped. `GatewayPaymentCompletedHandlerTests` only cover 100-unit B2C/B2B ordering.

---

