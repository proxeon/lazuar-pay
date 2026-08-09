# Phase 04 — Analysis (outbound webhooks: One vs Lhdn)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Decision (00.2):** One durable = platform; LHDN **end-state A** (route through One); **interim C freeze** until A ships. Reject B (second full Lhdn outbox/signing stack).  
**This phase implements:** C freeze documentation + inventory + Lhdn failure observability only. **Not** full A convergence.

---

## 1. Inventory — Two outbound customer-webhook paths

| Aspect | One (platform) | Lhdn (special-case) |
|--------|----------------|---------------------|
| **Role** | Only platform-grade durable delivery | Module-local fire-and-forget for e-invoice lifecycle |
| **Registry table** | `one.TenantWebhookEndpoints` | `lhdn.WebhookSubscriptions` |
| **Delivery table** | `one.WebhookDeliveryOutboxes` | *None* (no outbox, no retries) |
| **Dispatcher** | `OutboundWebhookDispatcherJob` | In-process HTTP from sender service |
| **Enqueue** | `OutboundWebhookEventHandlers` on `OutboundWebhookRequestedIntegrationEvent` | `DispatchExternalWebhookCommand` from status poller |
| **Signing** | Standard Webhooks–style `t={unix},v1={hmac_hex}` over `{ts}.{body}` | HMAC-SHA256 hex of **body only** |
| **Signature header** | `X-Lazuar-Signature` (`t=…,v1=…`) | `X-Lazuar-Signature` (raw hex, no timestamp) |
| **Extra headers** | `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id` | none |
| **HTTP client** | Named `"DeveloperWebhooks"` (15s timeout) | Default `IHttpClientFactory` client |
| **Retries / DLQ** | Up to 5 attempts, exponential backoff; status `FAILED` | None — one attempt; failures swallowed after log |
| **Metrics** | `LazuarMetrics.RecordWebhookFailed("outbound")` | `LazuarMetrics.RecordWebhookFailed("lhdn")` (Phase 04.2-C) |
| **Logs / delivery history** | Outbox rows + workspace webhook logs API | Logs only (no delivery log table) |
| **Event filter** | Per-endpoint `EnabledEvents` (empty = all) | Implicit all LHDN invoice events for active URLs |

---

## 2. One path — file map

### Domain

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs` | Multi-endpoint registry; URL, secret, `EnabledEvents`, `AcceptsEvent` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs` | Per-delivery row; claim lease, success/failure, max 5 attempts |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookUrlValidator.cs` | URL validation (SSRF-related guards live here) |

### Application / contracts

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs` | Integration event: org + event type + JSON payload (fan-out when `TargetUrl` null) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Contracts/IOneQueryService.cs` | `GetWorkspaceWebhooksAsync` / `GetWorkspaceWebhookLogsAsync` DTOs |

### Infrastructure

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs` | Subscribe → filter endpoints → enqueue `WebhookDeliveryOutbox` rows |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | Claim PENDING (`FOR UPDATE SKIP LOCKED`), POST, retry/fail |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs` | Sign + verify helpers |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs` | `TenantWebhookEndpoints`, `WebhookDeliveryOutboxes` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/DependencyInjection.cs` | Named HTTP client + hosted dispatcher + event subscription |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | Workspace webhook CRUD + delivery logs (+ provision webhook ensure) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Repositories/OneRepository.cs` | Endpoint load/list/add |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` | Snapshots for list/logs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260627124757_InitialOneSchema.cs` | Creates webhook tables |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260803173944_AddWebhookEndpointEnabledEvents.cs` | `EnabledEvents` column |

### Publishers into One (event sources)

| Path | Event type(s) |
|------|----------------|
| `Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | `subscription.activated`, `.suspended`, `.canceled`, `.resumed` |
| `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | `subscription.past_due` |
| `Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | `order.completed` |
| `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | `payment_link.paid` |
| `Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` | `payment.completed`, `payment.failed` |

### Shared metrics

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Application/Observability/LazuarMetrics.cs` | `lazuar.webhook.failed` counter (`source` tag) |

### Tests (reference)

| Path | Focus |
|------|--------|
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookTests.cs` | Signature, fan-out, `AcceptsEvent` |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OutboundWebhookClaimTests.cs` | Claim lease |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/SubscriptionLifecycleWebhookTests.cs` | Publish lifecycle → One event |
| `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/IntegrationCheckoutOutboundWebhookTests.cs` | payment.completed/failed |

---

## 3. Lhdn path — file map

### Domain / application

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs` | URL + secret + active flag (no event filter column) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Services/IWebhookSenderService.cs` | Port |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Build `invoice.{status}` payload; loop active subs → sender |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs` | Register / delete subscription |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` | List webhooks; reports events `invoice.valid`, `invoice.invalid` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Ports/ILhdnRepository.cs` | `GetActiveWebhooksAsync` / `AddWebhookSubscription` |

### Infrastructure

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` | Fire-and-forget HTTP + body HMAC; logs + metric on failure |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | On VALID/INVALID → `DispatchExternalWebhookCommand` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints.cs` | Admin LHDN webhook register/list/delete |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Repositories/LhdnRepository.cs` | Persistence |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/LhdnDbContext.cs` | `WebhookSubscriptions` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/DependencyInjection.cs` | Registers `IWebhookSenderService` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Migrations/20260627124829_InitialLhdnSchema.cs` | Creates `lhdn.WebhookSubscriptions` |

---

## 4. Event type catalog

### One workspace outbound (published today)

| Event type | Source module |
|------------|---------------|
| `subscription.activated` | Commerce lifecycle |
| `subscription.suspended` | Commerce lifecycle |
| `subscription.canceled` | Commerce lifecycle |
| `subscription.resumed` | Commerce lifecycle |
| `subscription.past_due` | Commerce `BillingEngineJob` |
| `order.completed` | Commerce order completed |
| `payment_link.paid` | Commerce custom payment link |
| `payment.completed` | Payments integration checkout |
| `payment.failed` | Payments integration checkout |

*(LHDN `invoice.*` is **not** published to One today.)*

### Lhdn outbound (customer-facing)

| Event type | When |
|------------|------|
| `invoice.valid` | Status poller: MyInvois VALID |
| `invoice.invalid` | Status poller: MyInvois INVALID |

Payload shape (Lhdn): `{ "event": "invoice.valid|invalid", "data": { internal_id, lhdn_uuid, status, qr_link, error_message, timestamp } }` (snake_case JSON).

---

## 5. HMAC / signature differences (integrator-visible)

| | One | Lhdn |
|--|-----|------|
| **Signed material** | `{unixTimestamp}.{rawBody}` | `rawBody` only |
| **Header value** | `t=<unix>,v1=<hex>` | `<hex>` only |
| **Replay resistance** | Timestamp + optional receiver skew window (`TryVerify` default 300s) | None |
| **Delivery identity** | Delivery-id + webhook-id headers | Not present |

**Implication for end-state A:** when LHDN routes through One, customers must verify the **One** scheme (or a dual-verify window must be designed). Do not silently change Lhdn hex-only signing on the frozen path without product/docs.

---

## 6. Decision application (this phase)

| 00.2 choice | Status in Phase 04 |
|-------------|-------------------|
| **A** — LHDN lifecycle through One dispatcher | Documented as **end-state**; **not implemented** (non-trivial: dual registries, signing parity, payload versioning) |
| **B** — second Lhdn durable stack | **Rejected**; no Lhdn outbox introduced |
| **C freeze** — fire-and-forget remains | **Active**: README freeze rules; observability only |

### Freeze rules (operational)

1. Do **not** add Lhdn-local outbox / retry / DLQ as a second product stack.
2. Do **not** “half-upgrade” `WebhookSenderService` into durable delivery.
3. Do **not** invent a third signing scheme.
4. Allowed under freeze: logs, metrics, docs, bugfixes that do not expand the stack.
5. Full A: map `invoice.*` → `OutboundWebhookRequestedIntegrationEvent` (or One command), retire `DispatchExternalWebhookCommand` / `WebhookSenderService` for customer delivery, migrate subscription registry → One endpoints (or dual-write cutover) — **later phase**.

---

## 7. Out of scope

- Implementing A convergence code
- Deleting Lhdn webhook tables/endpoints
- Changing Lhdn signature algorithm
- Extracting `Modules/Webhooks` (00.2: stay in One)
- Phase 03 dual API keys work
