# W3-LP-060 — done

Recurring seats live on `Subscription.Quantity` (default 1) with optional `PendingQuantity`. Checkout stepper is allowed for FIXED `mo`/`yr` as well as one-time. Renewals, dunning AUTO_CHARGE, arrears GET, and webhook `amount` use `unit × N`. Admin `POST /subscribers/{id}/quantity` schedules next renewal.

## Files

- `Subscription.Quantity` / `ScheduleQuantity` / `ApplyPendingQuantity`
- `CommerceCheckoutQuantity` allows FIXED recurring
- Billing / `PastDueDunningProcessor` / arrears / `CommerceWebhookPayload` / `RenewalCheckoutIssuer`
- Checkout hop 1 stepper, ops set seats

## Tests run

- `CommerceCheckoutQuantityTests`, `BillingEngineJobTests` (qty × unit), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-060` can move **N → Y**.
