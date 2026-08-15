# W0-LP-090 — done

Inbound payment webhooks stay `{ received: true }` after verify + durable queue. That is **not** paid. CHIP / Billplz / Razorpay money events without a stable id are fail-closed (`Verified=false`, no Guid / empty EventId). A signed redelivery whose payments outbox is Dead is re-queued on the same outbox id; a missing outbox is republished onto the existing log. Pre-ticket backfill rows with no `OutboxMessageId` stay skipped.

No raw-intake table. No replay UI. HTTP body unchanged.

## Files changed

### Domain / ports

- `apps/lazuar-api/Modules/Payments/Domain/Entities/PaymentWebhookLog.cs` — nullable `OutboxMessageId`; `ProcessedAt` documented as received/queued
- `apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentRepositories.cs` — `GetBy*` + `TryRequeueDeadOutboxAsync` (`Requeued` / `AlreadyActive` / `Missing`)

### Handler

- `ProcessGatewayWebhookCommandHandler*.cs` — EventId / business-key lookup inspects outbox; Dead re-queue; missing-outbox republish; whitespace-only business key is null
- `Modules/Payments/Infrastructure/Endpoints.cs` — `{ received: true }` is intake, not fulfillment

### Adapters

- `ChipCollectGatewayAdapter.cs` — `purchase.id` then root `id`; else `Missing stable CHIP purchase id`
- `BillplzGatewayAdapter.cs` — blank bill id fail-closed; HMAC via `FixedTimeEquals`

### Persistence

- `PaymentWebhookLogRepository` — Get + re-queue Dead (`Pending`, clear `ProcessedAt` / `NextAttemptAt` / attempts)
- `PaymentConfigurations.cs` + migration `20260816235900_AddPaymentWebhookOutboxMessageId` — `OutboxMessageId uuid NULL`, no FK

### Tests

- `ProcessGatewayWebhookCommandHandlerTests.cs` — missing config, unknown type, Dead re-queue (EventId + business key), backfill skip, missing-outbox republish
- `ChipCollectGatewayAdapterTests.cs` / `BillplzGatewayAdapterTests.cs` / `StripeGatewayAdapterTests.cs` / `RazorpayGatewayAdapterTests.cs` — verify + empty-id fail-closed
- `PaymentWebhookLogRepositoryTests.cs` — Dead → Pending, Pending → AlreadyActive, unknown → Missing

## Tests run

- `Lazuar.ModuleTests` filter `ProcessGatewayWebhookCommandHandlerTests|BillplzGatewayAdapterTests|ChipCollectGatewayAdapterTests|StripeGatewayAdapterTests|RazorpayGatewayAdapterTests|PaymentWebhookLogRepositoryTests` — **55 passed**

Not committed. Not pushed.

Tracker LP-090 Lazuar **P → Y**. `LP-PAY-016` / `LP-PAY-017` unchanged. Proposed `LP-PAY-020` noted as done-by-LP-090 in `13-payments-refunds-rails.md`.
