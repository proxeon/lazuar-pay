# W0-LP-073 — done

Email recovery (`reminder.dunning`) now **commits hop 2**. `FulfillmentRequested` hydrate publishes `DispatchMessage` onto `communications.OutboxMessages` and `SaveChanges`s the same scoped `CommunicationsDbContext` as `OutboxEventBus`. Commerce still acks hop 1 after `ReminderDispatchLog`; Messaging / `ResendEmailService` can drain the comms outbox and send with tenant BYOK.

New orgs get the default campaign from entitlement seed (templates + `DefaultTemplatesSeeded` share one comms save). Pre-dunning −3 fires when `daysUntilDue <= 3`, not at day −14. Default **+3 is EMAIL** with `{{update_payment_link}}` (existing tenants keep their deployed +3). Hydrate **throws** on missing CRM / missing `client_profile_id` / empty profile email / empty EMAIL body so hop 1 retries instead of silent-acking.

WhatsApp flag unchanged (`Messaging:WhatsAppEnabled` stays default false). No LP-153 variable catalog work. No receipt / lifecycle flush (LP-151). No campaign snapshot (LP-079).

## Files changed

### Communications (hop 2 + seed)

- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — inject `CommunicationsDbContext`; `PublishAsync` then `SaveChangesAsync`; dunning hydrate throws when it cannot send
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs` — one save after templates + `DefaultTemplatesSeeded` outbox row

### Engine / seed

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PreDunning.cs` — catch-up `daysUntilDue <= Math.Abs(DayOffset)`
- `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` — default +3 EMAIL (new orgs only)

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — E1 day-0 EMAIL payload + log; E2 second run no re-publish; E3 WA-only skip+log; E4 −3 at 10d vs 3d; E5 no campaign match; E6 paused not claimed
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs` — H1 real `OutboxEventBus` writes `DispatchMessage`; H2–H4 throw, no outbox; empty EMAIL body throw
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/AppEntitlementGrantedIntegrationEventHandlerTests.cs` — COMMERCE seed writes `DefaultTemplatesSeeded` outbox; second grant is no-op
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignCommandHandlerTests.cs` — default +3 EMAIL with `{{update_payment_link}}`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Messaging/DispatchMessageIntegrationEventHandlerTests.cs` — inactive/null BYOK → FAILED + no-fallback throw; suppressed → SKIPPED

## Tests run

- `Lazuar.ModuleTests` filter `DunningEngineJobTests|DunningTemplateVariableSubstitutionTests|AppEntitlementGrantedIntegrationEventHandlerTests|DunningCampaignCommandHandlerTests|DispatchMessageIntegrationEventHandlerTests|ResendEmailServiceTests` — **46 passed**
- `Lazuar.ModuleTests` filter `BillingEngineJobTests|TenantEmailConfigurationTests|DefaultMessageTemplatesTests|GatewayPaymentFailedIntegrationEventHandlerTests` — **31 passed**

Not committed. Not pushed.

Existing tenants keep a dead +3 WHATSAPP until they edit or re-deploy. `LifecycleEventHandlers` / receipt `DispatchMessage` still lack SaveChanges (LP-151).
