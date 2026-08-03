<!-- Source subagent: 019fc650-3514-7d71-af79-0676859c0645 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Outbound Customer Webhooks Gap Analysis

**Scope:** Lazuar → customer systems (app/developer webhooks), not inbound payment-gateway or Resend webhooks.  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Product context:** CaaS / Compliance CaaS (ADR 019, ADR 021). Integration-facing webhooks are a declared pillar of “Developer SaaS” fulfillment.

---

## What Exists

There are **three separate webhook-shaped systems**. Only two are *outbound to customers*, and they are **not unified**.

### A. Platform / Commerce “Developer” outbound webhooks (One + Commerce)

| Layer | What it is | Path |
|--------|------------|------|
| Registration | Single URL + auto secret per workspace | `TenantWebhookEndpoint` |
| API | GET/PUT endpoint, GET logs | `/one/workspaces/{id}/webhooks`, `.../logs` |
| UI | Ops “Developer → Outbound Webhooks” + “Delivery Logs” | `DeveloperSettingsPage.tsx`, `DeliveryLogsPage.tsx` |
| Trigger | Product `FulfillmentTargets` containing `http(s)://...` | Commerce product JSONB list |
| Fan-out event | `OutboundWebhookRequestedIntegrationEvent` | Commerce contracts |
| Enqueue | Match endpoint by **exact URL**, write outbox row | `OutboundWebhookEventHandlers` |
| Deliver | Background job, HMAC-SHA256 header | `OutboundWebhookDispatcherJob` |

**Registration model** (`TenantWebhookEndpoint`):

- Fields: `OrganizationId`, `Url`, `SecretKey`, `IsActive`, timestamps  
- API save creates secret once as `whsec_` + secure token; later updates **do not rotate** secret  
- Repo/API assume **one endpoint per org** (`FirstOrDefault` by `OrganizationId`)  
- Schema: `one.TenantWebhookEndpoints` (no unique index on org, no event filters)

**Delivery outbox** (`WebhookDeliveryOutbox`):

- Status machine: `PENDING` → `SUCCESS` / `FAILED`  
- Max **5** attempts, exponential backoff `2^AttemptCount` minutes  
- Signature: `X-Lazuar-Signature` = hex(HMAC-SHA256(payload, secret))  
- HTTP client `"DeveloperWebhooks"`, 15s timeout  
- Poll every **10s**, batch **50**  
- Logs API: last **50** rows; no payload body, no response body, no redelivery

**Commerce event types actually emitted as outbound webhooks:**

| Event type | Source |
|------------|--------|
| `order.completed` | `OrderCompletedIntegrationEventHandler` |
| `subscription.activated` | lifecycle handler |
| `subscription.suspended` | lifecycle + `BillingEngineJob` (no PM → past due) |
| `subscription.canceled` | lifecycle + dunning final action |
| `subscription.resumed` | lifecycle after recovery |
| dunning event strings | `DunningEngineJob` (varies by campaign) |

**Payload shape (thin):**

```json
{
  "id": "<uuid v7>",
  "event_type": "subscription.activated",
  "created_at": "...",
  "data": {
    "subscription_id": "...",
    "client_profile_id": "...",
    "product_id": "...",
    "is_first_payment": true,
    "status": "ACTIVE"
  }
}
```

No amount, currency, customer email/name, product slug/name, gateway ref, tax invoice link, or metadata bag.

**Product UI path:** free-text textarea “Post-Purchase Webhooks” — one URL per line on the product form (`ProductForm.tsx`). Copy says Lazuar POSTs when that product is purchased. **That is incomplete and, given the enqueue logic, often false.**

### B. LHDN module outbound webhooks (module-local)

| Layer | What it is |
|--------|------------|
| Model | `WebhookSubscription` (multi-URL per org) |
| API | POST/GET/DELETE `/api/v1/lhdn/webhooks` (+ SDKs) |
| Dispatch | `DispatchExternalWebhookCommand` from status poller |
| Send | `WebhookSenderService` fire-and-forget HTTP POST |
| Signing | Same `X-Lazuar-Signature` HMAC-SHA256 pattern |
| Events | Only when LHDN poll sees `VALID` or `INVALID` |

Payload:

```json
{
  "event": "invoice.valid" | "invoice.invalid",
  "data": {
    "internal_id": "...",
    "lhdn_uuid": "...",
    "status": "VALID|INVALID",
    "qr_link": "...",
    "error_message": "...",
    "timestamp": "..."
  }
}
```

**No UI** in ops-page for LHDN webhook management (API/SDK only).  
**No delivery log, no retries, no outbox.** Failures are log lines only.

### C. Inbound / internal webhooks (out of scope but often confused)

| System | Direction | Role |
|--------|-----------|------|
| Payments `PaymentWebhookLog` + gateway adapters | **Inbound** | Stripe/Billplz/CHIP/Razorpay → Lazuar; idempotency + signature verify |
| Resend `/webhooks/resend` (Svix) | **Inbound** | Bounce/complaint → suppression list |
| `FulfillmentRequestedIntegrationEvent` | **Internal** | e.g. `internal:COMMUNICATIONS` → dunning email/WhatsApp, not customer HTTP |

These are mature relative to outbound developer webhooks. ADR 004/009 focus entirely on **inbound** gateway metadata, not customer callbacks.

### What the product *claims* vs what ships

ADR 019 explicitly promises:

> “Outbound Webhook Dispatch… HMAC-SHA256 signed payloads to external URLs, saving developers weeks of billing backend engineering.”

What shipped is a **thin prototype**: single workspace URL + product URL list + minimal commerce lifecycle events + a separate LHDN path with weaker delivery. README for One **does not document** outbound webhooks at all.

---

## LHDN Webhook Subscription Model as Reference

### Strengths (relative to platform)

1. **Multi-subscription** per organization (`lhdn.WebhookSubscriptions`, index on `OrganizationId`).  
2. **Explicit developer API surface** in TypeSpec, OpenAPI, and both LHDN SDKs (`packages/lhdn-sdk-ts`, `packages/lhdn-sdk-dotnet`).  
3. **Caller-supplied secret** at register time (`RegisterWebhookRequestDto.secret`) — useful for pre-shared secrets with ERP/middleware.  
4. **Domain-relevant payload** for compliance integrators: `lhdn_uuid`, `qr_link`, status, error.  
5. Soft delete via `Deactivate()` rather than hard delete.  
6. Clear trigger boundary: status terminal states from `LhdnStatusPollingJob`.

### Weaknesses (should not be copied blindly)

| Gap | Detail |
|-----|--------|
| **Fire-and-forget** | `WebhookSenderService` swallows errors; no queue |
| **No retries / DLQ** | One attempt, then silence |
| **No delivery audit** | Cannot prove delivery to customer/support |
| **`events[]` is fiction** | Request DTO requires `events`; domain **does not store** them; list handler **hardcodes** `["invoice.validated","invoice.rejected"]` while actual event names are `invoice.valid` / `invoice.invalid` |
| **Event catalog mismatch** | List DTO claims `invoice.validated` / `invoice.rejected`; wire payload uses `invoice.valid` / `invoice.invalid` |
| **No filter** | Every active subscription gets every terminal status |
| **No submitted/cancelled/credit-note events** | Only VALID/INVALID poll outcomes |
| **No UI** | Ops invoicing module has tax invoices/quotes UI, no webhooks page |
| **Default HttpClient** | No named client timeout policy like One’s `DeveloperWebhooks` |
| **Synchronous send inside poll job** | Blocks status worker on customer latency/timeouts |

### Schema (LHDN)

`Url`, `Secret`, `IsActive`, `CreatedAt` only — no `Events`, `Description`, `FailureCount`, `DisabledAt`, versioned secrets.

### When LHDN fires

Only from `LhdnStatusPollingJob` after MyInvois status is `VALID` or `INVALID`.  
`LhdnDocumentSubmittedIntegrationEvent` / cancelled paths do **not** call `DispatchExternalWebhookCommand`.

---

## Missing Platform-Wide Webhook System

There is **no** BuildingBlocks / SharedKernel webhook primitive:

- No `IWebhookDispatcher` / `IAppWebhookService`  
- No platform event catalog  
- No shared delivery table used by Billing, Commerce, LHDN, Payments  
- No Svix (or equivalent) integration  
- No standardized envelope, headers, or signature versioning across modules  

What exists instead:

```
Commerce lifecycle ──► OutboundWebhookRequestedIntegrationEvent
                         │
                         ▼
                   One.OutboundWebhookEventHandlers
                         │  (silent drop if URL ≠ registered endpoint)
                         ▼
                   one.WebhookDeliveryOutboxes
                         │
                         ▼
                   OutboundWebhookDispatcherJob ──► customer HTTP

LHDN status poll ──► DispatchExternalWebhookCommand
                         │
                         ▼
                   WebhookSenderService ──► customer HTTP (no outbox)
```

### Critical architecture bug: dual registration that does not compose

1. **Developer Settings** registers *one* workspace URL + secret.  
2. **Product form** stores *arbitrary* URLs in `FulfillmentTargets`.  
3. Commerce publishes `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl` = product URL.  
4. One handler only enqueues if:

```csharp
e.OrganizationId == @event.OrganizationId
  && e.Url == @event.TargetUrl
  && e.IsActive
```

**Consequences:**

- Product Zapier/Make URL **≠** workspace URL → **silent drop** (no log, no error UI).  
- Workspace endpoint configured, products empty / without matching URL → **nothing ever delivers**, despite UI copy “when a customer completes a checkout…”.  
- UI implies two features (workspace webhooks *and* per-product webhooks); runtime requires **exact intersection**.  
- Product URLs cannot use the workspace signing secret unless the strings match; secrets are bound to `TenantWebhookEndpoint`, not to product URLs.

This alone makes the integration surface feel “worse than nothing” for real CaaS customers.

### Other structural holes

| Missing capability | Today |
|--------------------|--------|
| Multi-endpoint workspace webhooks | One row-by-convention only |
| Event subscription filters | Always “all commerce events that get enqueued” (if URL matches) |
| Fan-out from **all** products to workspace endpoint | Not implemented |
| Billing → customer webhook | No |
| Payment money events → customer | No (only internal `GatewayPaymentCompleted`) |
| LHDN → shared delivery infrastructure | Parallel poor cousin |
| Cross-module event naming | Commerce `event_type` vs LHDN `event` |
| Idempotency for receivers | Envelope `id` exists for One path; LHDN envelope has no delivery id |
| Dead-letter / auto-disable after N failures | Status `FAILED` only; endpoint stays Active |
| SSRF protections | No URL allowlist, no private-IP block |
| Horizontal safety | Job uses simple poll; no lease/claim (multi-instance double delivery risk) |
| Tests | **Zero** module/integration tests for outbound webhooks |

### Explicit non-events

`custom_payment_link` checkouts complete and **return without** any `OrderCompleted` / outbound webhook (`GatewayPaymentCompletedIntegrationEventHandler`). High-ticket / ad-hoc payments invisible to integrators.

---

## Event Types Customers Would Need

Mapped to CaaS / Compliance CaaS (checkout, subscription, money, tax). **Bold** = partially present.

### Checkout & orders

| Event | Need | Status |
|-------|------|--------|
| **`order.completed`** | Unlock digital good / SaaS seat | Present if product URL ∩ workspace URL |
| `order.refunded` / `payment.refunded` | Revoke access | Missing |
| `checkout.session.completed` | Analytics + CRM | Missing (session model internal only) |
| `payment_link.paid` (custom payment) | High-ticket manual links | Explicitly skipped |

### Subscriptions & dunning

| Event | Need | Status |
|-------|------|--------|
| **`subscription.activated`** | Provision | Present (with caveats) |
| **`subscription.resumed`** | Re-provision after past due | Present |
| **`subscription.suspended`** | Deprovision | Present |
| **`subscription.canceled`** | Deprovision final | Present |
| `subscription.past_due` / `invoice.payment_failed` | Early warning | Collapsed into suspended path; no dedicated fail event |
| `subscription.renewed` / `subscription.cycle_charged` | Renewal analytics | Missing distinct event (may look like re-activate) |
| `subscription.trial_ending` | Nurture | Missing |
| Dunning step fired | CRM workflows | Partial (opaque event strings from dunning job) |

### Money / payments (gateway-agnostic)

| Event | Need | Status |
|-------|------|--------|
| `payment.succeeded` | Accounting sync, licenses | Missing outbound |
| `payment.failed` | Ops alerting | Missing outbound |
| `charge.dispute.created` | Risk ops | Internal event only (`GatewayDisputeCreated…`) |
| `refund.completed` | Access + LHDN credit path | Missing outbound |

### Billing / invoices (merchant-facing)

| Event | Need | Status |
|-------|------|--------|
| `invoice.issued` | ERP / customer portal | Internal `InvoiceIssuedIntegrationEvent` → LHDN only |
| `invoice.paid` / ledger entries | Finance | Missing outbound |
| Consolidated B2C batch issued | Monthly compliance | Internal only |

### LHDN / compliance

| Event | Need | Status |
|-------|------|--------|
| `invoice.submitted` | “In flight to tax authority” | Missing |
| **`invoice.valid`** (wire) | Show QR, claim input tax | Present LHDN-only path |
| **`invoice.invalid`** | Exception handling | Present LHDN-only path |
| `invoice.cancelled` | Reverse ERP doc | Missing outbound |
| Credit/debit/refund note lifecycle | Full compliance loop | Missing outbound |
| Naming consistency | SDK contracts | Broken (`validated`/`rejected` vs `valid`/`invalid`) |

### Platform / identity (lower priority for CaaS)

Workspace member, API key revoked, credit wallet low — not exposed.

### Payload enrichment customers need (all missing today)

- Customer: email, name, phone (with PII policy)  
- Money: amount, currency, fees, gateway transaction id  
- Product: id, slug, name, interval  
- Tax: document internal id, LHDN UUID, QR URL on **payment** success if already available  
- Correlation: checkout session id, idempotency key, merchant `metadata`  
- Environment: live vs test  

Without this, integrators must immediately call back into Lazuar APIs (if those even exist publicly) — defeating “webhook-first CaaS.”

---

## Delivery Guarantees, Signing, Retries

### One path (best of the two)

| Concern | Implementation | Gap vs industry (Stripe/Svix) |
|---------|----------------|-------------------------------|
| At-least-once | Outbox + retries | Yes, but no lease → multi-instance duplicate risk |
| Ordering | Per-tenant FIFO not guaranteed | Global batch by `NextAttemptAt` |
| Retries | 5 attempts, exponential minutes | No jitter; no long-tail (days); no manual redeliver |
| Success criteria | Any 2xx | OK |
| Failure visibility | Status + last error string | No HTTP body; UI fakes “HTTP 200 OK” for success |
| Signing | HMAC-SHA256 over raw body, hex | No timestamp → replay forever; no `t=`/`v1=` style; no multi-secret rotation |
| Headers | Only `X-Lazuar-Signature` | Missing `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Timestamp`, user-agent |
| Idempotency for receiver | Envelope `id` | Not in headers; LHDN path has no delivery id |
| Timeout | 15s | OK |
| Circuit break | None | Endpoint never auto-disabled |
| SSRF | None | Risk if tenant points at metadata endpoints |

### LHDN path

| Concern | Implementation |
|---------|----------------|
| Guarantees | Best-effort, single shot |
| Retries | None |
| Audit | Logs only |
| Signing | Same weak HMAC (no timestamp) |
| Backpressure | Blocks poll job |

### Inbound contrast (how good Lazuar is when *receiving*)

Payments: signature verification, `PaymentWebhookLog` idempotency, metadata reconstruction (ADR 009).  
Communications: Svix-style Resend verification with timestamp window.  

**Asymmetry:** Lazuar is careful as a webhook *consumer* and casual as a webhook *producer*.

### Delivery log UX gaps

- No request/response payload inspection  
- No “Resend” button  
- No filter by event type/status  
- Success row hardcodes “HTTP 200 OK” even if status was 204  
- No attempt timeline  

---

## Management API & UI Gaps

### One / Developer (ops-page)

| Capability | Status |
|------------|--------|
| Configure single URL | Yes |
| Active toggle | Yes |
| View signing secret | Always returned on GET (no one-time reveal) |
| Rotate secret | No |
| Multiple endpoints | No |
| Event multi-select | No |
| Test ping / sample event | No |
| Delivery logs list | Yes (shallow) |
| Log detail / redeliver | No |
| API key auth for webhook management | Workspace session only (not public developer API surface for One webhooks) |
| Public docs / developers-page content | Product OpenAPI via Scalar; **no webhook guide** / examples in repo docs |
| Product-level URL textarea | Exists but **broken** relative to dispatcher |

### LHDN

| Capability | Status |
|------------|--------|
| Register / list / delete via API + SDKs | Yes |
| Ops UI | **No** |
| Event selection stored | **No** (DTO theater) |
| Delivery logs | **No** |
| Secret rotation | Delete + re-register only |

### TypeSpec / contracts

- One: `WebhookEndpointDto`, `SaveWebhookEndpointRequestDto`, `WebhookDeliveryLogDto` — minimal.  
- Lhdn: `RegisterWebhookRequestDto.events` unused by domain.  
- No shared `WebhookEventDto` catalog in `packages/api-spec`.  
- Commerce models only expose **inbound** gateway `webhook_secret` fields (payment config), not outbound.

### SDKs

- LHDN SDKs include webhook CRUD.  
- No first-class “verify Lazuar signature” helper documented in repo for receivers.  
- No commerce/platform webhook SDK.

---

## Industry Comparison

| Capability | Stripe | Svix (generic) | Lemon Squeezy / Paddle | Lazuar today |
|------------|--------|----------------|------------------------|--------------|
| Multi endpoints | Yes | Yes | Yes | Partial (LHDN only; One = 1) |
| Event catalog + filters | Yes | Yes | Yes | No |
| Signed + timestamped | Yes | Yes | Yes | HMAC only |
| Secret rotation | Yes | Yes | Yes | No |
| Delivery log + body | Yes | Yes | Yes | Status only (One) |
| Automatic retries + redeliver | Yes | Yes | Yes | Basic (One) / none (LHDN) |
| Disable on failure | Yes | Yes | Yes | No |
| Test events / CLI | Yes | Yes | Yes | No |
| Rich money + customer payload | Yes | N/A | Yes | IDs only |
| Unified product + tax events | Stripe Tax / separate | N/A | Limited | Split LHDN vs Commerce |
| Docs & SDKs for verification | Excellent | Excellent | Good | Minimal / inconsistent |
| SSRF / endpoint validation | Yes | Yes | Yes | No |

For SEA CaaS specifically, competitors often win **because** gateways are weak on webhooks (ADR 019). Lazuar’s promise was to *be* the robust webhook layer. The current implementation does not clear that bar.

---

## Recommendations for Solid Integration Surface

### P0 — Make the existing path truthful (ship blockers)

1. **Unify trigger model**  
   - Workspace endpoint receives **all** relevant workspace events (no product URL required).  
   - Product-level URLs either:  
     - (a) become additional `TenantWebhookEndpoint`s with optional product filter, or  
     - (b) deliver with a **per-URL secret** stored on product config, **without** requiring equality to the workspace URL.  
   - **Delete or fix** the silent `endpoint == null return` path — enqueue failures must appear as failed deliveries or validation errors.

2. **Fix event contract bugs**  
   - Align LHDN wire names with API list (`invoice.validated` vs `invoice.valid`) and TypeSpec.  
   - Persist or drop `events[]` — do not hardcode lies.  
   - Emit outbound for `custom_payment_link` paid.

3. **Enrich payloads** to a documented minimum (ids + customer email + amount/currency + product slug + status + gateway payment id).

4. **Header convention (v1)**  
   - `X-Lazuar-Signature` → migrate to `X-Lazuar-Signature: t=…,v1=…` (timestamp + HMAC)  
   - `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`  
   - Document verification algorithm next to developers-page.

### P1 — Platform webhook module (do not keep forking)

Introduce a single **Webhooks** building block or module used by Commerce, Billing, LHDN:

- Tables: `WebhookEndpoints`, `WebhookEndpointEvents`, `WebhookDeliveries` (payload, attempts, response code/body snippet)  
- `IWebhookPublisher.PublishAsync(orgId, eventType, payload)` called from each domain  
- Shared dispatcher job (claim/lease for multi-instance)  
- Retries: e.g. 1m, 5m, 30m, 2h, 24h; mark endpoint `disabled` after consecutive failures  
- API: CRUD endpoints, rotate secret, list events, get delivery, redeliver, send test  

**Migrate LHDN** onto this infrastructure; retire `WebhookSenderService` fire-and-forget.

### P2 — Event catalog for Compliance CaaS

Publish a frozen catalog, versioned:

```
checkout.session.completed
order.completed
order.refunded
payment.succeeded
payment.failed
subscription.activated
subscription.renewed
subscription.past_due
subscription.suspended
subscription.canceled
subscription.resumed
invoice.issued
lhdn.document.submitted
lhdn.document.validated
lhdn.document.invalid
lhdn.document.cancelled
```

Filter per endpoint. Document on developers-page with sample signatures.

### P3 — Product experience

- Ops: multi-endpoint UI, event checkboxes, secret rotate, test send, delivery detail + resend  
- LHDN webhooks UI under Invoicing/Developer  
- Product form: remove misleading free-text webhooks **or** replace with “also notify these URLs” backed by real multi-endpoint storage  
- Optional: portal webhook status for white-label? (low priority)

### P4 — Build vs buy

| Option | When |
|--------|------|
| **In-house outbox** (extend One) | Solo-founder scale, already half-built; keep control of MY data residency |
| **Svix / Standard Webhooks** | If reliability/UI becomes the bottleneck; still need domain event emission layer |

Prefer in-house v1 that matches **Standard Webhooks** signature format for ecosystem tooling, rather than inventing a third dialect forever.

### P5 — Hardening & quality

- SSRF: block link-local / private ranges (with override for enterprise)  
- HTTPS-only in production  
- Unit tests for signing + retry state machine  
- Integration test: activate subscription → outbox → signed POST  
- Architecture test: no second fire-and-forget sender without outbox  

---

## File Evidence Notes

### One (workspace outbound)

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs` | Single-endpoint aggregate |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs` | Retry / status machine (max 5) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Application/Commands/SaveWebhookCommand.cs` | Create secret once; no rotation |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs` | **Exact URL match or silent drop** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs` | HMAC + POST loop |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | GET/PUT webhooks, GET logs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Services/OneQueryService.cs` | Single endpoint + last 50 logs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/DependencyInjection.cs` | `DeveloperWebhooks` client; job + subscription |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Migrations/20260627124757_InitialOneSchema.cs` | Schema for endpoints + outbox |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/README.md` | **Omits** outbound webhooks |

### Commerce (emit path)

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Contracts/Events/OutboundWebhookRequestedIntegrationEvent.cs` | Integration event |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Contracts/Events/FulfillmentRequestedIntegrationEvent.cs` | Internal apps only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/OrderCompletedIntegrationEventHandler.cs` | `order.completed` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Application/EventHandlers/SubscriptionLifecycleIntegrationEventHandlers.cs` | activate/suspend/cancel/resume |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | past due → webhook/internal |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` | dunning webhooks + `FulfillmentRequested` to COMMUNICATIONS |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` | Payment success; **custom_payment_link early return** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/modules/commerce/components/ProductForm.tsx` | Free-text product webhook URLs |

### LHDN (module-local outbound)

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Domain/Aggregates/WebhookSubscription.cs` | Multi-URL model |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs` | Register/delete; **ignores events** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs` | Builds `invoice.{status}` payload |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs` | Fire-and-forget HMAC POST |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | Only VALID/INVALID dispatch |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Lhdn/Application/Queries/LhdnQueries.cs` | Hardcoded events list |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/lhdn/models.tsp` | `RegisterWebhookRequestDto.events` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/lhdn/routes.tsp` | CRUD routes |

### UI / contracts

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | Workspace webhook settings |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | Shallow logs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/components/Sidebar.tsx` | Developer nav |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/one/models.tsp` | Webhook DTOs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/one/routes.tsp` | Webhook routes |

### Strategy docs

| File | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | Promises HMAC outbound for SaaS |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/021-compliance-caas-pivot.md` | Compliance at POS; implies tax status visibility |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/004-…` / `009-…` | **Inbound** gateway webhooks only |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/docs/architecture-decision-log/014-apps.md` | LHDN `WebhookSubscription` listed |

### Not found

- BuildingBlocks webhook dispatcher  
- Tests under `apps/lazuar-api/tests` for outbound customer webhooks  
- Ops UI for LHDN webhook subscriptions  
- Secret rotation / test-event APIs  
- Platform event catalog in TypeSpec  

---

## Bottom line

Outbound customer webhooks exist as a **fragmented prototype**, not a CaaS-grade integration surface.

1. **Two delivery stacks** (One outbox vs LHDN fire-and-forget) with **incompatible envelopes**.  
2. **Fatal product bug:** workspace registration and product URLs must match exactly or events vanish.  
3. **Event coverage** is a thin slice of subscription lifecycle + LHDN terminal status; money, refunds, custom payments, invoice issued, and most compliance transitions are invisible.  
4. **Payloads are ID-only**; integrators cannot fulfill SaaS access without extra API round-trips (if any).  
5. **Signing/retries/logs/docs** lag Stripe/Svix and even lag Lazuar’s own inbound payment quality.  

Until P0–P1 land, telling developers “connect Lazuar webhooks to unlock your product” is **false advertising relative to the code paths that actually run**.
