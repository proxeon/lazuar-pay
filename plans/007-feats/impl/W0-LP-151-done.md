# W0-LP-151 — done

Tenant Resend now has three real send jobs. Official Receipt gets a CRM email (checkout session / subscription fallback — Billing is not reordered ahead of Commerce). Card decline publishes **Payment Failed** on `GatewayPaymentFailed` with `{ClientUrl}/{slug}/update-payment/{subId}`. First `SubscriptionActivated` and `POST /public/commerce/{slug}/portal/magic-link` send catalog **Portal Access** with a 24h `?token=` URL. Tax Invoice / Credit Note no longer impersonate Official Receipt. SUSPEND no longer sends Payment Failed. DispatchMessage is flushed (`SaveChanges`) on these paths. No PDF attach. No LP-153 hydrator. WhatsApp still `ToPhone: null`.

Existing orgs pick up missing catalog names (including Portal Access) on the next `AppEntitlementGranted` — seed is insert-if-missing, not wipe.

## Files changed

### Receipt

- `apps/lazuar-api/Modules/Commerce/Contracts/ICommerceDocumentLookup.cs` — `GetCustomerForDocumentAsync` + subscription comms snapshot
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceDocumentLookup.cs` — log → session CRM → sub CRM
- `apps/lazuar-api/Modules/Billing/Contracts/Commands/GenerateAndStoreDocumentCommand.cs` — optional `CorrelationId`
- `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` — uses fallback lookup
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — pass `subscription_id` / `receipt`
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` — pass `SubscriptionId`
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/DocumentPublishedIntegrationEventHandler.cs` — exact type match + SaveChanges

### Failed pay

- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` — new
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/LifecycleEventHandlers.cs` — unhook SUSPEND; cancel SaveChanges
- `apps/lazuar-api/Modules/Communications/Infrastructure/DependencyInjection.cs` — subscribe fail + activation + request; drop suspend

### Magic link

- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — **Portal Access** (EMAIL)
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/PortalAccessEmailHandlers.cs` — first pay + request
- `apps/lazuar-api/Modules/Commerce/Contracts/Events/PortalMagicLinkRequestedIntegrationEvent.cs`
- `apps/lazuar-api/Modules/Commerce/Contracts/Commands/RequestPortalMagicLinkCommand.cs`
- `apps/lazuar-api/Modules/Commerce/Application/Commands/RequestPortalMagicLinkCommandHandler.cs`
- `apps/lazuar-api/Modules/Commerce/Application/ICommerceRepository.cs` + `CommerceRepository.cs` — newest sub for client
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs` — POST always 200
- `packages/api-spec/modules/commerce/public-routes.tsp` + `models/portal.tsp`
- `apps/lazuar-portal/src/modules/portal/components/RequestMagicLinkForm.tsx` + portal empty state

### Seed

- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/AppEntitlementGrantedIntegrationEventHandler.cs` — insert missing catalog names

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceDocumentLookupTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Commands/GenerateAndStoreDocumentCommandHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/GatewayPaymentFailedEmailHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/LifecycleEventHandlersTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/PortalAccessEmailHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/RequestPortalMagicLinkCommandHandlerTests.cs`
- Extended: DocumentPublished type guard, entitlement seed-if-missing, DefaultMessageTemplates, GatewayPaymentCompleted correlation, Commerce fail handler still no `DispatchMessage`

## Tests run

- `Lazuar.ModuleTests` filter `DocumentPublished|GenerateAndStore|CommerceDocumentLookup|GatewayPaymentFailed|LifecycleEventHandlers|PortalAccess|RequestPortalMagicLink|AppEntitlementGranted|DefaultMessageTemplates|GatewayPaymentCompletedHandlerTests` — **46 passed**
- `Lazuar.ModuleTests` filter `ManualSubscriberEnrolled|DispatchMessage|MagicLinkToken` — **14 passed**
- `Lazuar.ArchitectureTests` — **14 passed**
- `node scripts/check-openapi-minimal-honesty.mjs` — **OK** (130 OpenAPI, 137 Minimal, 7 impl_only)

Not committed. Not pushed.

Tracker LP-151 Lazuar **P → Y**. LP-153 (shared variables), LP-073 (dunning sequence), LP-154 (suppression split) unchanged.
