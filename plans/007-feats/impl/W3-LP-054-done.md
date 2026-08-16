# W3-LP-054 — done

Recurring products can grant a timed `TRIALING` period with no first charge. `Product.TrialDays` (0–90) drives hop 1 copy, a $0/setup-future first hop on vaulting gateways, and `Subscription.ActivateTrial`. Billing does not add `TRIALING` to the exclusion list; a due trial converts on the existing vault/mint path. Webhook status union now includes `TRIALING`. Coupon zero-amount is not reused as a trial.

## Files

- `Product.TrialDays` / `SetTrialDays`, `Subscription.ActivateTrial` + `TrialEndsAt`
- Commerce migration `20260820120000_AddWave3SubscriptionBilling`
- `InitiateCheckout` / `ProcessZeroAmount` / open-checkout / offline / manual enroll
- `webhooks.tsp` status union, lifecycle payload uses live status
- `ProductForm` trial days, hop 1 trial copy

## Tests run

- `SubscriptionTrialTests`, `BillingEngineJobTests` (trial not billed before end), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-054` can move **N → Y**.
