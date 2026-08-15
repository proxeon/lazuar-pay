# W0-LP-153 — done

Dunning, Payment Failed, and Subscription Cancelled now share one hydrator. Catalog / default-campaign tags resolve to real values; `{{renewal_link}}` is an alias of `{App:ClientUrl}/{slug}/update-payment/{subscriptionId}`. `https://portal.lazuar.com/checkout/update` is gone from production code. Missing optional keys become empty strings — known tags never ship as `{{tag}}`. Unknown merchant tags are left as-is (no sweeper).

Payment Failed stays on `GatewayPaymentFailed` (LP-151). SUSPEND still does not send mail. WhatsApp is still not sent (`Messaging:WhatsAppEnabled` default false); cancel / failed-pay still populate the WA body so `ALL` channel copy is not raw. Dual CMS (step copy vs templates) unchanged. Digital delivery / receipts / ACTIVE change-card out of scope.

Tracker LP-153 Lazuar stays **P** — unit tests are the merge gate; no local BYOK inbox glance was done.

## Files changed

### Shared hydrator

- `apps/lazuar-api/Modules/Communications/Application/MessageTemplateHydrator.cs` — `MessageTemplateContext`, `Populate`, money/date format, `MessageLinkBuilder`, shared preview mocks (`update_payment_link` is a real update-payment URL)

### Production callers

- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — hydrator; `renewal_link` == update-payment; amount `0.00`; `current_period_end` human date
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` — cancel subject + body + WA; CRM + One + mail-context + token + real links
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` — same hydrator (LP-151 send trigger)

### Commerce port + payload

- `apps/lazuar-api/Modules/Commerce/Contracts/ISubscriberQueryService.cs` — `GetSubscriptionMailContextAsync` / `SubscriptionMailContext`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/SubscriberQueryService.cs` — commerce-only EF read (no CRM JOIN)
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/DunningStepDispatcher.cs` — payload `current_period_end` (`yyyy-MM-dd` from `NextBillingDate`) + `total_price` (same decimal as `amount`)

### Catalog / wiki / preview

- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — Payment Failed CTA `{{update_payment_link}}`; `{{renewal_link}}` optional alias
- `apps/lazuar-api/Modules/Communications/Infrastructure/Services/CommunicationsQueryService.cs` — wiki adds billing tags; drops `meeting_link` / `group_link`
- `apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs` — preview uses hydrator mocks
- `apps/lazuar-api/Modules/Communications/Application/Commands/MessageTemplateCommandHandlers.cs` — test-reminder uses the same mocks
- `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` — placeholder lists amount / currency / period / days
- `apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx` — create-template optional list includes `{{update_payment_link}}`

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/MessageTemplateHydratorTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/LifecycleEventHandlersTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/GatewayPaymentFailedEmailHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DefaultMessageTemplatesTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/TemplateVariablesWikiTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriberQueryServiceMailContextTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — day-0 payload asserts `total_price` + `current_period_end`

## Tests run

- `Lazuar.ModuleTests` filter `MessageTemplateHydratorTests|DunningTemplateVariableSubstitutionTests|LifecycleEventHandlersTests|DefaultMessageTemplatesTests|GatewayPaymentFailedEmailHandlerTests|SubscriberQueryServiceMailContextTests|TemplateVariablesWikiTests|DunningEngineJobTests.PastDue_Day0Email|AppEntitlementGrantedIntegrationEventHandlerTests` — **42 passed**
- `Lazuar.ModuleTests` filter `Communications|DunningEngineJobTests|DunningCampaignCommandHandlerTests|CommerceDocumentLookup|SubscriberQueryService` — **122 passed**
- `Lazuar.ArchitectureTests` — **14 passed**

Not committed. Not pushed.

Existing tenant “Payment Failed” rows keep `{{renewal_link}}` until reset-to-default; the alias still fills the live update-payment URL.
