<!-- Source subagent: 019fc650-3512-7283-86ea-5651dd7bb480 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Commerce Module Gap Analysis

**Scope:** `apps/lazuar-api/Modules/Commerce/` and `packages/api-spec/modules/commerce/`  
**Excluded from deep dive:** dunning campaign builder internals (noted where lifecycle depends on them)  
**Focus:** products, subscriptions, checkout, subscribers, campaigns surface, integrations, TypeSpec alignment

---

## Module Inventory

### Layout (Clean Architecture)

| Layer | Path | Role |
|--------|------|------|
| **Domain** | `Domain/Aggregates`, `Entities`, `ValueObjects`, `Events` | Aggregates + local domain events |
| **Application** | `Application/Commands`, `EventHandlers`, `Queries` | Command handlers, integration handlers, ports |
| **Contracts** | `Contracts/Commands`, `Events`, `ISubscriberQueryService` | Cross-module surface |
| **Infrastructure** | `Endpoints`, `Services`, `Repositories`, `Workers`, `EventHandlers`, `Migrations` | HTTP, EF/Dapper, jobs, inbox/outbox |

### Aggregates (6)

| Aggregate | Table | Purpose |
|-----------|--------|---------|
| `Product` | `commerce.Products` | Sellable checkout link (recurring or one-time) |
| `Subscription` | `commerce.Subscriptions` | Recurring entitlement + billing/dunning state |
| `CheckoutSession` | `commerce.CheckoutSessions` | Product checkout or ad-hoc custom payment link |
| `Order` | `commerce.Orders` | One-time purchase completion record |
| `Coupon` | `commerce.Coupons` | Promo codes with reserve/confirm/release |
| `DunningCampaign` | `commerce.DunningCampaigns` | Recovery sequences (shallow note only) |

### Supporting entities / value objects

- **Entities:** `ChargeAttemptLog`, `CommerceTransactionLog`, `DunningStep`, `ReminderDispatchLog`
- **VOs:** `CheckoutConfiguration`, `AdHocLineItem`
- **Domain events (unhandled):** `CouponReservedDomainEvent`, `CouponConfirmedDomainEvent`, `CouponReleasedDomainEvent` — raised on aggregate, **no domain event handlers**

### Commands (application handlers)

| Command | Handler | Outcome |
|---------|---------|---------|
| `CreateProductCommand` | Create | Creates product; auto-archives if no email config |
| `UpdateProductCommand` | Update | Updates product; blocks activate without email |
| `ArchiveProductCommand` / `RestoreProductCommand` | Soft archive / restore | No email gate on restore |
| Coupon create/update/delete | `CouponCommandHandlers` | Delete = archive |
| `InitiateCheckoutCommand` | Public checkout | Session + gateway or zero-amount path |
| `ProcessZeroAmountCheckoutCommand` | Internal | Completes free checkout |
| `CreateCustomCheckoutCommand` | Admin payment link | Ad-hoc line items session |
| `MarkCheckoutAsPaidOfflineCommand` | Admin | Completes session + ledger event only |
| `CreateManualSubscriberCommand` | Admin enroll | Reminder-only subscription |
| Dunning campaign CRUD + defaults | `DunningCampaignCommandHandlers` | Campaign surface |
| Pause/resume subscriber dunning | Manage handlers | Dunning pause flags |

### Integration events (published by Commerce)

| Event | Typical publishers | Consumers |
|-------|-------------------|-----------|
| `SubscriptionActivatedIntegrationEvent` | Payment complete, zero-amount, manual enroll | Commerce → HTTP fulfillment webhooks only |
| `SubscriptionSuspendedIntegrationEvent` | (domain exists) | Communications lifecycle + Commerce webhooks — **rarely published** |
| `SubscriptionCanceledIntegrationEvent` | (domain exists) | Same — **rarely published** |
| `SubscriptionResumedIntegrationEvent` | Arrears recovery from SUSPENDED | Commerce webhooks |
| `OrderCompletedIntegrationEvent` | One-time complete / zero-amount | Commerce → HTTP webhooks |
| `ManualSubscriberEnrolledIntegrationEvent` | Manual enroll, mark-paid offline | Billing ledger + receipt |
| `ZeroAmountCheckoutCompletedIntegrationEvent` | Zero-amount path | Billing discount ledger |
| `FulfillmentRequestedIntegrationEvent` | Billing engine, dunning | Communications (`internal:…` / hard-coded target) |
| `OutboundWebhookRequestedIntegrationEvent` | Lifecycle handlers, engines | One module dispatcher |
| `ExecuteOffSessionChargeIntegrationEvent` | **Payments.Contracts** version used by jobs | Payments off-session charge |
| Dead duplicate | `Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` | **Unused / stale** (no `GatewayName`) |

### Workers

| Worker | Cadence | Role |
|--------|---------|------|
| `BillingEngineJob` | Hourly | Due renewals → auto-debit or mark `PAST_DUE` |
| `DunningEngineJob` | Hourly | Pre-reminders + past-due steps + final cancel/suspend |
| `CommerceOutboxPublisherJob` | Outbox | Publish integration events |
| `CommerceInboxConsumerJob` | Inbox | Consume inbound integration events |

### Query / cross-module ports

- `ICommerceQueryService` — admin/public reads (products, subs, txs, coupons, stats, portal, custom checkouts, checkout status)
- `ISubscriberQueryService` — Communications broadcast fan-out (active/past-due recipients)
- Payments: `GenerateCheckoutSessionQuery`, `GenerateCustomerPortalQuery`, payment-config commands
- CRM: `ResolveClientProfileCommand`, `ICrmQueryService`
- One: tenant slug resolution
- Communications: `HasValidEmailConfigAsync` gate for checkout/products

### TypeSpec package

- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/commerce/models.tsp`
- `admin-routes.tsp` → `/admin/commerce/*` (`OrgAdmin` / Bearer)
- `public-routes.tsp` → `/public/commerce/*`

### Frontend consumers (outside module, demand signal)

- **ops-page:** dashboard, products, subscribers, transactions, coupons, dunning, custom checkouts (“quotes”)
- **portal-page:** public product checkout success, portal list, cancel plan (calls missing APIs)

---

## Domain Aggregates

### Product

**Fields:** Name, Slug (unique per org), Price, PricingModel (default `FIXED`), MinimumPrice, Currency, Interval (`one_time` / `mo`/`yr`-style), IsActive, GatewayName, CheckoutConfiguration, FulfillmentTargets (jsonb URLs or `internal:…`).

**Behaviors:** Create (active unless email missing → archived), update details, archive/restore.

**Gaps**

| Gap | Severity | Notes |
|-----|----------|--------|
| `PricingModel` / `MinimumPrice` never applied at checkout | **High** | Stored + exposed in DTO; checkout always uses fixed `product.Price` |
| Currency not updatable | Medium | `UpdateDetails` omits currency |
| CheckoutConfiguration not enforced server-side | **High** | Requires address/tax/phone never validated in `InitiateCheckout` |
| Interval model ad hoc | Medium | Only `one_time` vs “not one_time”; billing uses `yr` else month — no week/day/custom |
| Slug uniqueness only at DB | Low | No friendly conflict message path |
| No product → active subscription safety on archive | Medium | Archiving product does not block or migrate subs |
| Fulfillment targets format free-form | Medium | Lifecycle handlers only emit HTTP webhooks; `internal:` handled inconsistently (engines only) |

### Subscription

**Statuses:** `PENDING` → `ACTIVE` → `PAST_DUE` → `SUSPENDED` / `CANCELED`  
**Key fields:** ClientProfileId, ProductId, period ends, vaulted customer/token, IsReminderOnly, dunning campaign/step/pause, ReminderLogs.

**Behaviors:** Activate (special-case past-due/suspended period freeze), vault token, past-due, suspend, resume, cancel, dunning assign/advance/pause/clear, reminder log.

**Gaps**

| Gap | Severity | Notes |
|-----|----------|--------|
| No quantity / seats | Medium | Checkout multiplies price by quantity but sub has no seat count |
| No coupon / plan history on sub | Medium | Renewals always charge full `product.Price` |
| No cancel-at-period-end | **High** | Only hard `Cancel()`; portal cancel API missing |
| Status stringly typed | Medium | No enum; UI uses `CANCELLED`/`BANNED` vs domain `CANCELED` |
| IsReminderOnly never converts to vaulted without new checkout | Medium | Manual members stay reminder-only until card capture flow |
| PENDING filtered out of lists but can linger | Low | Constructor starts PENDING; only some paths activate |
| Resume does not emit consistently from all recovery paths | Medium | PAST_DUE recovery publishes Activated not Resumed |

### CheckoutSession

**Two constructors:** product session (+ optional coupon) or ad-hoc custom (line items + B2B flag).  
**Statuses:** `OPEN` → `COMPLETED` / `EXPIRED`.

**Gaps**

| Gap | Severity | Notes |
|-----|----------|--------|
| No expiry worker | **High** | `Expire()` exists; nothing calls it; coupons stay reserved |
| No amount/currency snapshot | **High** | Price changes after session open affect gateway only at initiate time; offline mark uses *current* product price |
| Quantity not stored | Medium | Multi-qty lost after session create |
| Metadata conflates session ↔ subscription id | **High** | Gateway metadata key `subscription_id` = **session** id until post-payment creates a new Subscription id |
| Custom complete path creates no Order | **High** | Gateway custom link only logs transaction + completes session |
| Offline mark creates no Order/Subscription | **High** | Only `ManualSubscriberEnrolledIntegrationEvent` (ledger); product entitlement not created |

### Order

Minimal: client, product, amount, currency, PENDING→COMPLETED/REFUNDED.

**Gaps:** No line items, tax, coupon, session linkage, gateway ref; `Refund()` never called from Commerce refund handler (only transaction log status flips). No admin order list.

### Coupon

Strong domain: percentage/fixed, max uses, reserved/used, min price, product scope, reserve/confirm/release.

**Gaps**

| Gap | Severity | Notes |
|-----|----------|--------|
| Confirm only on zero-amount path | **Critical** | Paid `GatewayPaymentCompleted` never `ConfirmReservation()` — reserves leak, max uses incorrect |
| No release on cancel/expiry | **Critical** | Abandoned checkouts leave `ReservedCount` forever |
| Domain events have no handlers | Low | Audit/telemetry unused |
| No per-customer / once-per-email limit | Medium | Only global max uses |
| Quantity multiplies discount incorrectly? | Medium | Discount = `CalculateDiscount(unit) * quantity` — OK for %; fixed-amount coupons also multiply (may be unintended) |

### DunningCampaign (surface only)

Exists with targeting, steps, metrics, final action. Default campaigns seeded from Communications `DefaultTemplatesSeeded`. Deep dunning design out of scope; lifecycle coupling called out under Workers/Events.

---

## Public vs Admin Endpoints

### Admin (`/admin/commerce`, `RequireAuthorization("OrgAdmin")`)

| Method | Route | Implementation | TypeSpec |
|--------|-------|----------------|----------|
| GET/POST/PUT/DELETE | `/products`, `/products/{id}`, restore | Yes | Yes |
| GET/POST/PUT/DELETE | `/dunning-campaigns` (+ defaults) | Yes | Yes |
| GET/PUT | `/payment-config` | Proxy to Payments | Yes |
| GET/POST | `/subscribers` | List + manual create | Yes |
| POST | `/subscribers/{id}/dunning/pause\|resume` | Yes | Yes |
| POST | `/subscribers/portal-link` | Yes (gateway customer portal) | **No** |
| GET | `/transactions` | Yes | Yes |
| GET/POST/PUT/DELETE | `/coupons` | Yes | Yes |
| GET | `/stats` | Yes (partial data) | Yes |
| POST/GET | `/custom-checkouts` | Yes | Yes |
| POST | `/checkouts/{id}/mark-paid` | Yes | Yes |
| POST | `/subscribers/export` | **Missing** | **Missing** |
| POST | `/subscribers/{id}/cancel` | **Missing** | **Missing** |
| POST | `/subscribers/{id}/ban` | **Missing** | **Missing** |
| POST | `/subscribers/{id}/record-payment` | **Missing** | **Missing** |
| POST | `/subscribers/{id}/refund` | **Missing** | **Missing** |
| GET | `/subscribers/{id}` detail | **Missing** | **Missing** |

Ops UI (`SubscribersPage.tsx`) calls export + `cancel` / `ban` / `record-payment` / `dunning/*` as if they exist — only dunning routes work.

### Public (`/public/commerce`)

| Method | Route | Implementation | TypeSpec | Clients |
|--------|-------|----------------|----------|---------|
| GET | `/{tenantSlug}/products/{slug}` | Yes (raw Dapper id lookup) | Yes | Checkout |
| GET | `/{tenantSlug}/validate-coupon` | Yes | Yes | Checkout |
| POST | `/checkout` | Yes | Yes | Checkout |
| GET | `/checkout/{subId}/status` | Yes | Yes | Success polling |
| GET | `/{tenantSlug}/custom-checkouts/{sessionId}` | Yes | Yes | Pay links |
| GET | `/checkout/{subId}/arrears` | Yes (inline SQL) | Yes | Update payment |
| POST | `/checkout/{subId}/update-payment` | Yes (inline SQL + Payments query) | Yes | Arrears |
| GET | `/{tenantSlug}/portal?token=` | Yes | Yes | Portal |
| POST | `/{tenantSlug}/portal/magic-link` | **Missing** | Yes | Spec only |
| POST | `/{tenantSlug}/portal/cancel` | **Missing** | Yes | **portal-page Cancel Plan** |
| GET | `/{tenantSlug}/portal/billing-link` | **Missing** | Yes | Spec only |

**Auth posture:** Public routes are unauthenticated; portal uses HMAC magic token (24h, subscription-scoped). No rate limiting visible on checkout/coupon validation.

---

## Subscription Lifecycle

```text
[Checkout / Manual enroll]
        │
        ▼
   PENDING ──(activate)──► ACTIVE
                             │
              BillingEngine (due, no vault) ──► PAST_DUE
              BillingEngine (vault) ──► ExecuteOffSessionCharge
                             │
              charge success (session path / sub id path) ──► ACTIVE (+ clear dunning)
              Dunning grace + FINAL CANCEL ──► CANCELED
              Dunning grace + FINAL SUSPEND ──► SUSPENDED
              SUSPENDED + payment ──► ACTIVE (Resume) + SubscriptionResumed
```

### Implemented paths

1. **Online first payment** — `GatewayPaymentCompleted` with `type=commerce_subscription` + session id → complete session → create Subscription/Order → activate + vault → `SubscriptionActivated` → HTTP webhooks; Billing ledger on same gateway event.
2. **Zero amount** — coupon 100% → `ProcessZeroAmountCheckout` → Order/Sub + `ZeroAmountCheckoutCompleted` (Billing) + lifecycle events; **coupon confirmed**.
3. **Manual enroll** — `IsReminderOnly=true` ACTIVE; optional ledger via `ManualSubscriberEnrolled`; optional activated webhooks if welcome flag.
4. **Renewal auto-debit** — hourly job; charge attempt log uniqueness per billing date; failures not clearly turning status past-due in Commerce (depends on Payments failure events — **no Commerce handler for charge failure**).
5. **No-method due** — mark `PAST_DUE` + fulfillment/outbound with event type **`subscription.suspended`** (status actually PAST_DUE — bug).
6. **Dunning final actions** — cancel/suspend + metrics; **does not publish typed `SubscriptionCanceled` / `SubscriptionSuspended`** → Communications `LifecycleEventHandlers` and Commerce lifecycle webhook fan-out for those typed events **do not run**.

### Missing lifecycle operations

| Operation | Gap |
|-----------|-----|
| Customer cancel (portal) | Spec + UI, no endpoint |
| Admin cancel / ban | UI only |
| Admin record offline payment on sub | UI only — would need advance period + ledger + transaction log |
| Proration / plan change / pause subscription | Not modeled |
| Failed off-session charge → PAST_DUE | No explicit Commerce handler |
| Renewal period advance on successful auto-debit | Relies on `GatewayPaymentCompleted` looking up **Subscription by id** when session already COMPLETED — works if metadata uses real sub id; BillingEngine publishes charge with **subscription id** ✓ |
| Welcome email on activate | No Communications subscription to `SubscriptionActivated` — only HTTP fulfillment targets |

### Status vocabulary mismatch

Domain: `CANCELED`. Ops UI optimistic updates: `CANCELLED`, `BANNED`. Stats/filters may diverge.

---

## Checkout Session Flow

### Product checkout (happy path)

1. Optional `validate-coupon`.
2. `POST /checkout` → resolve tenant slug → require Communications email config.
3. Resolve/create CRM profile (address optional, not validated against product config).
4. Optional coupon: row lock `FOR UPDATE`, validate, **Reserve**, attach coupon id.
5. Create OPEN session (24h expiry, no worker).
6. If net = 0 → process zero amount inline.
7. Else `GenerateCheckoutSessionQuery` to Payments with metadata `{ type: commerce_subscription, subscription_id: <sessionId>, tenant_id }`.
8. Gateway success → Commerce `GatewayPaymentCompleted` completes session, creates Sub/Order, logs `CommerceTransactionLog`.
9. Client polls `/checkout/{sessionId}/status` → on COMPLETED, finds **some** ACTIVE sub for same client+product → magic token.

### Custom checkout

1. Admin creates session with line items + client profile.
2. Public GET session details.
3. InitiateCheckout with `session_id` only (product_slug still required by DTO/command shape — empty product path if session set).
4. Hard-coded currency **MYR**, gateway **BILLPLZ**, product name “Custom Payment Request”.
5. On payment: complete session + transaction log; **no Order aggregate**, no fulfillment events.

### Offline mark-paid

Completes OPEN session; publishes `ManualSubscriberEnrolled` with `ProductId ?? Empty` and amount; **no entitlement (Sub/Order)**, no `CommerceTransactionLog` in Commerce handler.

### Checkout gaps (priority)

1. **Coupon reserve leak / non-confirm on paid path** (Critical)  
2. **Session expiry not enforced** (Critical)  
3. **Portal cancel / magic-link / billing-link missing** (Critical for customer surface)  
4. **Custom + offline fulfillment incomplete** (High)  
5. **Checkout field requirements not enforced** (High)  
6. **PWYW / PricingModel unused** (High product promise gap)  
7. **Quantity not persisted**; guest flag unused (Medium)  
8. **Status token lookup race / wrong sub** if multi-product or re-purchase (Medium)  
9. **Public product lookup** mixes raw SQL + query service (Low consistency)  
10. **Update-payment / arrears** endpoints bypass query service and join CRM/One schemas (boundary smell)  
11. **Is_b2b_required** on custom session never passed into payment metadata as `is_b2b_required` for Billing ledger B2B flag  

---

## Subscriber Management

### What works

- Paginated list (loads all rows then filters search in memory — scale risk).
- CRM enrichment for name/email/phone.
- Manual create with payment method / amount / welcome / billing dates.
- Dunning pause/resume.
- Cross-module recipient enumeration for Communications.
- Side-panel “payments” = global transactions filtered by customer email (not true sub payment history).

### What ops UI expects but API lacks

| Feature | Expected route | Status |
|---------|----------------|--------|
| CSV export | `GET/POST …/subscribers/export` | Missing |
| Cancel | `…/subscribers/{id}/cancel` | Missing |
| Ban | `…/subscribers/{id}/ban` | Missing (no BANNED status) |
| Record payment | `…/subscribers/{id}/record-payment` | Missing |
| Refund | `…/subscribers/{id}/refund` | Missing (tx panel even hits wrong module path `community`) |
| Status filter server-side | query param | Client-only filter |
| Portal link admin | `…/subscribers/portal-link` | Implemented, **not in TypeSpec** |

### Portal subscriber self-service

- Portal data aggregation works with valid token.
- Token generated only via checkout status success path (or would be via missing magic-link).
- Cancel Plan UI is **dead** without backend.

### Data quality gaps

- Search not SQL-level; no status/product filters.
- No single-subscriber GET for deep links.
- Days overdue includes `CANCELED` but not `SUSPENDED`.
- Vault IDs exposed on admin DTO (security/ops tradeoff).

---

## Event Integration with Payments / Billing / Communications

### Payments

| Direction | Contract | Notes |
|-----------|----------|-------|
| Out | `GenerateCheckoutSessionQuery` | Checkout + update-payment |
| Out | `GenerateCustomerPortalQuery` | Admin portal-link only |
| Out | `ExecuteOffSessionChargeIntegrationEvent` (Payments.Contracts) | Billing + dunning engines |
| In | `GatewayPaymentCompletedIntegrationEvent` | Core fulfillment |
| In | `GatewayRefundCompletedIntegrationEvent` | Transaction log only |
| Admin | Payment config GET/PUT | Thin Commerce façade over Payments |

**Gaps:** No Commerce handler for charge failure / dispute; refund does not call `Order.Refund()` or reverse subscription; duplicate unused ExecuteOffSession event in Commerce.Contracts; product gateway name vs custom BILLPLZ hard-code.

### Billing

| Event | Handler | Behavior |
|-------|---------|----------|
| `GatewayPaymentCompleted` | `GatewayPaymentCompletedHandler` | Full ledger + B2C receipt for **all** gateway payments (incl. commerce) |
| `ManualSubscriberEnrolled` | Manual handler | Cash/revenue + receipt |
| `ZeroAmountCheckoutCompleted` | ZeroAmount handler | Discount/revenue lines when original > 0 |

**Gaps:** Commerce transaction log and Billing ledger are parallel, not reconciled; offline mark-paid hits Billing but not Commerce tx log; zero-amount with original 0 may produce empty ledger edge cases; B2B path incomplete for commerce metadata.

### Communications

| Path | Behavior |
|------|----------|
| Email config gate | Create product / activate / initiate checkout |
| `FulfillmentRequested` → COMMUNICATIONS | Dunning/reminder payloads → templates + DispatchMessage |
| `SubscriptionSuspended` / `Canceled` typed events | Payment Failed / Subscription Cancelled templates |
| `DefaultTemplatesSeeded` | Auto-generate default dunning campaigns |
| `ISubscriberQueryService` | Broadcast audiences |

**Gaps:**

- Typed cancel/suspend events mostly **not published** from dunning final action → emails/webhooks incomplete.
- `SubscriptionActivated` **not** consumed for welcome email (manual “welcome” only fires webhooks).
- Hard-coded portal URLs `https://portal.lazuar.com/...` in Communications handlers; magic-link variables often plain portal URL without token.
- Lifecycle handler `renewal_link` placeholder is a dead generic URL.

### One / webhooks

- HTTP fulfillment targets → `OutboundWebhookRequested` → One dispatcher.
- `internal:` targets → `FulfillmentRequested` only in engines and dunning final action — **not** in `SubscriptionLifecycleIntegrationEventHandlers` (HTTP only).

### CRM

- Checkout and enroll resolve profiles; portal/list join CRM for identity.
- Checkout can pass billing address; not required by product flags.

---

## Workers

### BillingEngineJob (hourly)

- Selects all subs with `NextBillingDate <= now` (no org shard).
- Skips PAST_DUE / SUSPENDED / CANCELED.
- With vault: one charge attempt per billing date → Payments off-session.
- Without vault: PAST_DUE + webhook/fulfillment with **wrong event name** (`subscription.suspended`).
- Does not advance billing date optimistically (waits for payment event) — good for double-charge; bad if charge succeeds without Commerce metadata.

### DunningEngineJob (hourly)

- Pre-dunning ACTIVE with due in 14 days (negative day offsets).
- PAST_DUE processing, campaign assign, AUTO_CHARGE steps, communication steps.
- Final CANCEL/SUSPEND without typed integration events (see above).

### Outbox / Inbox

Standard PlatformDbContext pattern; required for reliable multi-module messaging.

### Missing workers

| Worker | Need |
|--------|------|
| CheckoutSession expiry + coupon release | Critical |
| Charge failure reconciliation | High |
| Stats / MRR materialization (optional) | Low |

---

## TypeSpec Contract Alignment

### Aligned (present both sides)

Products CRUD+restore, dunning CRUD+defaults, payment-config, subscribers list/create, dunning pause/resume, transactions, coupons, stats, custom-checkouts, mark-paid, public product/coupon/checkout/status/custom-checkout/arrears/update-payment/portal GET.

### TypeSpec without implementation

| Spec operation | Impact |
|----------------|--------|
| `requestMagicLink` | Portal email entry broken if depended on |
| `cancelPortalSubscription` | Cancel Plan broken in portal-page |
| `getBillingLink` | No customer self-serve card update via portal |

### Implementation without TypeSpec

| Route | Impact |
|-------|--------|
| `POST /admin/commerce/subscribers/portal-link` | Undocumented for clients/codegen |

### Model / DTO drift

| Item | Issue |
|------|--------|
| `PaymentRecordDto` | Defined in models.tsp, **no route** uses it |
| `CreateManualSubscriberDto` | Spec returns `StatusResponse`; fine |
| Transaction `payment_method` query | Spec + endpoint accept; **SQL ignores** filter |
| Subscriber actions cancel/ban/record-payment | Used by ops UI, nowhere in TypeSpec |
| Export | UI only |
| Product pricing enums | Free `string` for pricing_model, interval, gateway — no closed set in TypeSpec |
| Arrears uses product list price not arrears amount | Spec shape is product_name/amount/currency/status — amount is catalog price |

### Generated clients

`packages/api-types-ts` and `api-types-dotnet` include portal cancel/magic-link/billing-link — **false confidence** for frontends.

---

## Gaps & Recommendations

### P0 — Correctness / data integrity

1. **Confirm coupon on paid completion; release on expire/abandon**  
   In `GatewayPaymentCompletedIntegrationEventHandler`, after session complete, if `session.CouponId` → confirm. Add expiry job: OPEN sessions past `ExpiresAt` → `Expire()` + `ReleaseReservation()`.

2. **Publish typed lifecycle events from dunning final actions**  
   On CANCEL/SUSPEND, also `Publish(SubscriptionCanceled|SuspendedIntegrationEvent)` so Communications + HTTP lifecycle handlers run.

3. **Implement portal cancel (and preferably magic-link)**  
   Match TypeSpec; call `subscription.Cancel()` + publish canceled event + fulfillment.

4. **Fix BillingEngine PAST_DUE event type**  
   Emit `subscription.past_due` (or activate dunning without mislabeled suspend).

5. **Custom checkout + offline mark-paid entitlement**  
   Define productized outcome: create Order (and optional ad-hoc product), transaction log, fulfillment, consistent ledger metadata.

### P1 — Product completeness vs UI/spec

6. Admin subscriber actions: **cancel**, **record-payment** (activate/clear dunning + ledger + tx log), **export CSV**; drop or implement **ban**.  
7. Enforce **CheckoutConfiguration** on initiate.  
8. Implement or remove **PricingModel/MinimumPrice** from UI/API until PWYW works.  
9. Pass **B2B flag** into payment metadata for custom checkouts.  
10. Handle **off-session charge failure** → PAST_DUE + dunning assign.  
11. Align TypeSpec with `portal-link` and remove or implement unused DTOs/routes.  
12. Fix refund path: Commerce Order + transaction log + Billing (ops currently wrong module).

### P2 — Hardening & design

13. Snapshot amount/currency/quantity/coupon on CheckoutSession.  
14. Rename metadata `subscription_id` vs `checkout_session_id` carefully (migration).  
15. Checkout status: link Subscription/Order id on session at completion.  
16. SQL-side subscriber search/filter/pagination.  
17. Fill stats (`total_revenue_collected`, cash flow, payment methods) from `TransactionLogs`.  
18. Remove dead `Commerce.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent`.  
19. Domain events for coupons → audit or delete.  
20. Welcome email path via Communications on first activation.  
21. Magic links with real tokens in dunning templates (not bare portal URL).  
22. Tests: module tests for checkout coupon lifecycle, gateway completion, portal cancel — currently only light `CommerceQueryServiceTests`.

### Architecture smells (non-blocking but real)

- Public endpoints embed multi-schema SQL (commerce/crm/one).  
- Payment config lives under Commerce routes but Payments module owns state.  
- Fulfillment split between typed events, FulfillmentRequested, and ad-hoc payload JSON.  
- No README in Commerce module (unlike Billing/CRM/Payments).

---

## File-by-File Notes

### TypeSpec

| File | Notes |
|------|--------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/modules/commerce/models.tsp` | Full DTO surface; unused `PaymentRecordDto`; free-form strings; CreateManualSubscriber well documented |
| `admin-routes.tsp` | Solid admin map; missing portal-link, export, sub actions |
| `public-routes.tsp` | Three portal routes without backends |

### Domain

| File | Notes |
|------|--------|
| `Domain/Aggregates/Product.cs` | Clean; currency immutable on update; pricing model storage-only |
| `Domain/Aggregates/Subscription.cs` | Full status machine; Activate freezes period when past-due/suspended (intentional for arrears) |
| `Domain/Aggregates/CheckoutSession.cs` | Dual mode product/custom; Expire unused |
| `Domain/Aggregates/Order.cs` | Minimal one-time record |
| `Domain/Aggregates/Coupon.cs` | Strongest domain logic in module |
| `Domain/Aggregates/DunningCampaign.cs` | Metrics + targeting |
| `Domain/ValueObjects/CheckoutConfiguration.cs` | Flags only |
| `Domain/ValueObjects/AdHocLineItem.cs` | description/qty/price |
| `Domain/Entities/*` | ChargeAttempt unique per day; TransactionLog REFUNDED transition; ReminderDispatch unique |
| `Domain/Events/Coupon*.cs` | No handlers |

### Contracts

| File | Notes |
|------|--------|
| `Commands/*` | Mirror features; InitiateCheckout carries guest + quantity unused/partially used |
| `Events/*` | Solid set; unused/stale ExecuteOffSession in Commerce.Contracts |
| `ISubscriberQueryService.cs` | Good boundary for Communications |

### Application commands

| File | Notes |
|------|--------|
| `InitiateCheckoutCommandHandler.cs` | Core orchestrator; email gate; coupon reserve; zero path; custom hard-codes MYR/BILLPLZ |
| `ProcessZeroAmountCheckoutCommand.cs` | Only path that confirms coupons |
| `CreateCustomCheckoutCommandHandler.cs` | CRM resolve + session only |
| `MarkCheckoutAsPaidOfflineCommandHandler.cs` | Completes + ledger event; no Sub/Order/tx log |
| `CreateManualSubscriberCommandHandler.cs` | Reminder-only activate; conditional ledger/welcome |
| `CreateProductCommandHandler.cs` | Archives without email |
| `UpdateProductCommandHandler.cs` | Email required to activate |
| `Archive/RestoreProductCommandHandler.cs` | Soft flags; restore skips email gate |
| `CouponCommandHandlers.cs` | Delete = archive |
| `ManageSubscriberDunningCommandHandlers.cs` | Pause/resume only |
| `DunningCampaignCommandHandlers.cs` | CRUD + step replace |
| `OrderCompletedIntegrationEventHandler.cs` | HTTP webhooks only |
| `SubscriptionLifecycleIntegrationEventHandlers.cs` | HTTP webhooks only; no internal: apps |

### Infrastructure endpoints

| File | Notes |
|------|--------|
| `Endpoints.cs` | Wires groups; custom-checkouts + mark-paid inline |
| `Endpoints/ProductEndpoints.cs` | Matches TypeSpec |
| `Endpoints/PublicEndpoints.cs` | Core public API; **missing portal cancel/magic-link/billing-link**; arrears/update-payment fat handlers |
| `Endpoints/SubscriberEndpoints.cs` | List/create/dunning/portal-link; **no export/actions** |
| `Endpoints/CouponEndpoints.cs` | Full CRUD archive |
| `Endpoints/TransactionEndpoints.cs` | Filter payment_method unused downstream |
| `Endpoints/StatsEndpoints.cs` | Thin |
| `Endpoints/PaymentConfigEndpoints.cs` | Payments façade |
| `Endpoints/DunningCampaignEndpoints.cs` | Full campaign API |

### Infrastructure services / repo

| File | Notes |
|------|--------|
| `CommerceQueryService.cs` | DI partial host |
| `…Products.cs` | Maps owned checkout config + jsonb targets |
| `…Subscribers.cs` | In-memory search/pagination after full load |
| `…Checkout.cs` | Status + token via sub lookup by profile+product |
| `…Portal.cs` | Aggregates subs/orders for client |
| `…CustomCheckouts.cs` | ProductId IS NULL sessions; line item deserialize |
| `…Transactions.cs` | TransactionLogs; payment_method hardcoded GATEWAY |
| `…Coupons.cs` | Full list |
| `…Stats.cs` | MRR/churn partial; revenue/trend/methods **stub zero** |
| `…Dunning.cs` | Campaign read model with steps jsonb_agg |
| `SubscriberQueryService.cs` | Broadcast audience |
| `CommerceRepository.cs` | EF + coupon FOR UPDATE; cross-schema template id helper for dunning defaults |
| `CommerceDbContext.cs` | Schema commerce; jsonb converters; append-only hacks for steps/logs |

### Event handlers / workers

| File | Notes |
|------|--------|
| `GatewayPaymentCompletedIntegrationEventHandler.cs` | Heart of fulfillment; coupon gap; custom incomplete; arrears recovery |
| `GatewayRefundCompletedIntegrationEventHandler.cs` | Tx log only; ExternalReference match vs PaymentRecordId — verify Payments sets same id |
| `DefaultTemplatesSeededIntegrationEventHandler.cs` | Seeds dunning if empty |
| `BillingEngineJob.cs` | Renewal + PAST_DUE; event mislabel |
| `DunningEngineJob.cs` | Pre/post dunning; final actions without typed events |
| `CommerceOutbox/Inbox*Job.cs` | Standard |

### DI

| File | Notes |
|------|--------|
| `Infrastructure/DependencyInjection.cs` | DbContext, repos, queries, 4 hosted jobs, event subscriptions listed |
| `Application/DependencyInjection.cs` | MediatR scan marker only |

### Migrations

| Pattern | Notes |
|---------|--------|
| Initial schema → coupons products → pricing model → transaction logs → reminder-only → dunning → ad-hoc line items → dunning refactor → gateway on product | Schema evolution matches features; no expiry job table needed |

### Tests

| File | Notes |
|------|--------|
| `tests/…/CommerceQueryServiceTests.cs` | Limited query coverage |
| No unit tests for checkout/payment/coupon lifecycle | Major quality gap |
| Billing tests cover ManualSubscriber + Gateway ledger | Adjacent only |

### Frontend demand (context)

| File | Notes |
|------|--------|
| `apps/ops-page/.../SubscribersPage.tsx` | Export + cancel/ban/record-payment/refund expected |
| `apps/portal-page/.../portal/page.tsx` | Cancel Plan → missing API |
| `apps/ops-page` commerce pages | Products, coupons, dashboard, txs aligned with existing APIs |

---

## Summary Scorecard

| Area | Maturity | Headline |
|------|----------|----------|
| Product catalog | Medium | CRUD solid; PWYW/config flags incomplete |
| Checkout (product) | Medium–High | Works end-to-end; coupon lifecycle broken for paid |
| Checkout (custom/offline) | Low | Payment can succeed without full entitlement model |
| Subscriptions | Medium | Billing + dunning engines exist; self-serve + admin actions thin |
| Subscribers admin | Low–Medium | List/create/dunning only vs rich UI |
| Coupons | Medium | Domain strong; reservation integrity weak |
| Stats | Low | Partial KPIs |
| Portal public | Low–Medium | Read works; cancel/magic-link/billing-link missing |
| TypeSpec alignment | Medium | Core routes OK; critical public + admin drift |
| Cross-module events | Medium | Payments/Billing strong; Communications lifecycle incomplete |
| Tests | Low | Thin |

**Bottom line:** Commerce is a real multi-tenant checkout + subscription core with gateway, ledger, and dunning hooks, but **coupon reservation integrity**, **portal self-serve**, **admin subscriber actions**, and **custom/offline fulfillment** are the largest functional gaps versus TypeSpec and the ops/portal UIs. Addressing P0 items first prevents silent inventory corruption (coupons) and broken customer cancel, then close the admin/UI contract gap.
