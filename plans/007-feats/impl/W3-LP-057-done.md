# W3-LP-057 — done

Collection pause is a flag on an otherwise `ACTIVE` membership (`CollectionPausedUntil`). Billing and pre-dunning skip while paused; status does not become `SUSPENDED` or `PAUSED`. Resume is a date, not a payment. Ops labels dunning mute **Pause recovery** and collection holiday **Pause collection**.

## Files

- `Subscription.PauseCollection` / `ResumeCollection` / `IsCollectionPaused`
- `BillingEngineJob` skip while paused (does not roll `NextBillingDate`)
- `DunningEngineJob.Claim` pre-dunning excludes collection pause
- `POST /subscribers/{id}/collection/pause|resume`
- `SubscribersPage` two verbs

## Tests run

- `SubscriptionCollectionPauseTests`, `BillingEngineJobTests` (paused due tick), Commerce filter **355 passed**

Not committed. Not pushed.

Tracker `LP-057` can move **P → Y**.
