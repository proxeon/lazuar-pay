# W0-LP-052 — done

Automatic renewal actually runs. Vaulted due rows still dispatch billing attempt 1 + `ExecuteOffSessionCharge` (dates do not move). Non-vaulted due rows mint a hosted checkout bound to the **existing** `subscription_id`, persist the URL, then `PAST_DUE`. Generate failure does not flip `PAST_DUE`. Claim skips `PENDING`; `one_time` is not renewed. Adapter throw on off-session publishes `charge_exception` and is not rethrown. Stripe off-session uses `RequestOptions.IdempotencyKey = event.Id`.

LP-047 is unchanged: Billplz / reminder-only / no-vault still do not off-session; dunning `AUTO_CHARGE` still owns attempts 2–4.

## Files changed

### Commerce

- `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` — `CurrentRenewalCheckoutUrl` / `CurrentRenewalCheckoutForDate`; `Activate` accepts nullable next date; clear URL on recover / resume / non-arrears activate
- `apps/lazuar-api/Modules/Commerce/Application/RenewalCheckoutIssuer.cs` — `GenerateCheckoutSessionQuery` with `subscription_id` = existing Subscription id (not a Commerce session)
- `apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs` — optional `checkout_url`
- `apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` — `one_time` enroll no longer writes `NextBillingDate`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` — skip `PENDING` / `one_time` / missing product (`failedIds`); vaulted attempt 1 unchanged; non-vaulted mint then `PAST_DUE`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` — reuse stored URL when `CurrentRenewalCheckoutForDate == NextBillingDate.Date`
- `apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs` + `Migrations/20260816120000_AddSubscriptionRenewalCheckout.*` + snapshot — two nullable columns

### Payments

- `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` — optional `idempotencyKey`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — `IdempotencyKey = event.Id`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` / `RazorpayGatewayAdapter.cs` / `BillplzGatewayAdapter.cs` — accept unused key (best-effort)
- `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` — pass `@event.Id`; unexpected throw → `charge_exception`, no rethrow

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/BillingEngineJobTests.cs` — vaulted attempt 1; already-attempted no-op; non-vaulted mint; generate throw retries; skip statuses / future / `PENDING`; `one_time`; missing product sibling
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` — throw → `charge_exception`; idempotency key forwarded
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — `IdempotencyKey`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionRecoveryTests.cs` — one_time null due date; recover/resume clear URL

## Tests run

- `Lazuar.ModuleTests` filter `BillingEngineJobTests|ExecuteOffSessionChargeIntegrationEventHandlerTests|StripeGatewayAdapterTests|SubscriptionRecoveryTests|BillplzGatewayAdapterTests|DunningEngineJobTests|DunningCampaignCommandHandlerTests|SubscriptionLifecycleWebhookTests` — **51 passed**
- `Lazuar.ModuleTests` filter `PublicArrearsEndpointsBoundaryTests|PaymentGatewayCapabilitiesTests|CommerceHonestyDtoTests` — **11 passed**

Not committed. Not pushed.
