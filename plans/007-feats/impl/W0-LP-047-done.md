# W0-LP-047 — done

Honest vault / off-session: Stripe and CHIP can vault; Billplz (and any other rail) is reminder-only. Off-session never throws. Billing/dunning skip `AUTO_CHARGE` when the gateway cannot vault or the subscription is reminder-only. Campaign save rejects AUTO_CHARGE when every targeted product is reminder-only or targets are MANUAL-only.

## Files changed

### Payments

- `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` — `SupportsOffSession` / `IsReminderOnlyGateway` (STRIPE/CHIP only)
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` — `ChargeOffSessionAsync` returns `false`, does not throw
- `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` — capability short-circuit + `NotSupportedException` → `off_session_not_supported`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — `setupFutureUsage` sets `CustomerCreation=always`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — parse client id + recurring token from root or purchase; customer falls back to token

### Commerce

- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` — vault only on off-session gateways; CHIP token-only accepted
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` — `Activate(..., isReminderOnly: !hasVault)`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` — Billplz pay-again does not store vault
- `apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs` — recurring zero-amount is reminder-only
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` — charge only if gateway + not reminder-only + vault
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs` — same predicate; still records reminder log
- `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignAutoChargeGuard.cs` — campaign AUTO_CHARGE reject
- `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` — create/update call the guard
- `apps/lazuar-api/Modules/Commerce/Application/ICommerceRepository.cs` + `Repositories/CommerceRepository.cs` — `GetProductsByIdsAsync`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs` — `supports_off_session`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Subscribers.cs` — `is_reminder_only`
- `apps/lazuar-api/Modules/Commerce/Application/Modules.Commerce.Application.csproj` — `InternalsVisibleTo` for module tests

### Contracts / UI

- `packages/api-spec/modules/commerce/models/product.tsp` — `supports_off_session`
- `packages/api-spec/modules/commerce/models/subscriber.tsp` — `is_reminder_only`
- `packages/api-types-dotnet/Lazuar.ApiContracts.cs` — regen
- `packages/api-types-ts/src/index.ts` — regen
- `apps/lazuar-ops/src/lib/utils.ts` — `gatewaySupportsOffSession`
- `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` + `CreateProductForm.tsx` — reminder-only renewals copy
- `apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx` — Reminder-only / Auto-renew badge
- `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` + `CampaignTimeline.tsx` — `allowAutoCharge`
- `apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx` — compute/gate AUTO_CHARGE
- `apps/lazuar-ops/src/modules/commerce/components/dunning/CampaignSettingsPanel.tsx` — gateway next to product; honest targeting labels
- `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` — Reminder-only badge vs Zap
- `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` — “Payment is due” / Complete Payment

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/PaymentGatewayCapabilitiesTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/BillplzGatewayAdapterTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ChipCollectGatewayAdapterTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignCommandHandlerTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceHonestyDtoTests.cs`

## Tests run

- `Lazuar.ModuleTests` filter `PaymentGatewayCapabilitiesTests|BillplzGatewayAdapterTests|ExecuteOffSessionChargeIntegrationEventHandlerTests|ChipCollectGatewayAdapterTests|StripeGatewayAdapterTests|BillingEngineJobTests|DunningEngineJobTests|DunningCampaignCommandHandlerTests|CommerceProductCompletenessTests|SubscriptionRecoveryTests|CommerceHonestyDtoTests` — **65 passed**

Not committed. Not pushed.
