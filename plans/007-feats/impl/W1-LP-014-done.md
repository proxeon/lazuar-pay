# W1-LP-014 — done

Quantity on hosted checkout is honest. FIXED `one_time` hop 1 has a 1–99 stepper. Initiate validates N **before** CRM / coupon / session persist. Payments hop 2 now gets **unit net** + `Quantity` so adapters do not square the charge. Recurring and PWYW stay qty 1 (API 400 if N ≠ 1). `CheckoutSession.Quantity` and `Order.Quantity` persist N. No `Subscription.Quantity` (LP-060).

Coupon math stays per unit. Custom / M2M / renewal still send Payments `Quantity: 1`. Adapters unchanged.

## Files changed

### Commerce

- `Modules/Commerce/Application/CommerceCheckoutQuantity.cs` — **new.** `Min = 1`, `Max = 99`. `NormalizeOrThrow`: omit → 1; reject 0 / negative / >99; require 1 unless FIXED + `one_time`.
- `Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` — normalize before CRM; `unitNet = Price − unitDiscount`; persist session N; zero-amount on `lineNet == 0`; paid path `Amount: unitNet`, `Quantity: N`.
- `Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs` — line gross/discount × N; `Order.Quantity = N`; `ZeroAmountCheckoutCompleted` amounts are line figures.
- `Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` — same line math; `Order.Quantity = N`.
- `Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` — additive `quantity` on `order.completed` from `Order.Quantity`.
- `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` — paid one-time `Order` keeps `session.Quantity`.
- `Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` / `Order.cs` — `Quantity` default 1 (custom sessions stay 1).
- `Modules/Commerce/Infrastructure/CommerceDbContext.cs` + `20260817010000_AddCheckoutAndOrderQuantity` — `commerce.CheckoutSessions.Quantity` and `commerce.Orders.Quantity` `integer NOT NULL DEFAULT 1`. No `Subscriptions.Quantity`.

### Payments / spec

- `Modules/Payments/Contracts/Queries/GenerateCheckoutSessionQuery.cs` — XML-doc: Amount is unit; adapters multiply Quantity.
- `packages/api-spec/modules/commerce/models/checkout.tsp` — quantity doc (default 1, FIXED one-time, 1–99). Types not regenerated (doc-only).

### Portal

- `apps/lazuar-portal/src/modules/checkout/types.ts` — `CHECKOUT_QUANTITY_MIN/MAX`, context `quantity` + `quantityAdjustable`.
- `CheckoutView.tsx` — clamp 1–99; adjustable only FIXED + `one_time`; form sends 1 otherwise.
- `OrderSummaryCard.tsx` — stepper + `unit × N` + “Discount (per item × N)”.
- `CheckoutForm.tsx` — still POSTs `quantity`; unused form stepper removed.

### Tests

- `CommerceCheckoutQuantityTests.cs` — omit/1/3; 0/−1/100 throw; `mo`/`yr`/PWYW N=3 throw; N=1 allowed; N=2/99 on `mo`/`yr`/PWYW throw; N=99 on FIXED one-time allowed.
- `CommerceProductCompletenessTests.cs` — FIXED one-time qty 3 unit Amount 100 / Quantity 3 (not 300 / not 900); coupon Amount 90; session N=3; qty 0/−1/100 throw before persist; recurring (`mo`/`yr`) and PWYW N=2/3 throw before CRM; custom still Amount=line sum Quantity=1; 100% coupon qty 3 Order qty 3 / line OriginalAmount 300; offline qty 3 AmountPaid 300; webhook one-time qty 3 Order; initiate + paid webhook persist session/order Quantity via `CommerceRepository` + in-memory EF.

## Tests run

- `Lazuar.ModuleTests` filter `CommerceCheckoutQuantityTests|CommerceProductCompletenessTests` — **40 passed** (plus later PWYW initiate case).
- `Lazuar.ModuleTests` filter `CommerceCheckoutQuantityTests|InitiateCheckout_|MarkCheckoutAsPaidOffline_OneTime_Qty3|GatewayPaymentCompleted_OneTime_Qty3` — **23 passed**.
- `Lazuar.ModuleTests` filter `OutboundWebhookRequestedPersistTests|GatewayPaymentCompletedRecoveryMetricsTests|GatewayCommonTests` — **27 passed**.
- `npx tsc --noEmit -p apps/lazuar-portal/tsconfig.json` — clean.
- `Lazuar.ModuleTests` filter `CommerceCheckoutQuantityTests|CommerceProductCompletenessTests` (after extra qty coverage) — **55 passed**, 0 failed, 0 skipped. Duration 640 ms. Not committed. Not pushed.

Manual §7.5 (Billplz/Stripe hop-2 amount = summary) **not run** here.

Not committed. Not pushed.

Seats / renewal × N remain LP-060. PWYW still charges catalog `Price` (CK-012 / LP-013).
