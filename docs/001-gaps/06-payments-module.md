<!-- Source subagent: 019fc650-3511-7762-8927-4f42319aed74 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Payments Module Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/` and related contracts/events consumed by Commerce, Billing, LHDN, and ops UI.  
**Architecture intent (from module README + ADRs 004/009):** Payments is a **stateless gateway orchestrator** (“Cashier”) — BYOK credentials, checkout/portal session generation, webhook verification/idempotency, fee extraction, and universal money-movement events. It is **not** a ledger, subscription engine, or fulfillment engine.

---

## Module Inventory

### Layer structure

| Layer | Path | Role |
|--------|------|------|
| **Contracts** | `Modules/Payments/Contracts/` | Integration events, checkout/portal/system queries (plus mis-namespaced command/query types) |
| **Domain** | `Modules/Payments/Domain/` | `TenantPaymentConfiguration`, `PaymentWebhookLog` |
| **Application** | `Modules/Payments/Application/` | Ports (`IPaymentGatewayAdapter`, repos), webhook command, checkout/portal/system/agent handlers |
| **Infrastructure** | `Modules/Payments/Infrastructure/` | EF (`payments` schema), adapters (Stripe/Billplz/CHIP/Razorpay), factory, webhook endpoints, config CRUD, off-session/refund handlers, inbox/outbox workers |
| **UI (ops)** | `apps/ops-page/.../PaymentSettingsPage.tsx` | BYOK credential vault UI |
| **Consumers** | Commerce, Billing, LHDN | Subscribe to payment integration events / issue MediatR queries |

### Domain model (thin)

- **`TenantPaymentConfiguration`**: per-tenant, per-gateway BYOK (`ApiKey`, `WebhookSecret`, `MerchantId`), unique `(OrganizationId, GatewayType)`.
- **`PaymentWebhookLog`**: idempotency ledger unique `(Provider, EventId)`.
- **Removed** (migration `RemoveAccountingOverrides`): `IsActive`, `EstimatedFeePercentage`, `FixedFee`, `TaxRate` — fee estimation for Billplz now hard-coded to **0** at webhook processing time.

### Application capabilities

| Capability | Entry | Notes |
|------------|--------|------|
| Tenant checkout | `GenerateCheckoutSessionQuery` | Default gateway `BILLPLZ` |
| Platform top-up checkout | `GenerateSystemCheckoutSessionQuery` | System tenant `00000000-...-0001` |
| Customer portal | `GenerateCustomerPortalQuery` | **Stripe only** in handler |
| Webhook ingest | `POST /webhooks/payments/{gatewayType}/{tenantId}` | Query string → `Query-*` headers (Billplz metadata ADR) |
| Config CRUD | `GetPaymentConfigQuery` / `UpdatePaymentConfigCommand` | Tenant admin + platform group |
| Agent tool | `GetPaymentConfigAgentQuery` | Lists configured gateway names only |
| Off-session charge | `ExecuteOffSessionChargeIntegrationEvent` (inbox) | From Commerce billing/dunning jobs |
| Refunds | `GatewayRefundRequestedIntegrationEvent` (inbox) | **No publisher found in Commerce** |

### Gateways registered

`StripeGatewayAdapter`, `BillplzGatewayAdapter`, `ChipCollectGatewayAdapter`, `RazorpayGatewayAdapter` via `PaymentGatewayFactory`.

### Integration events (Contracts)

| Event | Published by Payments? | Subscribers found |
|-------|------------------------|-------------------|
| `GatewayPaymentCompletedIntegrationEvent` | Yes (webhook) | Commerce, Billing (ledger + platform top-up) |
| `GatewayDisputeCreatedIntegrationEvent` | Yes (Stripe dispute webhook) | Billing chargeback clawback |
| `GatewayPaymentFailedIntegrationEvent` | **Defined only — never published** | None |
| `GatewayRefundRequestedIntegrationEvent` | Consumed by Payments | **No publisher in codebase** |
| `GatewayRefundCompletedIntegrationEvent` | Yes (refund handler, broken amounts) | Billing ledger, Commerce transaction log, LHDN credit-note/cancel |
| `GatewayRefundFailedIntegrationEvent` | Yes (refund handler) | **No subscribers** |
| `ExecuteOffSessionChargeIntegrationEvent` | Consumed by Payments | Published by Commerce `BillingEngineJob` / `DunningEngineJob` |
| `ApiCreditPurchasedIntegrationEvent` | **Never published** | Handler exists in Billing but **not subscribed** in DI |

### DB schema (`payments`)

- `TenantPaymentConfigurations`
- `PaymentWebhookLogs`
- `OutboxMessages` / `InboxMessages` (platform box pattern)

---

## Gateway Adapter Abstraction Quality

### Strengths

1. **Single port** `IPaymentGatewayAdapter` with a consistent surface: checkout, webhook parse, refund, portal, off-session.
2. **Factory** by normalized `GatewayType` string — product-level gateway routing works (`Product.GatewayName`).
3. **Normalized webhook result** `GatewayWebhookParsedResult` unifies amounts, fees, FX, customer/token IDs.
4. **BYOK-first**: API keys never come from host appsettings for tenant commerce; adapters always take `apiKey` as parameter.
5. **Billplz metadata workaround** is intentional and documented (ADR 004/009): callback URL query + `Query-*` headers.
6. **CHIP onboarding automation**: on save, fetch RSA public key + register webhook events.

### Weaknesses / design debt

| Issue | Detail |
|-------|--------|
| **Interface is capability-blind** | Portal / off-session / refunds throw or return `false` on unsupported gateways instead of capability flags. Callers cannot discover support without try/catch or hardcoding. |
| **Fee estimation params are dead** | `ParseWebhookAsync(..., estimatedFeePercentage, fixedFee, taxRate)` still on port; config columns removed; webhook handler always passes `0,0,0`. |
| **No capability matrix type** | No `SupportsOffSession`, `SupportsPortal`, `SupportsPartialRefund`, `SupportsDisputes`. |
| **No CancellationToken** | All adapter methods lack CT — poor for long HTTP/SDK calls. |
| **No typed money / minor units** | `amount * 100` / `(long)` everywhere — zero-decimal currencies (JPY/KRW) and rounding will mis-charge. |
| **LineItems always empty** | `GatewayPaymentCompleted` always publishes `LineItems: new List<LineItemDto>()` — contract field unused. |
| **Gateway name defaults leak domain** | Query defaults to `"BILLPLZ"`; portal hardcodes Stripe; refund default `"STRIPE"`. |
| **Secrets model is single-string** | Razorpay requires `KeyId:KeySecret` concatenated; Stripe uses `SecretKey` vs `ApiKey` overload in update handler — fragile. |
| **Duplicate event type** | `Modules.Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` exists **without** `GatewayName`; workers correctly publish **Payments** contracts version — dead Commerce type is a trap. |
| **Layering smell** | `UpdatePaymentConfigCommand` lives under Contracts path but namespace `Modules.Payments.Application.Commands`. `GetPaymentConfigQuery` same mismatch. |

**Quality rating:** usable multi-gateway orchestration for **hosted checkout + verified webhooks**, immature for **recurring / refunds / disputes / fee fidelity**.

---

## Per-Gateway Capability Matrix

| Capability | Stripe | Billplz | CHIP Collect | Razorpay |
|------------|--------|---------|--------------|----------|
| Hosted checkout | Yes (`checkout.session` mode=payment) | Yes (bills API) | Yes (purchases) | Yes (payment links / registration links) |
| Metadata in webhook body | Yes | **No** (reconstructed from URL/ref) | Yes (`purchase.metadata`) | Yes (`notes`) |
| Signature verification | Yes (`Stripe-Signature`) | Yes (HMAC SHA256 x_signature) | Yes (RSA PEM public key) | Yes (`X-Razorpay-Signature`) |
| Exact gateway fees | Yes (balance_transaction expand on session path) | **No** (estimation stubbed → always 0) | Yes (`payment.fee_amount` / `net_amount`) | Yes (`fee`/`tax` on payment entity) |
| Tax amount | Session total details | No | No | Payment `tax` |
| FX rate | Balance transaction exchange rate | Fixed 1 / MYR | Fixed 1 | Fixed 1 |
| Setup future usage / vault | Yes (`setup_future_usage=off_session`) | **No** | Yes (`force_recurring`, `skip_capture` for 0) | Yes (subscription registration link) |
| Customer ID on webhook | Session / PI customer | No | **Always null** | From payment entity |
| Token / PM on webhook | Payment method id | No | Purchase id if `is_recurring_token` | `token_id` |
| Off-session charge | Yes (PI confirm off_session) | **Throws NotSupported** | Yes (create purchase + charge token) | Yes (recurring payment) |
| Customer billing portal | Yes | Throws | Throws | Throws |
| Refunds API | Yes (by PaymentIntent) | **Always false** | Yes (purchase refund) | Yes (payment refund) |
| Partial refund amount | Accepts amount | N/A | Accepts amount | Accepts amount |
| Refund webhook → event | **Not handled** | N/A | `payment.refunded` registered but **not mapped** | Not handled |
| Disputes / chargebacks | `charge.dispute.created` → event | No | No | No |
| Payment failure event | Not mapped to internal fail publish | Parsed as `PAYMENT_FAILED` but **dropped** | Same | Non-`payment.captured` ignored |
| Sandbox/prod switching | Via key (sk_test/live) | URL heuristic on `App:ApiBaseUrl` contains `lazuar.com` | Single production API host | Via key |
| Webhook auto-provision | Manual secret in dashboard | Manual | **Auto on config save** | Manual |

---

## Checkout & Portal Flows

### Tenant commerce checkout

```
Commerce InitiateCheckout
  → (optional CRM ResolveClientProfile, coupon, CheckoutSession persist)
  → GenerateCheckoutSessionQuery(tenant, amount, product.GatewayName, metadata)
  → TenantPaymentConfiguration BYOK
  → adapter.GenerateCheckoutAsync → redirect URL
```

**Metadata contract used by Commerce fulfillment:**

```text
type: commerce_subscription | custom_payment_link
subscription_id: CheckoutSession.Id (or existing sub id for recoveries)
tenant_id: OrganizationId
(+ dunning_campaign_id on arrears update-payment)
setupFutureUsage: product.Interval != "one_time"
```

**Gaps:**

1. **Custom payment links hardcode gateway `"BILLPLZ"`** in `InitiateCheckoutCommandHandler` — ignores product gateway / multi-gateway config.
2. **Update-payment public endpoint** (`PublicEndpoints`) uses `GenerateCheckoutSessionQuery` **without** `GatewayName` → **defaults to BILLPLZ**, even if subscription was vaulted on Stripe/CHIP/Razorpay.
3. **Zero-amount path** bypasses Payments entirely (`ProcessZeroAmountCheckoutCommand`) — correct for free coupons, but no vault setup for free trials that need a card on file.
4. **Success URL trust**: no server-side session reconciliation on redirect; fulfillment is webhook-only (correct for S2S) but UX can show “success” before webhook.
5. **Stateless Payments** means no local “pending payment” entity — recovery depends entirely on gateway metadata fidelity (Billplz URL trick is critical).

### Platform utility credit top-up

```
Billing POST /credits/top-up
  → GenerateSystemCheckoutSessionQuery
  → config for system tenant 000...001 + gateway default BILLPLZ
  → metadata type=utility_credit_topup + tenant_id
  → webhook → GatewayPaymentCompleted
  → PlatformTopUpEventHandler grants credits by package thresholds
```

**Gaps:**

1. System checkout **always uses default gateway BILLPLZ** unless caller overrides (Billing does not pass gateway).
2. Customer email hard-coded empty string for top-up checkout.
3. Platform keys live in same table as tenant BYOK under a magic organization id — no dedicated platform secret store.

### Customer portal

```
Commerce SubscriberEndpoints → GenerateCustomerPortalQuery
  → forces STRIPE config only
  → Stripe Billing Portal by email lookup (first customer)
```

**Gaps:**

1. Email-based customer lookup is ambiguous (multiple customers / guest checkouts).
2. No portal for CHIP/Razorpay/Billplz — expected, but no Lazuar-hosted “manage payment method” alternative.
3. Does not use vaulted `VaultedCustomerId` from subscription — only email.

---

## Off-Session / Dunning Charge Path

### Intended flow

```
Commerce BillingEngineJob (hourly)
  ACTIVE sub, NextBillingDate <= now, vault present
  → ChargeAttemptLog (1/day)
  → ExecuteOffSessionChargeIntegrationEvent (Payments contract, product.GatewayName)

Commerce DunningEngineJob (hourly)
  PAST_DUE AUTOCHARGE steps, max 4 attempts
  → same event with DunningCampaignId

Payments ExecuteOffSessionChargeIntegrationEventHandler
  → adapter.ChargeOffSessionAsync(...)
  → success/fail only logged (no domain events)

Expected: gateway webhook PAYMENT_COMPLETED → Commerce renews subscription
```

### Critical correctness gaps

1. **Off-session metadata does not include Commerce correlation keys**  
   Stripe/CHIP attach only:
   - `receipt` = subscription id string  
   - optional `dunning_campaign_id`  
   Commerce handler **requires** `type ∈ {commerce_subscription, custom_payment_link}` **and** `subscription_id`.  
   **Result: successful auto-renewals and dunning auto-charges will not advance/activate subscriptions** when only `payment_intent.succeeded` / CHIP charge webhooks fire.

2. **BillingEngineJob does not mark PAST_DUE on charge failure**  
   If off-session fails, subscription remains `ACTIVE` with overdue `NextBillingDate`. Dunning’s `PAST_DUE` path never starts. Same-day re-attempt blocked by `ChargeAttemptLog`.

3. **BillingEngineJob does not optimistically advance billing period**  
   Even on success it relies solely on webhook side-effects (which are broken per #1).

4. **Stripe PI path skips fee expansion**  
   For `payment_intent.succeeded` (not checkout session), adapter returns `GatewayFee: 0`, incomplete economics for ledger.

5. **CHIP `GatewayCustomerId` always null** on webhook — vault storage may store empty customer + token=purchaseId. Off-session re-fetch uses token as purchase id (works for CHIP), but Stripe requires both customer + payment method.

6. **Billplz cannot auto-renew** — throws; any product set to BILLPLZ with recurring interval will always fall into “no vault” path and PAST_DUE.

7. **Razorpay off-session** uses hard-coded `billing@lazuar.com` / `0000000000` for recurring payment payload — operational risk.

8. **No failure integration event** when charge fails — Commerce cannot drive dunning from payment outcome; dunning is calendar/status driven only.

9. **Handler returns silently** if config missing — no `GatewayPaymentFailed`, no charge attempt failure log in Payments.

---

## Refunds & Chargebacks

### Refund path (as implemented)

```
(??) publish GatewayRefundRequestedIntegrationEvent
  → Payments GatewayRefundRequestedIntegrationEventHandler
  → IssueRefundAsync(apiKey, transactionId, amount: ALWAYS 0)
  → on success: GatewayRefundCompleted(RefundedAmount: 0, Currency: "MYR", fees 0)
  → Billing posts zero-amount ledger; LHDN may generate CN with unit_price 0
```

**Gaps (severe):**

| Gap | Impact |
|-----|--------|
| **No publisher** of `GatewayRefundRequested` in Commerce/Billing endpoints/commands | Refund pipeline is dead code end-to-end |
| **Amount always 0** to gateway and completed event | Partial/full refunds incorrect; LHDN credit notes worthless amounts |
| **Billplz refund always false** | Failures for MY-first gateway |
| **No refund webhooks** | Async refunds / gateway-initiated refunds never complete domain state |
| **Commerce refund log match** uses `ExternalReference == PaymentRecordId.ToString()` while payment logs use **gateway transaction id** | Even completed events may not flip transaction status |
| **`GatewayRefundFailed` has no consumers** | Silent operator blind spot |
| **Currency hard-coded MYR** on completed event | Multi-currency products broken |

### Chargebacks / disputes

- Stripe only: `charge.dispute.created` → `GatewayDisputeCreatedIntegrationEvent`.
- Billing `ChargebackClawbackHandler` only for `metadata.type == utility_credit_topup` (platform credits).  
  **Commerce subscription disputes do not suspend access or reverse ledger entries.**
- Dispute PI metadata may lack `type`/`tenant_id` if original PI was off-session with sparse metadata.
- No dispute won/lost lifecycle.

---

## Tenant Payment Configuration (BYOK secrets handling)

### Model

- One row per `(OrganizationId, GatewayType)`.
- Fields: `ApiKey`, `WebhookSecret`, `MerchantId` (collection/brand).
- Stripe: `SecretKey` form field replaces `ApiKey` via update handler branch.
- Razorpay: single `ApiKey` as `keyId:keySecret`.
- CHIP: auto-fetches public key into `WebhookSecret`; auto-registers webhooks to  
  `{ApiBaseUrl}/webhooks/payments/chip/{organizationId}` (localhost rewritten to `lazuar-local-dev.com`).

### Surfaces

| Surface | Path |
|---------|------|
| Tenant ops UI | `GET/PUT /admin/commerce/payment-config` |
| Platform admin | `GET/PUT /api/v1/platform/payment-config` (same command, `ctx.TenantId`) |
| Agent | gateway names only |

### Gaps

1. **No encryption at rest** — plaintext `text` columns; README claims “encrypted API keys” but **no crypto converter / KMS / envelope** in module.
2. **Masking only on read** (`••••` + last 4) — update preserves secrets if mask/empty sent (good), but logs/DB dumps still full secrets.
3. **`IsActive` removed** — cannot disable a gateway without deleting credentials.
4. **No validation of key format** server-side (except CHIP remote call); Billplz 128-char check is **frontend only**.
5. **No multi-key / rotation** (publishable vs secret, test vs live flags).
6. **System tenant BYOK** shares schema with customers — privilege isolation depends on callers knowing magic GUID.
7. **`IgnoreQueryFilters()` everywhere** on config/webhook repos — intentional for webhooks/system, but expands blast radius if tenant context wrong.
8. **PlatformEndpoints** mixes **superadmin auth (Dapper into `one.GlobalUsers`)** with payment config inside Payments infrastructure — boundary violation / wrong module ownership.
9. **No test-mode indicator** in UI/API responses.

---

## Workers & Reliability

### Payments workers

- `PaymentsOutboxPublisherJob` / `PaymentsInboxConsumerJob` — thin wrappers over BuildingBlocks platform jobs.
- Outbox uses `FOR UPDATE SKIP LOCKED`, batch 20, 5s poll, **always marks processed even on failure** (poison-message protection) with `Error` field set.
- Trigger via `DatabaseJobTrigger` after SaveChanges.

### Strengths

- Transactional outbox for webhook → multi-module fan-out.
- Webhook idempotency unique index `(Provider, EventId)`.
- ADR history shows awareness of runtime type dispatch and metadata pitfalls.

### Gaps

| Gap | Detail |
|-----|--------|
| **Idempotency race** | Check `HasBeenProcessed` then insert — concurrent webhooks can dual-publish before unique index fails; insert exception may surface as 500 and gateway retries (may be OK) but incomplete transaction handling not explicit |
| **Failed outbox never retried** | ProcessedAt always set → permanent drop of integration events on handler exception |
| **No dead-letter / alert pipeline** | Error string on row only |
| **Webhook returns 200 only after command success** | Good for gateway retries; InvalidOperationException for bad signature may return non-2xx depending on global exception middleware (not specialized) |
| **Billplz EventId = billId** | Replays of same bill are blocked (good); multiple payments on one bill unlikely but limited |
| **CHIP event id = root id** | Depends on provider uniqueness across event types |
| **No raw webhook body archive** | Only EventId + Provider; hard to reprocess or audit |
| **Fee estimation removal** | Billplz ledger always gross=net for fees |
| **Hourly billing/dunning jobs outside Payments** | Coupled reliability of Commerce timers; no jitter/backoff |

---

## Cross-Module Event Contracts

### Happy path (first payment)

```
Payments.GatewayPaymentCompleted
  ├─ Commerce: complete CheckoutSession / create Subscription|Order, vault tokens, transaction log
  └─ Billing: double-entry ledger + B2C receipt document
```

### Platform top-up

```
Payments.GatewayPaymentCompleted (type=utility_credit_topup)
  └─ Billing.PlatformTopUpEventHandler → credits + system expense ledger
```

### Disputes (credits only)

```
Payments.GatewayDisputeCreated
  └─ Billing.ChargebackClawbackHandler
```

### Refunds (theoretical)

```
[missing publisher]
  → GatewayRefundRequested → Payments adapter
  → GatewayRefundCompleted
      ├─ Billing ledger reverse
      ├─ Commerce transaction → REFUNDED
      └─ LHDN cancel/CN
```

### Contract quality issues

1. **`GatewayPaymentFailed` is dead** — adapters detect failures; handler early-returns without publish.
2. **`ApiCreditPurchased` is dead** — superseded by `GatewayPaymentCompleted` + platform top-up handler; leftover type + unsubscribed Billing handler.
3. **`LineItemDto` unused** — revenue type classification never flows from Payments.
4. **OrganizationId vs TenantId naming** — events mix `OrganizationId` (payments) with `TenantId` (off-session); same GUID, easy confusion.
5. **Webhook organization is path tenantId**, not metadata tenant — system top-up webhooks hit **system tenant path**, credits granted via **metadata.tenant_id** (correct pattern) but Billing ledger for general payments posts to **event OrganizationId** (system) for non-top-up too if someone used system keys incorrectly.
6. **Commerce duplicate `ExecuteOffSessionCharge`** without gateway field — architectural footgun.
7. **No versioning** on event contracts / no schema evolution policy.
8. **Docs still say `community_subscription`** in places; code uses `commerce_subscription` — migration/docs drift.

---

## Security of Stored Credentials

| Control | Status |
|---------|--------|
| TLS to gateways | Assumed via HTTPS SDK/HTTP clients |
| Webhook signature verify before side effects | Yes (all adapters) |
| Secrets encrypted at rest in Postgres | **No** |
| Secrets in app logs | Risk: error bodies may include API responses; refund/charge failures log transaction ids |
| Masked API responses | Yes (last 4) |
| RBAC on config endpoints | Relies on admin commerce / platform auth (not re-audited here) |
| Tenant isolation on config | Unique org+gateway; global query filter bypassed in repositories |
| Webhook auth beyond signature | Tenant GUID in URL is public knowledge; security = signature secret |
| Key rotation / revoke | Manual overwrite only |
| PCI scope | Hosted checkout / vaulted tokens at gateway — good; storing sk_live plaintext increases breach impact |
| README accuracy | Claims encryption — **misleading** |

**Highest risk:** full Stripe/CHIP/Razorpay secrets in cleartext DB + any DB backup / SQL injection / over-privileged support access = complete payment takeover per tenant.

---

## Gaps & Recommendations (Prioritized)

### P0 — Correctness / money movement broken or silent

1. **Fix off-session → webhook correlation**  
   On `ChargeOffSessionAsync`, always set metadata:  
   `type=commerce_subscription`, `subscription_id`, `tenant_id`, optional `dunning_campaign_id`.  
   Optionally publish a synchronous success event with those fields if webhooks are delayed.

2. **BillingEngineJob failure path**  
   On failed off-session (need a failure event from Payments), mark `PAST_DUE` and start dunning; do not leave ACTIVE+overdue forever.

3. **Wire refund request end-to-end**  
   Commerce/admin “Refund” command → publish `GatewayRefundRequested` with **real amount, currency, gateway name, gateway transaction id**.  
   Stop hardcoding `amount=0` / `MYR` in Payments handler.

4. **Publish `GatewayPaymentFailed`** when adapters return `PAYMENT_FAILED` (and optionally off-session failures) so Commerce can drive dunning/UX.

5. **Fix update-payment + custom checkout gateway selection**  
   Use product/subscription gateway, not BILLPLZ default.

### P1 — Security & ops

6. **Encrypt BYOK secrets at rest** (AES-GCM/data-protection with platform master key or KMS); never log secrets; document rotation.

7. **Restore or replace fee profile** for Billplz (and any fee-less gateways) so Billing ledger fees are not always 0.

8. **Outbox retry policy** — don’t permanently mark poisoned messages processed without alerts; max retries + DLQ table.

9. **Archive raw webhook payloads** (encrypted, retention TTL) for reprocessing/audit.

### P2 — Product completeness

10. **Capability discovery API** (`supports_off_session`, `supports_portal`, …) for product UI gating.  
11. **Dispute handling for commerce** (suspend access / reverse revenue), not only credit clawback.  
12. **Refund webhooks** (Stripe `charge.refunded`, CHIP `payment.refunded`, Razorpay refund events).  
13. **Remove dead contracts/handlers** (`ApiCreditPurchased*`, Commerce duplicate off-session event) or implement fully.  
14. **Portal by customer id** from vault, not email search.  
15. **Minor-unit / multi-currency money helper** shared across adapters.  
16. **Automated Stripe webhook endpoint registration** parity with CHIP (optional).  
17. **Tests** for adapters (signature vectors), webhook idempotency, off-session metadata, refund amounts — currently almost no Payments module tests.

### P3 — Hygiene

18. Align README with reality (encryption, fee extraction, event list).  
19. Move platform superadmin auth out of Payments `PlatformEndpoints`.  
20. Put commands/queries namespaces and folders in sync; drop dead fee parameters or reintroduce config.  
21. Re-add `IsActive` or soft-disable for gateways.

---

## File-by-File Notes

### Domain

| File | Notes |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs` | Minimal aggregate; plaintext secrets; no IsActive/fee fields; `UpdateCredentials` can change GatewayType (odd given unique index on type). |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Domain/Entities/PaymentWebhookLog.cs` | Solid idempotency entity; no payload/status/tenant columns. |

### Application ports & handlers

| File | Notes |
|------|------|
| `.../Application/Ports/IPaymentGatewayAdapter.cs` | Core port + DTOs; fee params vestigial; no capabilities/CT. |
| `.../Application/Ports/IPaymentRepositories.cs` | Config + webhook log; SaveChanges on log repo couples outbox commit. |
| `.../Application/Commands/ProcessGatewayWebhookCommand.cs` | Thin command. |
| `.../Application/Commands/ProcessGatewayWebhookCommandHandler.cs` | **Key orchestrator**: verifies, filters only COMPLETED/DISPUTE, idempotency, publishes events; **drops PAYMENT_FAILED**; fee args hardcoded 0; empty LineItems. |
| `.../Application/Queries/GenerateCheckoutSessionQueryHandler.cs` | Clean BYOK checkout. |
| `.../Application/Queries/GenerateSystemCheckoutSessionQueryHandler.cs` | Magic system tenant GUID; injects top-up metadata. |
| `.../Application/Queries/GenerateCustomerPortalQueryHandler.cs` | Stripe-only hardcode. |
| `.../Application/Queries/Agent/GetPaymentConfigAgentQuery.cs` | Low-risk; exposes gateway names only. |
| `.../Application/DependencyInjection.cs` | Empty marker for assembly scan. |

### Contracts

| File | Notes |
|------|------|
| `.../Contracts/Events/GatewayPaymentCompletedIntegrationEvent.cs` | Rich money fields + vault ids; LineItems unused. |
| `.../Contracts/Events/GatewayPaymentFailedIntegrationEvent.cs` | **Orphan** — never published. |
| `.../Contracts/Events/GatewayDisputeCreatedIntegrationEvent.cs` | Used for credit clawback only. |
| `.../Contracts/Events/GatewayRefundRequestedIntegrationEvent.cs` | No amount field — design forces full refund or external lookup; **no publisher**. |
| `.../Contracts/Events/GatewayRefundCompletedIntegrationEvent.cs` | Amount fields exist but handler fills zeros. |
| `.../Contracts/Events/GatewayRefundFailedIntegrationEvent.cs` | No subscribers. |
| `.../Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs` | Includes `GatewayName` — correct version used by Commerce jobs. |
| `.../Contracts/Events/ApiCreditPurchasedIntegrationEvent.cs` | Dead legacy. |
| `.../Contracts/Queries/GenerateCheckoutSessionQuery.cs` | Default gateway BILLPLZ. |
| `.../Contracts/Queries/GenerateSystemCheckoutSessionQuery.cs` | Default BILLPLZ. |
| `.../Contracts/Queries/GenerateCustomerPortalQuery.cs` | Email + return URL only. |
| `.../Contracts/Queries/GetPaymentConfigQuery.cs` | Namespace `Application.Queries` inside Contracts folder. |
| `.../Contracts/Commands/UpdatePaymentConfigCommand.cs` | Namespace `Application.Commands`; `collection_id` JSON alias for MerchantId. |

### Gateways

| File | Notes |
|------|------|
| `.../Gateways/PaymentGatewayFactory.cs` | Simple DI enumeration; throws NotSupported. |
| `.../Gateways/StripeGatewayAdapter.cs` | Best-of-breed: fees, disputes, portal, off-session; PI path weak fees; off-session metadata incomplete. |
| `.../Gateways/BillplzGatewayAdapter.cs` | Solid HMAC + URL metadata; no vault/portal/refunds; fees estimated but callers pass 0; prod URL heuristic fragile. |
| `.../Gateways/ChipCollectGatewayAdapter.cs` | Full-ish MY stack; RSA verify; off-session + refunds; portal missing; customer id null; refund events not parsed. |
| `.../Gateways/RazorpayGatewayAdapter.cs` | Key split by `:`; registration links for vault; off-session hard-coded contact; portal missing. |

### Infrastructure wiring / HTTP

| File | Notes |
|------|------|
| `.../Infrastructure/DependencyInjection.cs` | Registers 4 adapters, outbox bus, inbox/outbox jobs, refund + off-session handlers. |
| `.../Infrastructure/Endpoints.cs` | Public webhooks; raw body + headers + Query-*; OK response. |
| `.../Infrastructure/PlatformEndpoints.cs` | **Misplaced superadmin auth** + platform payment-config; uses `ctx.TenantId` for config. |
| `.../Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` | Mask preservation; Stripe secret mapping; CHIP auto-setup; no encryption. |
| `.../Infrastructure/Queries/GetPaymentConfigQueryHandler.cs` | Masking; maps Secret_key = ApiKey mask. |
| `.../Infrastructure/Repositories/PaymentRepositories.cs` | Always IgnoreQueryFilters. |
| `.../Infrastructure/Configurations/PaymentConfigurations.cs` | Unique indexes good. |
| `.../Infrastructure/PaymentsDbContext.cs` | Schema `payments`; inbox/outbox indexes. |
| `.../Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` | No success/fail domain events; silent fail. |
| `.../Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` | **amount=0**, currency MYR hardcode — broken. |
| `.../Infrastructure/Workers/PaymentsInboxConsumerJob.cs` | Boilerplate. |
| `.../Infrastructure/Workers/PaymentsOutboxPublisherJob.cs` | Boilerplate. |
| `.../Infrastructure/Migrations/20260627124811_InitialPaymentsSchema.cs` | Initial tables incl. fee columns. |
| `.../Infrastructure/Migrations/20260705131411_RemoveAccountingOverrides.cs` | Drops fee/active columns — regresses Billplz fee fidelity. |
| `.../Infrastructure/Modules.Payments.Infrastructure.csproj` | Stripe.net + Razorpay packages; CHIP/Billplz raw HTTP. |
| `.../README.md` | Partially outdated (encryption, fee extraction, event completeness). |

### Cross-module consumers (related)

| File | Notes |
|------|------|
| `Modules/Commerce/.../InitiateCheckoutCommandHandler.cs` | Product gateway for products; custom links force BILLPLZ. |
| `Modules/Commerce/.../GatewayPaymentCompletedIntegrationEventHandler.cs` | Core fulfillment; vault; dunning recovery; **depends on metadata**. |
| `Modules/Commerce/.../GatewayRefundCompletedIntegrationEventHandler.cs` | Weak ExternalReference match. |
| `Modules/Commerce/.../BillingEngineJob.cs` | Dispatches off-session; no failure PAST_DUE. |
| `Modules/Commerce/.../DunningEngineJob.cs` | AUTOCHARGE ≤4; uses Payments ExecuteOffSession event correctly. |
| `Modules/Commerce/.../PaymentConfigEndpoints.cs` | Tenant BYOK API façade. |
| `Modules/Commerce/Contracts/Events/ExecuteOffSessionChargeIntegrationEvent.cs` | **Duplicate dead contract** (no GatewayName). |
| `Modules/Billing/.../GatewayPaymentCompletedHandler.cs` | Ledger + B2C docs. |
| `Modules/Billing/.../PlatformTopUpEventHandler.cs` | Package threshold credits. |
| `Modules/Billing/.../ChargebackClawbackHandler.cs` | Utility credits only. |
| `Modules/Billing/.../GatewayRefundCompletedHandler.cs` | Zero-amount safe but useless until refunds fixed. |
| `Modules/Billing/.../ApiCreditPurchasedHandler.cs` | Unsubscribed dead path. |
| `Modules/Billing/.../Endpoints.cs` | System checkout for top-up (BILLPLZ default). |
| `Modules/Lhdn/.../GatewayRefundCompletedIntegrationEventHandler.cs` | Cancel &lt;72h / CN &gt;72h; amounts from event (zeros today). |
| `apps/ops-page/.../PaymentSettingsPage.tsx` | Multi-gateway BYOK UX; client-side Billplz validation. |

### Docs / ADRs (context)

| File | Notes |
|------|------|
| `docs/architecture-decision-log/004-payment-integration-and-event-driven-guiideline.md` | Metadata, outbox typing, EF child entity pitfalls. |
| `docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md` | Billplz query-string state. |
| `apps/lazuar-api/docs/006-payment-webhook-idempotency-backfilling.md` | Cutover playbook; metadata schema slightly stale (`community_*`). |

---

### Executive summary

The Payments module is a **credible multi-gateway BYOK cashier** for **hosted checkout + signed webhooks + idempotent completion events**, with strong Stripe/CHIP coverage and a deliberate Billplz metadata design. It is **not production-complete for recurring revenue**: off-session charges lack Commerce correlation metadata, failures do not drive dunning, refunds are effectively unimplemented, secrets are plaintext despite docs, Billplz fees were removed, and several contract events/handlers are dead. Prioritize **off-session metadata + billing failure transitions + real refunds + encryption at rest** before expanding gateway surface area.
