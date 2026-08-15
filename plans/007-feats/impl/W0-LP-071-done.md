# W0-LP-071 — done

Failed renewal now **starts a dunning run**, not only `PAST_DUE` + campaign FK. Vaulted `GatewayPaymentFailed` and billing no-token both assign (if missing) and catch-up-dispatch due offsets (day-0 EMAIL on a same-day fail). The hourly past-due claim excludes ids already processed in the same `RunOnce`, so two `PAST_DUE` subscribers both get day-0 instead of the oldest row being scanned 50×.

Phase A PAST_DUE + assign is unchanged. `AssignDunningCampaign` still only runs when `CurrentDunningCampaignId` is null. Off-session throws already publish `GatewayPaymentFailed` (`off_session_not_supported` / `charge_exception`) from LP-047/052 — not regressed. No `DunningRun` table, no AUTO_CHARGE rewrite, no WhatsApp flag flip.

## Files changed

### Shared processor

- `apps/lazuar-api/Modules/Commerce/Domain/DunningCampaignMatcher.cs` — empty targets match all; `InferPaymentMethod`
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` — `Matches(org, product, method)`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/PastDueDunningProcessor.cs` — assign + pause skip + grace + catch-up dispatch
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` — WA demote + `reminder.dunning` payload

### Callers / claim

- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` — `Include(ReminderLogs)` + `Include(Steps)` campaigns; `ProcessAsync` on every failure (not only first PAST_DUE)
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` — no-token `MarkAsPastDue` then same processor
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Claim.cs` — `processedIds ∪ failedIds` excluded for both claim modes
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` — thin wrapper
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.Dispatch.cs` — delegates to dispatcher
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs` — matcher only (due-step inequality untouched)

### Payments (tiny G5)

- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — `payment_intent.payment_failed` → `PAYMENT_FAILED` with PI metadata

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` — day-0 on first fail; idempotent second fail; already-logged no-op; paused assign-only; no matching campaign; 0+3 catch-up
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — two PAST_DUE both day-0; no redispatch same run; paused skipped, sibling processed
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` — no-token day-0; two no-token both day-0
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignDomainTests.cs` — matcher on the real helper
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — PI failed mapping keeps `subscription_id`

## Tests run

- `Lazuar.ModuleTests` filter `GatewayPaymentFailedIntegrationEventHandlerTests|DunningEngineJobTests|BillingEngineJobTests|ExecuteOffSessionChargeIntegrationEventHandlerTests|StripeGatewayAdapterTests|DunningCampaignDomainTests` — **47 passed**
- `Lazuar.ModuleTests` filter `SubscriptionRecoveryTests|ProcessGatewayWebhookCommandHandlerTests|DunningCampaignCommandHandlerTests` — **27 passed**

Not committed. Not pushed.
