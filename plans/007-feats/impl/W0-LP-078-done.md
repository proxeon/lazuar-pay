# W0-LP-078 — done

Terminal CANCEL / SUSPEND now runs **after** due past-due steps, on `max(max(0, GracePeriodDays), last DayOffset >= 0)`. Grace no longer gates the step loop or `return`s for `FinalAction=NONE`. Same-tick last step + terminal is allowed (grace 0 + day-0 EMAIL dispatches, then cancels). Step `ActionType` CANCEL / SUSPEND / unknown is consumed and never treated as a domain cancel. Campaign delete only blocks **PAST_DUE** assignees. Ops shows **Terminal on day +N**.

No snapshot (LP-079). No `ActionType` cancel/suspend in the builder. `RecordChurn` still CANCEL-only. `Suspend()` still keeps `CurrentDunningCampaignId`.

## Files changed

### Engine

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/PastDueDunningProcessor.cs` — dispatch due steps first; `ResolveTerminalDayOffset`; skip non-comms ActionTypes; terminal after the loop
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` — thin `ResolveTerminalDayOffset` for tests

### Hygiene

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs` — `HasSubscriptionsAssignedToCampaignAsync` is PAST_DUE only
- `apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx` — “Terminal on day +{n}.”
- `apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx` — passes `steps`

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — formula cases + job matrix; old grace-skip AUTO_CHARGE test now expects PAST_DUE until last offset
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs` — `Suspend` / `Cancel` status

## Tests run

- `Lazuar.ModuleTests` filter `DunningEngineJobTests|SubscriptionRecoveryTests` — **50 passed**
- `Lazuar.ModuleTests` filter `BillingEngineJobTests|GatewayPaymentFailedIntegrationEventHandlerTests|DunningCampaignCommandHandlerTests|DunningCampaignDomainTests` — **36 passed**

Not committed. Not pushed.

Tracker LP-078 Lazuar **P → Y**. LP-079 and DN-008 (churn on SUSPEND) unchanged.
