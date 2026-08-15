# W0-LP-072 — done

Off-session `AUTO_CHARGE` now actually retries a vaulted Stripe/CHIP card during dunning (attempts **2–4**). Billing still owns attempt **1**. Billplz / Razorpay / no-vault / reminder-only consume the day-offset and never publish `ExecuteOffSessionCharge`. At most one `PENDING` attempt exists per `(SubscriptionId, TargetBillingDate)`; a later AUTO_CHARGE offset is deferred (not consumed) until that row leaves `PENDING`. Stripe PI create is idempotent on `lazuar-offsession:{chargeAttemptId}` and stamps `charge_attempt_id` in metadata. New orgs get default `+1` / `+5` AUTO_CHARGE steps (Billplz products still skip via `PaymentGatewayCapabilities`).

LP-047 / LP-052 / LP-071 are unchanged: capability allow-list, Billplz returns `false`, reminder-only skip, billing attempt 1, Stripe `payment_intent.payment_failed` → `PAYMENT_FAILED`, past-due claim excludes processed ids. No hard/soft decline fork (LP-076). No campaign snapshot (LP-079).

## Files changed

### Engine

- `apps/lazuar-api/Modules/Commerce/Infrastructure/Dunning/PastDueDunningProcessor.cs` — Stripe/CHIP allow-list (existing capabilities), max 4, no vault / reminder-only consume offset; `PENDING` / `SUCCEEDED` / one-per-tick do **not** consume offset and do not insert another attempt
- `apps/lazuar-api/Modules/Commerce/Application/Commands/DunningCampaignCommandHandlers.cs` — default seed `+1` and `+5` AUTO_CHARGE (new orgs only)

### Payments

- `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` — trailing optional `chargeAttemptId`
- `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` — `GetAdapter` + charge in try/catch; pass attempt id; idempotency key `lazuar-offsession:{chargeAttemptId}`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` — metadata `charge_attempt_id`; resolve idempotency from attempt id; PI failed map kept (LP-071)
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs` — metadata `charge_attempt_id`
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs` — notes `charge_attempt_id` (still not allow-listed)
- `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` — still returns `false`, ignores attempt id

### UI

- `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` — one action per day-offset note

### Tests

- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/DunningEngineJobTests.cs` — attempt 2 Stripe, CHIP gateway name, Billplz/Razorpay/no-vault consume, max 4, PENDING no consume, one charge per tick, idempotent offset, grace skip, pre-dunning no AUTO_CHARGE
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/ExecuteOffSessionChargeIntegrationEventHandlerTests.cs` — attempt id + formatted key; factory throw → `charge_exception`
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/StripeGatewayAdapterTests.cs` — idempotency resolve + metadata
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/DunningCampaignCommandHandlerTests.cs` — default +1/+5; second generate is no-op

## Tests run

- `Lazuar.ModuleTests` filter `DunningEngineJobTests|ExecuteOffSessionChargeIntegrationEventHandlerTests|StripeGatewayAdapterTests|BillplzGatewayAdapterTests|DunningCampaignCommandHandlerTests|BillingEngineJobTests|PaymentGatewayCapabilitiesTests|GatewayPaymentFailedIntegrationEventHandlerTests` — **74 passed**
- `Lazuar.ModuleTests` filter `ChargeAttemptLogTests|SubscriptionRecoveryTests|DunningCampaignDomainTests|ChipCollectGatewayAdapterTests|ProcessGatewayWebhookCommandHandlerTests|CommerceHonestyDtoTests` — **40 passed**

Not committed. Not pushed.

Existing tenants keep email-only defaults until ops adds AUTO_CHARGE or a new org seeds defaults. CHIP replay of the same outbox row can still create two purchases (no CHIP idempotency API).
