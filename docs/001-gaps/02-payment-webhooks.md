<!-- Source subagent: 019fc650-3511-7762-8927-4f04f56c6187 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Payment Webhooks Gap Analysis

**Scope:** Lazuar Hub Payments inbound gateway webhooks, related integration-event consumers, and outbound customer/developer webhooks.  
**Codebase root:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Primary module:** `apps/lazuar-api/Modules/Payments`

---

## Inventory

### Inbound payment webhooks (providers → Lazuar)

| Artifact | Absolute path | Role |
|---|---|---|
| HTTP endpoint | `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs` | `POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}` |
| Command | `.../Application/Commands/ProcessGatewayWebhookCommand.cs` | DTO: TenantId, GatewayType, RawBody, Headers |
| Handler | `.../Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | Verify → filter → idempotency → outbox publish |
| Port | `.../Application/Ports/IPaymentGatewayAdapter.cs` | `ParseWebhookAsync`, `GatewayWebhookParsedResult` |
| Factory | `.../Infrastructure/Gateways/PaymentGatewayFactory.cs` | Adapter lookup by gateway type |
| Stripe adapter | `.../Gateways/StripeGatewayAdapter.cs` | Signature + event mapping |
| Billplz adapter | `.../Gateways/BillplzGatewayAdapter.cs` | HMAC form signature + query metadata |
| CHIP adapter | `.../Gateways/ChipCollectGatewayAdapter.cs` | RSA signature + event mapping |
| Razorpay adapter | `.../Gateways/RazorpayGatewayAdapter.cs` | HMAC header + `payment.captured` |
| Idempotency entity | `.../Domain/Entities/PaymentWebhookLog.cs` | `(Provider, EventId)` unique |
| Log repository | `.../Infrastructure/Repositories/PaymentRepositories.cs` | `HasBeenProcessedAsync` / `Add` |
| EF config | `.../Infrastructure/Configurations/PaymentConfigurations.cs` | Unique index |
| Schema migration | `.../Migrations/20260627124811_InitialPaymentsSchema.cs` | `payments.PaymentWebhookLogs` |
| Tenant BYOK config | `.../Domain/Aggregates/TenantPaymentConfiguration.cs` | ApiKey, WebhookSecret, MerchantId |
| CHIP auto-register | `.../Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | Fetches RSA key, registers CHIP webhook |
| Outbox bus | `BuildingBlocks/Infrastructure/OutboxEventBus.cs` | Write integration event to `payments.OutboxMessages` |
| Outbox worker | `.../Workers/PaymentsOutboxPublisherJob.cs` | Dispatch to in-memory bus |
| Module DI | `.../Infrastructure/DependencyInjection.cs` | Registers 4 adapters + workers |
| Module README | `.../README.md` | Declared responsibilities (partially stale) |

### Integration events (Payments contracts)

| Event | Path | Emitted by webhook path? |
|---|---|---|
| `GatewayPaymentCompletedIntegrationEvent` | `Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` | **Yes** |
| `GatewayDisputeCreatedIntegrationEvent` | `Contracts/Events/GatewayDisputeCreatedIntegrationEvent.cs` | **Yes** (Stripe only) |
| `GatewayPaymentFailedIntegrationEvent` | `Contracts/Events/GatewayPaymentFailedIntegrationEvent.cs` | **Never** (dead contract) |
| `GatewayRefundRequestedIntegrationEvent` | (outbound refund request) | Not from webhook |
| `GatewayRefundCompletedIntegrationEvent` | API refund path only | Not from webhook |
| `GatewayRefundFailedIntegrationEvent` | API refund path only | Not from webhook |
| `ApiCreditPurchasedIntegrationEvent` | `Contracts/Events/ApiCreditPurchasedIntegrationEvent.cs` | **Orphan / unused by webhook path** |
| `ExecuteOffSessionChargeIntegrationEvent` | Off-session charge request | Results rely on **later** webhooks |

### Downstream consumers of payment-success events

| Consumer | Path | Filters |
|---|---|---|
| Commerce fulfillment | `Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | `type` ∈ `commerce_subscription`, `custom_payment_link` |
| Billing ledger | `Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | All completed payments (ledger) |
| Utility credit top-up | `Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs` | `type == utility_credit_topup` |
| Chargeback clawback | `Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs` | `DISPUTE_CREATED` + utility top-up metadata |

### Outbound webhooks (Lazuar → customer systems)

| System | Path | Purpose |
|---|---|---|
| One developer webhooks | `Modules/One/Domain/TenantWebhookEndpoint.cs`, `WebhookDeliveryOutbox.cs`, `OutboundWebhookDispatcherJob.cs` | Commerce lifecycle callbacks |
| LHDN developer webhooks | `Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs`, `WebhookSenderService.cs` | Tax/e-invoice notifications (not payments) |
| Resend email webhooks | `Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs` | Bounce/complaint (inbound, Svix-style) |

### Docs / ADRs

- `apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md`
- `docs/architecture-decision-log/004-payment-integration-and-event-driven-guiideline.md`
- `docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md`
- `apps/lazuar-api/docs/001-cross-module-communication.md` (still shows `community_subscription` example — **stale**)

### Tests

**No automated tests** exercise `ProcessGatewayWebhookCommandHandler`, any gateway `ParseWebhookAsync`, or signature verification. Grep under `apps/lazuar-api/tests` finds no payment-webhook unit/integration tests.

---

## Inbound Webhook Pipeline

### Route registration

Mapped under `/api/v1` in `Program.cs`:

```313:313:apps/lazuar-api/src/Lazuar.Api/Program.cs
apiGroup.MapPaymentsEndpoints();
```

Effective URL:

```text
POST /api/v1/webhooks/payments/{gatewayType}/{tenantId:guid}
```

Examples:

- `/api/v1/webhooks/payments/stripe/{tenantId}`
- `/api/v1/webhooks/payments/billplz/{tenantId}`
- `/api/v1/webhooks/payments/chip/{tenantId}`
- `/api/v1/webhooks/payments/razorpay/{tenantId}`

### Step-by-step flow

```
Provider HTTP POST
        │
        ▼
Endpoints.cs
  • EnableBuffering, read raw body as string
  • Reject empty body (InvalidOperationException → 400)
  • Copy all HTTP headers (case-insensitive dict)
  • Inject Query-* from Request.Query (ADR-009)
  • gatewayType.ToUpperInvariant()
  • MediatR → ProcessGatewayWebhookCommand
        │
        ▼
ProcessGatewayWebhookCommandHandler
  1. Load TenantPaymentConfiguration by (TenantId, GatewayType)
     - Fail if missing or WebhookSecret empty
  2. adapter.ParseWebhookAsync(apiKey, webhookSecret, rawBody, headers, 0,0,0)
  3. If !Verified → InvalidOperationException (→ 400)
  4. If EventType ∉ {PAYMENT_COMPLETED, DISPUTE_CREATED} → silent return 200
  5. HasBeenProcessedAsync(EventId, Provider) → silent return 200
  6. Add PaymentWebhookLog
  7. Publish GatewayPaymentCompleted or GatewayDisputeCreated via PaymentsEventBus (outbox)
  8. SaveChanges (log + outbox row, same EF context)
        │
        ▼
HTTP 200 { "received": true }
        │
        ▼ (async)
PaymentsOutboxPublisherJob → InMemoryEventBus → Commerce / Billing handlers
```

### Endpoint behavior (evidence)

```17:66:apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs
group.MapPost("/{gatewayType}/{tenantId:guid}", async (
    string gatewayType,
    Guid tenantId,
    HttpContext context,
    IMediator mediator,
    ILoggerFactory loggerFactory) =>
{
    // ... read rawBody, headers, Query-* ...
    try
    {
        var command = new ProcessGatewayWebhookCommand(
            TenantId: tenantId,
            GatewayType: gatewayType.ToUpperInvariant(),
            RawBody: rawBody,
            Headers: headers
        );
        await mediator.Send(command);
        return Results.Ok(new { received = true });
    }
    catch (Exception ex) when (ex is not InvalidOperationException && ex is not BuildingBlocks.Domain.BusinessRuleValidationException)
    {
        logger.LogError(ex, "Unexpected critical error processing webhook for tenant {TenantId}.", tenantId);
        throw; 
    }
});
```

### Handler behavior (evidence)

```32:99:apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs
// config + secret required
// ParseWebhookAsync with fee/tax args hard-coded to 0
// only PAYMENT_COMPLETED and DISPUTE_CREATED
// check-then-insert PaymentWebhookLog
// PublishAsync then SaveChangesAsync
```

### Architectural intent (stateless)

Per ADR 004/009 and module README:

- Payments does **not** store pending checkouts.
- Context (`type`, `subscription_id`, `tenant_id`) must round-trip via gateway metadata or callback URL query string.
- On success, Payments emits a generic integration event; domain modules filter by `Metadata["type"]`.

---

## Per-Gateway Adapter Analysis

### 1. Stripe (`StripeGatewayAdapter`)

**Checkout**

- Mode `payment` only (not Stripe Billing subscriptions).
- Metadata attached to Checkout Session; `tenant_id` injected.
- `setupFutureUsage` → PaymentIntent `setup_future_usage=off_session`.
- **Does not** construct or register webhook URL in code — dashboard/manual BYOK setup required.

**Webhook verification**

- Requires `Stripe-Signature`.
- `EventUtility.ConstructEvent(rawBody, signature, webhookSecret)` — standard Stripe SDK verification (includes timestamp tolerance defaults).
- Failures return `Verified=false` → handler throws → **HTTP 400**.

**Event mapping**

| Stripe type | Mapped | Notes |
|---|---|---|
| `checkout.session.completed` | `PAYMENT_COMPLETED` | Preferred path; expands PI for fee/FX |
| `payment_intent.succeeded` | `PAYMENT_COMPLETED` | Off-session / alternative path |
| `charge.dispute.created` | `DISPUTE_CREATED` | Fetches PI metadata for clawback |
| Everything else | passthrough type, `Verified=true` | Dropped by handler whitelist |

**Strengths**

- Proper cryptographic verification.
- Real fee extraction from `balance_transaction` when expandable.
- Dispute support with metadata recovery.
- Stable `EventId = stripeEvent.Id` (`evt_...`).

**Gaps / risks**

1. **Dual-event double-processing risk:** Stripe often delivers both `checkout.session.completed` and `payment_intent.succeeded` for the same payment. They have **different** `evt_` IDs, so `PaymentWebhookLog` does **not** dedupe them. Downstream:
   - Billing ledger dedupes by `GatewayTransactionId` (good).
   - Commerce session checks `Status == COMPLETED` (mostly good).
   - **PlatformTopUp does not dedupe by transaction id** → risk of **double credit grant** if both events carry `utility_credit_topup` metadata (PI metadata may differ from Session metadata depending on how checkout was created).
2. No handling of `charge.refunded`, `payment_intent.payment_failed`, `invoice.*`, `customer.subscription.*`.
3. `payment_intent.succeeded` path sets `GatewayFee=0` (no balance transaction fetch).
4. No API-version pinning; schema drift risk.
5. Synchronous Stripe API call inside webhook parse (PI expand) increases latency and failure surface — if Stripe API is down, signature-valid webhook may fail mid-parse → 500 → retries (OK) but delayed fulfillment.
6. Off-session charges (`ExecuteOffSessionChargeIntegrationEventHandler`) only log failure; success depends on `payment_intent.succeeded` webhook. Metadata on off-session PI is only `{receipt, dunning_campaign_id}` — **missing `type` / `subscription_id`**, so Commerce renewal activation may **not** run from webhook for pure off-session charges unless another path activates the subscription.

---

### 2. Billplz (`BillplzGatewayAdapter`)

**Checkout**

- Builds callback URL:  
  `{ApiBaseUrl}/webhooks/payments/billplz/{tenantId}?type=...&reference_1=...`
- Also sets `reference_1` / `reference_2` on the bill (stripped from S2S body — ADR-009).
- Localhost rewritten to `lazuar-local-dev.com`.

**Webhook verification**

- Form-urlencoded body; HMAC-SHA256 over sorted `key+value` pairs excluding `x_signature`.
- Tries with “extra fields” (`paid_at`, `transaction_id`, `transaction_status`) then without — good compatibility shim.
- Signature is in body field `x_signature`, not header.

**Event mapping**

- `paid=true` or `state=paid` → `PAYMENT_COMPLETED`
- else → `PAYMENT_FAILED` (**never published**)
- `EventId = billId` (bill id, not a delivery id)

**Metadata reconstruction**

```167:195:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
// form reference_1 / reference_2, else Query-reference_1 / Query-subscription_id / Query-type
// utility_credit_topup → metadata["tenant_id"] = reference1
// else → metadata["subscription_id"] = reference1
```

**Gaps / risks**

1. **Fees always zero:** fee/tax config columns were removed (`RemoveAccountingOverrides` migration); handler always passes `estimatedFeePercentage=0`. Billplz never returns fees → `GatewayFee=0` always → incorrect net revenue in ledger for Billplz.
2. **Currency hard-coded `MYR`.**
3. **Idempotency key is bill id**, not “event delivery.” Acceptable for paid-once bills, but cannot distinguish multiple lifecycle callbacks if Billplz ever reuses bill semantics.
4. Unpaid callbacks return `PAYMENT_FAILED` → handler exits **without** writing webhook log → bill can still be paid later (OK). Repeated unpaid spam is re-verified each time (CPU/log noise).
5. Query-string metadata is forgeable only if attacker can hit the URL; signature still binds form body fields. Query params are **not** part of HMAC — so `Query-type` / `Query-subscription_id` are **not authenticated**. Anyone who can learn a valid signed Billplz form body and the callback URL shape could theoretically pair a different query string if they can get the gateway (or a proxy) to POST to a modified URL. In practice Billplz POSTs to the registered `callback_url` only; risk is mainly **misconfiguration / open redirect of callback** rather than random internet forgers. Still weaker than signed metadata.
6. Refunds: `IssueRefundAsync` always `false`; no refund webhooks.

---

### 3. CHIP Collect (`ChipCollectGatewayAdapter`)

**Checkout**

- Creates purchase with metadata nested under `purchase.metadata`.
- **Does not set per-purchase callback URL** — relies on account-level webhook registered at config time.

**Config-time auto-setup** (`UpdatePaymentConfigCommandHandler`)

- On new CHIP API key: fetch RSA public key → store as `WebhookSecret`.
- Register webhook: events `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized` →  
  `{ApiBaseUrl}/webhooks/payments/chip/{organizationId}`.

**Webhook verification**

- `X-Signature` base64 RSA-SHA256 PKCS1 over raw body using PEM public key — correct CHIP model.

**Event mapping**

| CHIP type | Mapped | Handler action |
|---|---|---|
| `purchase.paid` | `PAYMENT_COMPLETED` | Process |
| `purchase.preauthorized` | `PAYMENT_COMPLETED` | **Treats auth-hold as paid** |
| `purchase.payment_failure` | `PAYMENT_FAILED` | Dropped |
| `payment.refunded` | unmapped passthrough | Dropped despite registration |
| Other | passthrough | Dropped |

**Gaps / risks**

1. **`purchase.preauthorized` = paid** can fulfill before capture/settlement — money not guaranteed.
2. **`EventId = root.id`** treated as purchase id; if multiple event types share same id, second is dropped; if they differ, double-process risk.
3. Registers `payment.refunded` but never processes refunds via webhook.
4. Multi-tenant CHIP: each tenant registers a webhook URL with **their** org id. Depends on CHIP allowing multiple webhooks per account/brand; re-saving config may create **duplicate** webhooks (no idempotent upsert / list-before-create).
5. Re-saving masked key (`••••`) skips re-registration — good; rotating key re-registers — may orphan old webhooks.
6. No fee estimation fallback; fee only if `payment.fee_amount` present.

---

### 4. Razorpay (`RazorpayGatewayAdapter`)

**Checkout**

- Payment Link or Registration Link; `notes` = metadata.
- `callback_url` is **browser success URL**, not Lazuar webhook — webhooks must be configured in Razorpay dashboard per merchant.

**Webhook verification**

- `X-Razorpay-Signature` + `Utils.verifyWebhookSignature(rawBody, signature, webhookSecret)`.

**Event mapping**

- Only `payment.captured` → `PAYMENT_COMPLETED`.
- `EventId` from `X-Razorpay-Event-Id` header, else payment entity id, else **`Guid.NewGuid()`**.

**Critical gap**

```132:156:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs
var eventId = !string.IsNullOrEmpty(eventIdHeaderKey) ? headers[eventIdHeaderKey] : paymentEntity.GetProperty("id").GetString();
// ...
EventId: eventId ?? Guid.NewGuid().ToString(),
```

If header and payment id are missing, **every retry gets a new EventId** → **duplicate fulfillment**. Payment id fallback is OK when present; `Guid.NewGuid()` is unsafe.

**Other gaps**

- No refund / dispute / failed payment events.
- Notes metadata depends on payment link carrying notes through to payment entity (usually true; not guaranteed for all flows).
- Off-session recurring payment uses hardcoded email/contact placeholders.

---

### Gateway factory & DI

All four adapters registered as `IPaymentGatewayAdapter`; factory selects by uppercase type. Unknown type → `NotSupportedException` → **HTTP 500** (not 400), which may cause infinite provider retries on typo’d path.

---

## Idempotency & Exactly-Once Semantics

### What exists

1. **DB unique index** `IX_PaymentWebhookLogs_Provider_EventId` on `(Provider, EventId)`.
2. **Pre-check** `HasBeenProcessedAsync` for fast path.
3. **Transactional outbox**: log + outbox message added on same `PaymentsDbContext`, single `SaveChangesAsync` — atomic with outbox write.
4. **Migration / cutover playbook** (doc 006) for seeding legacy event IDs.
5. **Partial downstream idempotency:**
   - Billing ledger: `HasEntryBeenProcessedAsync("GATEWAY_PAYMENT", GatewayTransactionId)`.
   - Commerce: session status / existing subscription branches.
   - Chargeback handler comments assume Payments-level dedupe only.

### What is missing / broken

| Issue | Severity | Detail |
|---|---|---|
| Check-then-act race | Medium | Two concurrent deliveries can both pass `HasBeenProcessedAsync`; one loses unique constraint → exception → **500**. Survivor committed. Retry of loser should hit pre-check. **No explicit catch** of unique violation → noisy errors / delayed ACK. |
| Provider event-id ≠ business id | High | Dedupes **delivery/event** ids, not **payment** ids. Stripe dual events → two logs, two outbox events. |
| No payload / status storage | High | Cannot replay, debug, or distinguish “ignored event” vs “processed payment.” Log only has EventId, Provider, ProcessedAt. |
| `PAYMENT_FAILED` not logged | Low | Harmless for Billplz unpaid→paid; wastes work. |
| Razorpay `Guid.NewGuid()` EventId | **Critical** | Breaks idempotency under retries. |
| PlatformTopUp no tx-level unique | **High** | Wallet top-up has no unique constraint on gateway transaction reference; depends solely on webhook log event id. |
| ApiCreditPurchasedHandler | Medium | Also no idempotency; event appears unused by current webhook pipeline but handler exists. |
| Outbox poison-pill | **High** | `OutboxPublisherJob` **always** sets `ProcessedAt` even on handler failure — **permanent silent drop** of fulfillment after “successful” webhook ACK. |
| No exactly-once to customers | Medium | Outbound webhooks at-least-once with consumer-side id; no delivery guarantees beyond 5 retries. |
| Refund path amounts | High | `IssueRefundAsync(..., amount: 0)` then emits `RefundedAmount: 0` — refund “success” events are financially meaningless; not webhook-related but adjacent money pipeline. |

### Order of operations risk

Handler:

1. Add log (tracked)
2. `PublishAsync` (add outbox row tracked)
3. `SaveChangesAsync`

If step 3 fails after... both fail together (EF unit of work). **Good.**  
If SaveChanges succeeds and process crashes before HTTP response, provider retries → idempotent. **Good.**  
If SaveChanges succeeds, outbox worker fails handlers and marks outbox processed → **money recorded as “webhook received” but domain never fulfilled**. **Bad (exactly-once broken across modules).**

---

## Security (Signatures, Replay, Tenant Isolation)

### Signature verification matrix

| Gateway | Mechanism | Implemented? | Replay protection |
|---|---|---|---|
| Stripe | HMAC header `Stripe-Signature` + timestamp | Yes (SDK) | SDK default tolerance (~5 min) |
| Billplz | HMAC-SHA256 of form fields | Yes | **None** (no timestamp) |
| CHIP | RSA-SHA256 PEM public key | Yes | **None** beyond body uniqueness |
| Razorpay | HMAC `X-Razorpay-Signature` | Yes | Depends on Razorpay scheme; no extra app-level window |

### Tenant isolation / routing

- Tenant is **path parameter**, not derived from signed payload.
- Config loaded with `IgnoreQueryFilters` by `(OrganizationId, GatewayType)`.
- Webhook secret is **per-tenant per-gateway** (BYOK) — correct for multi-tenant.
- OrganizationId on integration events = **URL tenantId**, not metadata `tenant_id`.
- Platform top-ups use system tenant id `00000000-0000-0000-0000-000000000001` in checkout generation; metadata carries real `tenant_id` for wallet credit.

**Risks**

1. **No constant-time comparison** on Billplz string equality (minor).
2. **No IP allowlisting** (optional industry practice; Stripe discourages sole reliance).
3. **Secrets storage**: `WebhookSecret` / `ApiKey` stored as plain `text` columns — no encryption-at-rest in app layer visible in schema.
4. **Query-string state (Billplz)** not signed; PII policy in ADR-009 is sound but Guids in access logs still leak business graph.
5. **Error messages** on 400 include signature failure detail (`GlobalExceptionHandler` sets `Detail = exception.Message`) — useful to attackers for probing.
6. **Empty body → 400**, signature fail → 400: providers typically **stop retrying** on 4xx. Misconfigured secret → **permanent payment non-fulfillment** until human fixes and **manual replay** (no admin replay tool exists).
7. **CHIP public key as WebhookSecret** naming is confusing; rotation requires re-fetch.

### Comparison to Resend webhook (better pattern in same monorepo)

`PublicComplianceEndpoints` implements Svix-style:

- `svix-id`, `svix-timestamp`, `svix-signature`
- 300s staleness check
- Fixed-time compare

Payment webhooks do **not** share this rigor outside Stripe SDK.

---

## Outbound Webhooks to Customers

Payment webhooks themselves do **not** notify customer systems. Chain is:

```
Gateway webhook → GatewayPaymentCompleted
  → Commerce handler → SubscriptionActivated / OrderCompleted / ...
    → SubscriptionLifecycleIntegrationEventHandlers
      → OutboundWebhookRequestedIntegrationEvent
        → One.WebhookDeliveryOutbox
          → OutboundWebhookDispatcherJob → customer URL
```

### One module dispatcher

**Strengths**

- Outbox table, exponential backoff (`2^attempt` minutes), max 5 attempts → `FAILED`.
- HMAC-SHA256 `X-Lazuar-Signature` over payload.
- Payload includes `id`, `event_type`, `created_at`, `data`.

**Gaps vs Svix / Stripe outbound best practice**

| Concern | Current | Best practice |
|---|---|---|
| Signing | Raw hex HMAC of body | Versioned scheme + timestamp (`t=...,v1=...`) to prevent replay |
| Idempotency key header | Only JSON `id` | Explicit `Webhook-Id` + timestamp headers |
| Retries | 5, fixed schedule | Longer tail, jitter, DLQ alerting |
| Endpoint management | One URL per workspace | Multiple endpoints, event filters |
| Observability | Basic logs table | Delivery attempts, response bodies, redrive UI |
| Matching endpoint | Match by `OrganizationId + Url + Active` | Endpoint id on event |
| Payment events | Not direct | Customers never get `payment.succeeded` — only product lifecycle |

### LHDN `WebhookSenderService`

- Synchronous fire-and-forget; failures only logged.
- No outbox, no retry, no DLQ.
- Not payment-related but shows inconsistent outbound patterns across modules.

---

## Observability

### What exists

- Endpoint logs unexpected exceptions.
- Adapters log verification/checkout failures.
- `PaymentWebhookLog.ProcessedAt` for successful process path only.
- Outbox `Error` string on dispatch failure (but message still marked processed).

### What does not exist

- No retention of **raw webhook body**, headers, or response status.
- No `ProcessingStatus` enum (`Received`, `Ignored`, `Failed`, `Processed`).
- No metrics (latency, failure rate, duplicate rate) / OpenTelemetry spans.
- No correlation id linking webhook → outbox → commerce order.
- No admin API to list/replay webhooks.
- No alerting on outbox `Error` or stuck PENDING (outbox marks processed even on error).
- `LineItems` always empty on completed event — loses SKU-level observability.
- No structured log of `EventType`, `EventId`, `GatewayTransactionId` on success path in handler (silent success).

---

## Event Emission Consistency After Webhook

| Gateway result | Event published? | Consumers |
|---|---|---|
| Payment success | `GatewayPaymentCompletedIntegrationEvent` | Commerce, Billing ledger, PlatformTopUp |
| Payment failure | **None** (`GatewayPaymentFailedIntegrationEvent` dead) | Dunning / UX cannot react via Payments |
| Dispute (Stripe) | `GatewayDisputeCreatedIntegrationEvent` | ChargebackClawback (utility only) |
| Refund (webhook) | **None** | — |
| Refund (API) | Completed/Failed with **amount 0** | Ledger/commerce may mis-record |
| Off-session charge fail | **None** | Only logger in ExecuteOffSession handler |
| Metadata missing `type` | Event still published | All consumers no-op → **silent money drop** (classic Billplz pitfall documented in ADR-009) |

**Silent drop classes still present:**

1. Metadata missing/wrong `type` or `subscription_id`.
2. Outbox handler exception after webhook 200.
3. `purchase.preauthorized` may activate too early; later failure not reversed.
4. Docs still describe `community_subscription`; code uses `commerce_subscription` / `custom_payment_link` / `utility_credit_topup` — migration confusion risk.

---

## Gaps & Risks

### P0 — Correctness / money safety

1. **Stripe dual-event / multi-event id ≠ payment id** can double-emit integration events; credit top-up not transaction-idempotent.
2. **Outbox poison messages marked processed** → permanent non-fulfillment after ACK 200 to gateway.
3. **Razorpay EventId fallback to new Guid** → duplicate processing on retries.
4. **`GatewayPaymentFailed` never emitted** — failed payments invisible to domain.
5. **CHIP `purchase.preauthorized` treated as paid.**
6. **Refund pipeline broken** (amount 0; no webhook refund handling) — financial asymmetry.
7. **Off-session charge metadata incomplete** — renewals may not fulfill via webhook path.

### P1 — Design / operability

8. Stateless model + Billplz unsigned query metadata is fragile; no pending-session store as safety net.
9. Fee columns removed; Billplz net amounts wrong.
10. No raw payload audit trail / replay tooling.
11. No webhook unit/integration tests.
12. CHIP webhook registration not idempotent; refund events registered but ignored.
13. HTTP 400 on config/signature errors prevents automatic recovery after secret fix (no manual redrive).
14. README / docs drift (`community_subscription`, fee estimation still mentioned).

### P2 — Hardening / product

15. Secrets in plaintext DB columns.
16. No multi-endpoint outbound Svix-grade developer platform.
17. No rate limiting / abuse protection on public webhook routes.
18. Unknown gateway type → 500 retries.
19. Empty `LineItems` always.
20. LHDN outbound has no retries (pattern inconsistency).

### Industry best-practice delta (Stripe / Svix)

| Best practice | Lazuar status |
|---|---|
| Verify signatures | Yes (per gateway) |
| Return 2xx quickly; async heavy work | Partial — sync PI expand on Stripe; outbox for domain |
| Idempotent processing | Partial — event-id only; race unhandled; dual events |
| Store event payloads | **No** |
| Ignore unknown events with 200 | Yes |
| Retry-safe handlers | Partial; outbox DLQ wrong |
| Replay from dashboard | **No** |
| Versioned outbound signatures + timestamp | **No** (simple HMAC) |
| Explicit event catalog & schema | Implicit metadata bag |
| Test with CLI / fixtures | **No tests** |
| Separate “received” from “processed” | **No** |
| Dead-letter + alert | **No** |

---

## Recommendations (Prioritized)

### P0 — Fix immediately

1. **Business-key idempotency**  
   Dedupe on `(Provider, GatewayTransactionId, EventTypeFamily)` **or** map multiple Stripe types to one logical payment key before outbox publish. Prefer: process only `checkout.session.completed` for Checkout and only `payment_intent.succeeded` for off-session (explicit allowlist per flow).

2. **Fix outbox failure handling**  
   Do **not** mark outbox `ProcessedAt` on handler failure; implement retry count + DLQ table + alert. Webhook ACK and domain fulfillment must not silently diverge.

3. **Razorpay EventId**  
   Require `X-Razorpay-Event-Id` or payment id; **never** `Guid.NewGuid()`. Fail verification if missing.

4. **PlatformTopUp / wallet**  
   Unique constraint or existence check on gateway transaction reference before `TopUp`.

5. **Stop treating `purchase.preauthorized` as paid**  
   Only `purchase.paid` (or explicit capture confirmation).

6. **Publish `GatewayPaymentFailedIntegrationEvent`** for mapped failures; wire Commerce dunning/UI.

7. **Off-session metadata**  
   Attach `type`, `subscription_id`, `tenant_id`, `dunning_campaign_id` to PaymentIntent/notes so webhooks can fulfill renewals.

### P1 — Structural redesign (addresses “webhooks are worse”)

8. **Webhook intake table** (replace thin `PaymentWebhookLog`):
   - Id, TenantId, Provider, ProviderEventId, ProviderPaymentId  
   - RawBody (or object storage pointer), Headers JSON  
   - SignatureValid, Status (`Received|Ignored|Processed|Failed`)  
   - Error, ProcessedAt, AttemptCount  
   Unique `(Provider, ProviderEventId)` + unique optional `(Provider, ProviderPaymentId, LogicalType)`  

9. **Two-phase processing**  
   - Phase A (HTTP): verify signature, persist raw event, return 200 within 1s.  
   - Phase B (worker): parse, enrich, publish outbox — retriable.

10. **Optional pending checkout projection** (escape hatch for “dumb” gateways)  
    Store `SessionId/BillId → metadata` at checkout generation with TTL; webhook merges gateway payload with stored context if metadata missing. Keeps modules decoupled while ending silent drops.

11. **Catch unique violations** in webhook handler → return 200 (duplicate).

12. **Refund webhooks**  
    Handle Stripe `charge.refunded` / CHIP `payment.refunded` / Razorpay refunds; fix API refund amounts.

13. **Restore fee strategy** for Billplz (config or post-payment API fetch).

14. **Test suite**  
    Signature fixtures per gateway; idempotency races; dual Stripe events; missing metadata; outbox failure.

### P2 — Platform quality

15. Encrypt BYOK secrets at rest.  
16. Admin replay UI / API.  
17. Metrics + alerts on webhook 4xx/5xx, ignore rates, outbox lag.  
18. Align outbound developer webhooks with Svix-style signatures (timestamp + id).  
19. Auto-register webhooks for Stripe/Razorpay where APIs allow, or document mandatory dashboard URLs per tenant.  
20. Refresh docs: remove `community_subscription`; document event catalog and metadata schema as a single source of truth.

---

## File-by-File Notes

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs`

- Single catch-all route; good multi-gateway surface.
- Query→`Query-*` injection implements ADR-009 correctly.
- Empty body → exception → 400 (provider may not retry).
- No auth middleware (correct for public webhooks) but no rate limit.
- Success body `{ received: true }` is fine.
- Does not return 400 for signature failures itself — bubbles as `InvalidOperationException`.

### `/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommand.cs`

- Clean command shape; `Id = Guid.CreateVersion7()` unused for idempotency.

### `/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`

- **Core design bottleneck:** sync verify+process; narrow event whitelist; hard-coded fee args `0,0,0`.
- Does not publish failures.
- Does not handle unique constraint races.
- Does not log success/ignore reasons.
- TenantId trusted from URL for `OrganizationId`.
- Transactional outbox write is the best part of this file.

### `/apps/lazuar-api/Modules/Payments/Domain/Entities/PaymentWebhookLog.cs`

- Minimal ledger; comments mention Stripe/Billplz only.
- No TenantId, payload, status, or payment id.

### `/apps/lazuar-api/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs`

- `GatewayWebhookParsedResult` is a flat DTO — workable but overloaded (`AmountPaid` used for dispute amount).
- Fee estimation parameters on `ParseWebhookAsync` are obsolete post-migration.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`

- Solid verification and fee enrichment for Checkout.
- Dual event types both map to `PAYMENT_COMPLETED` without payment-level dedupe.
- Dispute support is uniquely mature vs other gateways.
- Off-session / refund helpers live here but are not webhook-complete.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`

- Best embodiment of ADR-009; also its sharp edge (unsigned query metadata).
- HMAC dual-mode is pragmatic.
- Fees broken by config removal.
- Refunds unsupported.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`

- RSA verify correct.
- Checkout omits webhook URL (account-level only).
- Preauthorized=paid is dangerous.
- Token recurrence model uses purchase id as token — workable but opaque.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`

- Signature OK; EventId fallback **not** OK.
- Webhook URL not provisioned in app.
- Only `payment.captured`.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs`

- Simple; throws `NotSupportedException` for unknown types.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Repositories/PaymentRepositories.cs`

- Correct `IgnoreQueryFilters` for unauthenticated webhook path.
- No transactional `INSERT … ON CONFLICT DO NOTHING` pattern.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Configurations/PaymentConfigurations.cs`

- Unique index correctly defined.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs`

- CHIP auto-onboarding is the only automated webhook registration — good UX, weak lifecycle management (duplicates, no delete).
- Masked secret preservation is correct.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/Workers/PaymentsOutboxPublisherJob.cs` + `BuildingBlocks/Infrastructure/OutboxPublisherJob.cs`

- `FOR UPDATE SKIP LOCKED` batching is good.
- **Always marking processed** after error is a critical reliability bug for payment fulfillment.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs`

- Refund amount hard-coded `0`; currency hard-coded `MYR`.
- Not driven by webhooks; undermines refund event consumers.

### `/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs`

- Fire-and-forget charge; success/failure not turned into domain events except via later webhooks with incomplete metadata.

### `/apps/lazuar-api/Modules/Payments/Contracts/Events/*`

- `GatewayPaymentFailedIntegrationEvent` and `ApiCreditPurchasedIntegrationEvent` look like unfinished product surface.
- `GatewayPaymentCompletedIntegrationEvent.LineItems` always empty from webhook handler.

### `/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs`

- Metadata-driven routing; session completion guards help idempotency.
- Throws if product missing after session complete attempt — can poison outbox.
- Reuses `subscription_id` metadata key for **checkout session id** (naming confusion).

### `/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs`

- Strong ledger-level idempotency on `GatewayTransactionId`.
- Runs for **all** completed payments including utility top-ups — may create tenant-ledger noise depending on org id (platform vs tenant).

### `/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs`

- Missing transaction-level idempotency; package matching by amount threshold is heuristic (partial payments / FX risk).

### `/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`

- Stripe-dispute only in practice; relies on Payments dedupe; clawback amount heuristic mirrors top-up packages.

### `/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`

- Decent MVP outbound; not Svix-grade; not payment-event specific.

### `/apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs` + `WebhookSenderService.cs`

- Outbound tax webhooks; no retry; separate from payment money path.

### `/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs`

- Better inbound webhook security reference implementation (timestamp + fixed-time compare) than Payments.

### Docs

- **006** correctly describes unique index backfill; still mentions Community metadata schema.
- **ADR 004/009** accurately document Billplz silent-drop class; solution is query-string state — acceptable short-term, weak long-term.
- **README** claims fee estimation and `GatewayPaymentFailed` emission that code does not do.

---

## Summary Judgment

The payment webhook system is a **thin verify-and-fanout facade** optimized for a stateless BYOK multi-gateway story. The strongest pieces are:

- Per-tenant secrets and path-based routing  
- Stripe SDK verification + dispute path  
- Billplz ADR-009 query metadata workaround  
- Unique `(Provider, EventId)` log + transactional outbox write  

It is “worse” than industry practice primarily because:

1. **Idempotency is event-scoped, not payment-scoped**, with unsafe Razorpay fallback.  
2. **ACK-before-reliable-fulfillment** via outbox poison handling.  
3. **Incomplete event catalog** (failures/refunds ignored; contracts lie).  
4. **No forensic store / replay / tests**.  
5. **Metadata-only routing** still allows silent non-fulfillment when gateways or off-session flows drop context.  
6. **Inconsistent gateway maturity** (Stripe ≫ Billplz/CHIP/Razorpay).  

A durable redesign is a **two-phase webhook intake** (persist raw → async process) with **payment-level idempotency**, **honest event emission**, and **outbox DLQ**, plus a small **pending-session safety net** for gateways that cannot be trusted to return metadata.
