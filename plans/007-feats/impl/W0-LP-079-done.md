# W0-LP-079 — done

PAST_DUE assign now freezes campaign grace, final action, and steps as `v:1` JSON on `commerce.Subscriptions`. Mid-flight campaign edits cannot add catch-up steps, drop unsent snapshot offsets, change remaining copy, or retarget grace/final for that subscription. Archive-while-assigned still executes the snapshot (no longer stuck). `ClearDunning` / recover / resume drop the JSON; the next PAST_DUE assign snapshots the campaign as it is then.

No run table. No TypeSpec / ops UI. Pre-dunning ACTIVE still live-mutates (E9 documents the leftover).

## Files changed

### Domain

- `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/DunningCampaignSnapshot.cs` — `From` / serialize / `TryParse` (unknown `v` or garbage → null)
- `apps/lazuar-api/Modules/Commerce/Domain/IDunningStepCopy.cs` — shared step shape
- `apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs` — implements `IDunningStepCopy`
- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` — `DunningCampaignSnapshotJson`; assign with snapshot; `CaptureDunningCampaignSnapshot` lazy backfill; `ClearDunning` nulls JSON

### Engine

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/PastDueDunningProcessor.cs` — both assign sites write `From(campaign)`; execute snapshot; lazy-backfill archived/live by id when JSON is missing
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` — takes `IDunningStepCopy`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` — `jsonb` mapping

### Migration

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Migrations/20260816223418_AddSubscriptionDunningCampaignSnapshotJson.cs` — nullable `jsonb` + SQL backfill of currently assigned rows from live campaign+steps

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs` — snapshot write / clear / replace / mismatch
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignSnapshotTests.cs` — factory, round-trip, parse miss
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs` — first fail writes steps; already-assigned edit does not rewrite
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — E1–E9 + AUTO_CHARGE uses snapshot step id after `ClearSteps`

## Tests run

- `Lazuar.ModuleTests` filter `DunningCampaignSnapshotTests|SubscriptionRecoveryTests|DunningEngineJobTests|GatewayPaymentFailedIntegrationEventHandlerTests|DunningCampaignDomainTests|CommerceProductCompletenessTests|GatewayPaymentCompletedRecoveryMetricsTests` — **121 passed**
- `Lazuar.ModuleTests` filter `BillingEngineJobTests` — **12 passed**

Not committed. Not pushed.

Tracker LP-079 Lazuar **N → Y**. Pre-dunning leftover remains (E9). Recurly Settings History / per-run analytics still N (DN-022).
