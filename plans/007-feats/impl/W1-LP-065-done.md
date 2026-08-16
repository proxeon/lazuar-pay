# W1-LP-065 — done

Offline / manual payment subscription is an honest closed money loop. Ops can enroll a cash or bank-transfer member (or grant **one** complimentary period), see that payment on the member, and each later **Log Payment** extends access by one interval, books a **new** ledger row + Official Receipt path, and does **not** re-fire `subscription.activated`. The member stays reminder-only. No card on file. Next cycle the existing billing engine still mints a pay link and marks `PAST_DUE` unless ops logs another payment.

Ledger uniqueness is per **transaction log id**, not per subscription. Create always writes a `CommerceTransactionLog`. SUSPENDED recovery uses `RecoverFromPayment` so both dates move. Clerk `reference_number` is idempotent on the same subscription.

## Files changed

### Ledger / event

- `Modules/Commerce/Contracts/Events/ManualSubscriberEnrolledIntegrationEvent.cs` — `TransactionLogId` (empty Guid = pre-LP-065 outbox fallback)
- `Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` — `ReferenceId` = log id; `CorrelationId` still subscription id

### Create / Record / mark-paid

- `CreateManualSubscriberCommandHandler` — allow-list method, amount/product/duplicate validation, always write tx log, ledger keyed by log id, `subscription.activated` only when welcome
- `RecordSubscriberPaymentCommandHandler` — clerk-ref idempotency, `RecoverFromPayment` for SUSPENDED, optional `NextBillingDate`, no activated on ACTIVE renew
- `RecordSubscriberPaymentCommand` — optional `NextBillingDate`
- `MarkCheckoutAsPaidOfflineCommandHandler` — pass `TransactionLogId`; product-path log gets `SubscriptionId`
- `SubscriberEndpoints` — create `InvalidOperationException` / bad `product_id` → 400; bind record-payment override
- `OfflinePaymentMethods` — `BANK_TRANSFER` | `CASH` | `COMPED`

### Tx log + query

- `CommerceTransactionLog` — nullable `SubscriptionId`; empty external ref becomes log id
- `CommerceDbContext` + `20260817190000_AddTransactionLogSubscriptionId`
- `ICommerceRepository` / `CommerceRepository` — `HasActiveSubscriptionAsync`, `GetConfirmedTransactionLogByReferenceAsync`
- `GetTransactionsAsync` + TypeSpec — optional `subscription_id` (ignores email search when set)
- Gateway completion log writer stamps `SubscriptionId` when the paid row is a subscription

### Ops

- `CreateSubscriberModal.tsx` — recurring active products only; prefill amount; COMPED = one period; welcome = portal access link
- `SubscribersPage.tsx` — **Paid through / Next due** = `next_billing_date`; ledger by `subscription_id`; hide Stripe portal on reminder-only; refresh row after Log Payment
- `transactionStatus.ts` — hide Refund on `BANK_TRANSFER` / `CASH` / `COMPED` / `MANUAL` / `MANUAL_OFFLINE`

### Tests

- `CreateManualSubscriberCommandHandlerTests` — C1–C12
- `RecordSubscriberPaymentCommandHandlerTests` — R1–R10
- `ManualSubscriberEnrolledHandlerTests` — B1–B3 (two log ids book twice; replay does not)
- `CommerceQueryServiceTests` — `subscription_id` filter ignores email mix

## Tests run

- `Lazuar.ModuleTests` filter `CreateManualSubscriberCommandHandlerTests|RecordSubscriberPaymentCommandHandlerTests|ManualSubscriberEnrolledHandlerTests` — **28 passed**
- `Lazuar.ModuleTests` filter `CommerceProductCompletenessTests|CrossTenantIdorTests.RecordRefund|CreateManualSubscriber|RecordSubscriberPayment|ManualSubscriberEnrolled` — **66 passed** (includes W0-LP-077 record-payment recovery)
- `Lazuar.IntegrationTests` filter `CommerceQueryServiceTests` — **4 passed**
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean

Not committed. Not pushed.

Tracker `LP-065` Lazuar **P → Y**. Do not flip LP-053.

## Out of scope (still later)

M2M enroll (LP-137), CSV import (LP-064), lifetime SKU (SL-094), invoices (SL-084), Hub portal (LP-173), offline refund ledger (LP-091), `subscription.renewed`.
