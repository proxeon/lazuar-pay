# 010 — Failed ModuleTests (48)

Full, uncondensed subagent analyses. Do not treat this index as the analysis.

HEAD at analysis time: `4531f210` (`fix/180-unify-outbox-inbox`).
Suite: `Lazuar.ModuleTests` — 1250 passed, 2 skipped, 48 failed.

| File | Subagent | Assigned tests |
|------|----------|----------------|
| [01-checkout-b2b-sst.md](./01-checkout-b2b-sst.md) | B2B checkout SST | 6 `CheckoutB2bIdentityTests` InitiateCheckout B2B / session-id |
| [02-initiate-checkout-qty-sst.md](./02-initiate-checkout-qty-sst.md) | InitiateCheckout qty SST | 12 `CommerceProductCompletenessTests` InitiateCheckout + 1 MarkPaid yearly coupon |
| [03-mark-paid-offline-sst.md](./03-mark-paid-offline-sst.md) | Mark-paid offline SST | 4 `MarkCheckoutAsPaidOffline_*` |
| [04-pastdue-and-nsubstitute.md](./04-pastdue-and-nsubstitute.md) | PAST_DUE + NSubstitute | 11 `GatewayPaymentFailed*` + 6 `BillingEngineJobTests.RunOnce_*` |
| [05-delivery-log-tenant-filter.md](./05-delivery-log-tenant-filter.md) | Delivery-log tenant filter | 8 `DispatchMessageIntegrationEventHandlerTests` |

Implementation of the recommended test-fixture fixes is a later step.
