# W3-LP-058 — done

An `ACTIVE` or `TRIALING` subscription can schedule a swap to another recurring product in the same org (same gateway, currency, interval). `ProductId` stays until the due tick; `BillingEngineJob` applies `PendingProductId` then charges the new price. Undo clears pending. No mid-cycle charge.

## Files

- `Subscription.SchedulePlanChange` / `ClearPendingPlanChange` / `ApplyPendingPlanChange`
- `ChangePlanCommandHandler`, `POST /subscribers/{id}/change-plan`
- Billing apply-on-due, ops picker + pending badge

## Tests run

- `ChangePlanCommandHandlerTests`, `BillingEngineJobTests` (apply pending then charge new price), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-058` can move **N → Y**.
