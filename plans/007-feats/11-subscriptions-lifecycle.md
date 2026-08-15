# 11 — Subscriptions lifecycle

**Program:** `plans/007-feats` — competitor features vs Lazuar Pay (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`).  
**Workspace ground truth:** Commerce module as of **16 August 2026**.  
**Comparators:** Stripe Billing, Chargebee Billing 2.0, Paddle Billing.  
**Stance:** Full uncondensed analysis. Status every item against *this* repo. Do not treat a tracker row as a commitment to ship. Do not collapse Commerce subscriptions (Hub-owned catalog + lifecycle) with Payments cashier (ad-hoc amount + metadata) or with Aura SaaS seats (Paddle MoR, outside Hub). Aura is a Hub customer, not a competitor.

---

## Method

This file answers one product-engineering question:

> For every subscription-lifecycle job Stripe Billing, Chargebee, and Paddle sell as table-stakes, what does Lazuar Pay Commerce actually implement, what is a slice, and what is absent?

It is **not**:

- A rewrite of `docs/001-gaps/07-commerce-module.md` (that document is **stale** — cancel, record-payment, portal cancel, coupon confirm, session expiry, payment-failed → PAST_DUE, and typed dunning cancel/suspend events now exist).
- A dunning-campaign design document (campaigns are in scope only where they change subscription state). Sibling file `12-dunning-and-recovery.md` owns recovery depth.
- An accounting audit of the Billing ledger (credits wallet is named only to keep it *out* of “metered subscription”).
- A commitment to become a Stripe Billing clone.

### How the repo was read

1. **Domain aggregates** under `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/` — `Product`, `Subscription`, `CheckoutSession`, `Order`, `Coupon`, `DunningCampaign`.
2. **Command handlers** under `Modules/Commerce/Application/Commands/` — checkout, zero-amount, manual enroll, offline mark-paid, admin/portal cancel, record-payment, refund, dunning pause/resume.
3. **Workers** under `Modules/Commerce/Infrastructure/Workers/` — `BillingEngineJob`, `DunningEngineJob` (Claim / PreDunning / PastDue / Dispatch), `CheckoutSessionExpiryJob`, inbox/outbox.
4. **Payment event handlers** — `GatewayPaymentCompletedIntegrationEventHandler` (open checkout + subscription renewal), `GatewayPaymentFailedIntegrationEventHandler`, `GatewayRefundCompletedIntegrationEventHandler`.
5. **HTTP** — TypeSpec `packages/api-spec/modules/commerce/{admin-routes,public-routes,models}/*` and the matching Minimal API files under `Infrastructure/Endpoints/`.
6. **Frontends** — `apps/lazuar-ops` subscriber directory + product form; `apps/lazuar-portal` checkout + portal page + `CommunityPortalView`.
7. **Cross-module** — Communications lifecycle templates + `FulfillmentRequested` / `LifecycleEventHandlers`; Payments `GenerateCheckoutSessionQuery` + gateway customer-portal adapters; Billing prepaid wallet (explicitly *not* subscription usage).
8. **Tests** under `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/` as evidence of intended behavior, not as a completeness claim.
9. **Competitor docs** current as of crawl windows 10–16 August 2026: Stripe `docs.stripe.com/products-prices/pricing-models` and Billing trial/cancel/pause pages; Chargebee Billing 2.0 subscriptions / plans / proration / trial / gift; Paddle `developer.paddle.com/build/subscriptions/cancel-subscriptions` and pause API.

### Status vocabulary used in this file

Aligned with `20-sequencing-and-tracker-schema.md` Layer B / Layer C so the living tracker can promote rows without a second taxonomy.

| Field | Values |
|-------|--------|
| **ours_depth** | `shipped` · `partial` · `none` · `doc_off` · `stub` · `killed` · `n/a` |
| **competitor cell** | `Y` production-grade · `P` slice · `N` not in product · `—` not applicable |
| **verdict** | Ours · Theirs · Both · Partial · Later · Never · N/A |

**Honesty rules applied here:**

- A field that is stored and shown in ops UI but **never applied at charge time** is `partial`, not `shipped`.
- A UI copy that promises period-end cancel while `Cancel()` flips status immediately is `partial`.
- `TRIALING` mentioned only as a string in an anonymize handler, with no constructor path, is `none` (dead vocabulary).
- Tenant **utility credits** (`billing.TenantCreditBalance`) are a **different product** from metered subscription usage. They do not count as usage billing.
- Paddle as **Aura SaaS MoR** (salon → Aura) is **out of this chapter**. Paddle as a **competitor billing engine** for creator/SaaS subscriptions is in.

### Product-line lock (do not contradict)

From `apps/lazuar-docs/docs/guide/product-lines.md` and `plans/007-feats/README.md`:

| Product | Owner | Events |
|---------|-------|--------|
| Payments cashier | Integrator’s domain objects | `payment.completed` / `payment.failed` |
| **Commerce** (this file) | Hub catalog + subscriptions | `subscription.*`, `order.completed`, `payment_link.paid` |
| LHDN | Tax documents | `invoice.*` |
| Aura Plan | Paddle MoR, **not Hub** | Paddle subscription events, ignored on Hub guest webhook |

Standing constraints: Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank. Wrap rails; do not rebuild acquiring. Do not become a website builder, marketplace, POS, or ERP.

Frozen outbound Commerce types (`packages/api-spec/modules/commerce/models/webhooks.tsp`): `subscription.activated` · `subscription.resumed` · `subscription.past_due` · `subscription.canceled` · `subscription.suspended`. **Do not emit `subscription.updated`.**

---

## Domain model in repo

### Layout

Commerce is a four-layer module:

| Layer | Path | Role |
|-------|------|------|
| Domain | `Modules/Commerce/Domain` | Aggregates, VOs, charge-attempt entity, coupon domain events |
| Application | `Modules/Commerce/Application` | Command handlers, checkout metadata helper, webhook payload builder, lifecycle fan-out |
| Contracts | `Modules/Commerce/Contracts` | Commands, integration events, `IMagicLinkTokenService`, `ISubscriberQueryService` |
| Infrastructure | `Modules/Commerce/Infrastructure` | EF `commerce` schema, endpoints, workers, Dapper query service, HMAC portal tokens |

Registered hosted workers (`Infrastructure/DependencyInjection.cs`):

- `CommerceInboxConsumerJob` / `CommerceOutboxPublisherJob`
- `BillingEngineJob` — interval `BackgroundWorkers:BillingEngineInterval` default **01:00:00**
- `DunningEngineJob` — interval `BackgroundWorkers:DunningEngineInterval` default **01:00:00**
- `CheckoutSessionExpiryJob` — hardcoded **5 minutes**

### Product (the “plan”)

File: `Domain/Aggregates/Product.cs`.

| Field | Type | Notes |
|-------|------|-------|
| `Name`, `Slug` | string | Slug unique per org (DB), lowercased |
| `Price` | decimal | Catalog list / recommended price |
| `PricingModel` | string | Default `"FIXED"`; ops UI also writes `"PWYW"` |
| `MinimumPrice` | decimal | Intended PWYW floor |
| `Currency` | string | Stored; **not** updatable in `UpdateDetails` |
| `Interval` | string | Ops options: `one_time`, `mo`, `yr` |
| `IsActive` | bool | Archive / restore |
| `GatewayName` | string | Per-product rail (Billplz / Stripe / CHIP…) |
| `CheckoutConfiguration` | VO | `RequiresAddress`, `RequiresTaxId`, `RequiresPhone` |
| `FulfillmentTargets` | string list | HTTP webhook URLs or `internal:COMMUNICATIONS` |

There is **no** ProductPrice / PricePoint / Plan child table. One product = one price + one interval. There are no tiers, no add-ons, no setup-fee line, no trial-days column, no billing-cycle-anchor, no tax-inclusive flag, no seat entitlement map.

`UpdateDetails` can change name, slug, price, pricing model, minimum, interval, active, gateway, checkout flags, fulfillment targets. Currency is immutable after create. Archiving a product does **not** migrate, freeze, or cancel existing subscriptions.

Ops form (`apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx`):

- Pricing model dropdown: **Fixed Price** / **Pay What You Want (PWYW)** only.
- Interval dropdown: **One-Time Payment** / **Monthly (`mo`)** / **Yearly (`yr`)** only.
- Currency hard-coded **MYR** on submit.
- Billplz warning: “Billplz is online checkout only — it cannot vault cards or run silent auto-charge / dunning retries.”

TypeSpec (`models/product.tsp`) leaves `pricing_model` and `interval` as free `string`. There is no closed enum for `GRADUATED` / `VOLUME` / `STAIRSTEP` / `METERED` / `PER_SEAT`.

### Subscription (the aggregate)

File: `Domain/Aggregates/Subscription.cs`.

| Field | Meaning |
|-------|---------|
| `Id` | UUIDv7 |
| `OrganizationId` | Tenant |
| `ClientProfileId` | CRM person |
| `ProductId` | Single catalog FK — **the** plan |
| `Status` | String: `PENDING` → `ACTIVE` → `PAST_DUE` → `SUSPENDED` / `CANCELED` |
| `CurrentPeriodEnd` | Period end timestamp |
| `NextBillingDate` | Due date the billing engine claims on |
| `VaultedCustomerId` / `VaultedTokenId` | Gateway customer + reusable token |
| `IsReminderOnly` | True when there is no vault (manual / offline / Billplz-style) |
| `CurrentDunningCampaignId` | Assigned campaign |
| `CurrentDunningStepIndex` | Legacy progress, kept in sync with last completed offset |
| `LastCompletedDayOffset` | Highest dispatched dunning `DayOffset` |
| `DunningPausedUntil` | **Dunning** pause, not subscription pause |
| `SuspendedAt` | Set on `Suspend()` |
| `MetadataJson` | Checkout metadata that survives session expiry (aura_org_id, type, billing_interval) |
| `ReminderLogs` | Child collection, unique per (sub, schedule, target date, offset) |

**There is no field for:** quantity, seats, usage, coupon id, discount remaining, plan-change history, cancel-at, cancel-at-period-end, paused-until (subscription), trial-end, billing-cycle-anchor, timezone, collection-method, setup-fee, grandfathered unit price, scheduled_change, entitlement set.

#### Status machine (implemented)

```text
constructor ──────────────────────────────────────────────► PENDING

Activate(periodEnd, nextBilling, isReminderOnly?) ────────► ACTIVE
  if prior status is PAST_DUE or SUSPENDED:
      Activate() INTENTIONALLY does not move dates
      (arrears config-update path — do not use for recovery)

StoreVaultedToken(customer, token) ── clears IsReminderOnly

MarkAsPastDue() ──────────────────────────────────────────► PAST_DUE

Suspend() ────────────────────────────────────────────────► SUSPENDED  (+ SuspendedAt)

Resume(newNextBilling) ───────────────────────────────────► ACTIVE
  clears SuspendedAt + ClearDunning(); sets NextBillingDate

RecoverFromPayment(periodEnd, nextBilling) ───────────────► ACTIVE
  always advances both dates + ClearDunning()
  used when recovering from PAST_DUE

Cancel() ─────────────────────────────────────────────────► CANCELED
  no CancelAt, no period-end flag, no access-until date
```

`PENDING` is the constructor default. Lists (`GetSubscribersAsync`, portal SQL) filter `Status != 'PENDING'`. Nothing expires a lingering PENDING row.

`TRIALING` appears **only** as a defensive string in `ClientProfileAnonymizedIntegrationEventHandler` (`Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED" or "TRIALING" or "PENDING")`). No `ActivateTrial`, no trial-end job, no product `TrialDays`. It is dead vocabulary.

#### Period math (the only billing calendar)

Every first-activate / recover / record-payment / renewal path uses the same two-line calendar:

```csharp
var nextBilling = product.Interval == "yr"
    ? DateTime.UtcNow.AddYears(1)
    : DateTime.UtcNow.AddMonths(1);
subscription.Activate(DateTime.UtcNow, nextBilling);
```

Consequences:

- **UTC only.** `DateTime.UtcNow` everywhere. No `TimeZoneInfo`, no merchant IANA zone, no `billing_cycle_anchor`. Malaysia timezone exists on **Billing** `B2cConsolidationJob` (`Asia/Kuala_Lumpur`) and is **not** used by Commerce renewals.
- **Interval collapse.** Anything that is not `"yr"` is treated as a month — including `"one_time"` if a handler forgot to branch (record-payment *does* reject `one_time`; billing engine would still claim a due `one_time` if someone set `NextBillingDate`).
- **No day-of-month lock.** A subscribe at 16 Aug 14:03 UTC renews 16 Sep 14:03 UTC, then 16 Oct, then (via `AddMonths`) 16 Nov. End-of-month subscribe on 31 Jan becomes 28/29 Feb via BCL `AddMonths` — implicit, undocumented, not configurable.
- **No calendar billing.** Cannot force “always the 1st”.
- **Arrears recovery resets the clock to now**, not “original anchor + N periods”. A customer who pays 20 days late starts a fresh year/month from the payment instant.
- `CurrentPeriodEnd` and `NextBillingDate` are often set to the **same** value on first activate (`Activate(DateTime.UtcNow, nextBilling)` — period *end* is “now”, next bill is +1 interval). Webhook payload helper `CommerceWebhookPayload` documents that **`current_period_end` in outbound JSON is the paid-through instant = `NextBillingDate`**, not `CurrentPeriodEnd`. Integrators must not treat the column and the webhook field as the same clock.

Manual enroll is the only path that accepts operator-chosen `StartDate` / `NextBillingDate` (`CreateManualSubscriberCommand`). That is an override, not an anchor policy.

### CheckoutSession

File: `Domain/Aggregates/CheckoutSession.cs`.

Two constructors:

1. **Product session** — `organizationId`, `clientProfileId`, `productId`, optional `couponId`, `expiresAt` (always `UtcNow+24h` at initiate).
2. **Ad-hoc custom** — line items (`AdHocLineItem`: description, quantity, unit price), `isB2bRequired`, optional `GatewayName`.

Statuses: `OPEN` → `COMPLETED` / `EXPIRED`. `Expire()` is called by `CheckoutSessionExpiryJob` every 5 minutes for `OPEN && ExpiresAt < now`, which also `ReleaseReservation()` on the coupon.

**Not snapshotted on the session:** amount, currency, quantity, PWYW custom price, coupon discount, billing interval. Price is re-read from Product at completion / offline mark-paid. Quantity used to multiply the **first** gateway charge is not stored; the Subscription created afterwards has no quantity.

Metadata: `MetadataJson` copied onto the Subscription at first activate (`P09 / P10.22`). Gateway metadata still stamps `subscription_id` = **checkout session id** until the payment-completed handler creates a real Subscription. Renewal / update-payment paths stamp the **real** subscription id.

### Order

Minimal one-time completion record: client, product, amount, currency, `PENDING` → `COMPLETED` / `REFUNDED`. Created on one-time product checkout (gateway or zero-amount or offline). `Refund()` exists on the aggregate; Commerce refund handler refunds via Payments event and flips **transaction log** status, not necessarily `Order.Refund()`.

### Coupon

Strongest domain object in the module. `PERCENTAGE` or `FIXED`, global `MaxUses` with reserve/confirm/release, optional `MinimumOriginalPrice`, optional product allow-list, archive/restore. Core code/type/amount locked after first redemption.

**What it is not:** duration (`once` / `repeating` / `forever`), per-customer limit, first-N-months, student verification, gift-code redemption onto a third party, stacking, currency-specific amounts, subscription-attached remaining discount.

Checkout multiplies `CalculateDiscount(product.Price) * quantity`. Fixed-amount coupons therefore discount `Amount × quantity` (may be unintended vs “one fixed off the order”).

Paid path now confirms reservation in `HandleOpenCheckoutSessionAsync`. Expiry job releases. Zero-amount path confirms. These three used to be the P0 coupon-integrity bugs in the old gap doc; they are **implemented**.

Coupons apply to **first checkout only**. Renewals charge `product.Price` with no coupon id on the Subscription.

### DunningCampaign (state-changing surface)

Not a subscription feature, but it is the only path that **cancels or suspends** for non-payment.

- Targeting: product ids + payment methods (`ONLINE_GATEWAY` vs `MANUAL`).
- Steps: `DayOffset` (negative = pre-dunning while ACTIVE and due within 14 days; ≥0 = past-due).
- Actions: `EMAIL` / `WHATSAPP` / `ALL` / `AUTOCHARGE`|`AUTO_CHARGE`.
- `GracePeriodDays` + `FinalAction` `CANCEL` | `SUSPEND` | `NONE`.
- Metrics: recovered revenue, saved, churned.

WhatsApp is demoted to email when `Messaging:WhatsAppEnabled` is false (`DunningEngineJob.Dispatch`).

Pause/resume on a **subscriber** pauses **dunning**, not billing: `DunningPausedUntil`. Billing engine does not look at that flag. An ACTIVE vaulted sub still auto-debits on `NextBillingDate` even if dunning is paused.

### Charge attempts

`ChargeAttemptLog` + `ChargeAttemptLimits.MaxAttemptsPerBillingCycle = 4`.

- Billing engine owns attempt **1** (`SourceBilling`) for vaulted ACTIVE due subs.
- Dunning `AUTO_CHARGE` owns attempts **2–4**.
- Success/fail stamped from `GatewayPaymentCompleted` / `GatewayPaymentFailed` via `charge_attempt_id` metadata or latest PENDING log.

Billplz cannot vault; those subs go `IsReminderOnly` and the billing engine marks them `PAST_DUE` (no charge) and emits `subscription.past_due`.

### Workers in detail

#### BillingEngineJob

Cadence: hourly (configurable). Batch: 50, `FOR UPDATE SKIP LOCKED`, skip failed ids in-cycle.

Claim SQL:

```sql
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PAST_DUE', 'SUSPENDED', 'CANCELED')
```

`PENDING` and `ACTIVE` (and any future unknown status) are eligible. `TRIALING` would be billed if it existed.

- **Has vault:** insert attempt 1 if none for that target date; publish `Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent` with **`product.Price`** (not a snapshotted amount, not quantity × price, not couponed price) and `product.GatewayName`.
- **No vault:** `MarkAsPastDue()`, outbound `subscription.past_due` + `internal:` fulfillment. (Older gap doc said this emitted `subscription.suspended`; **current code emits `subscription.past_due`**.)

The engine **does not advance `NextBillingDate`** on dispatch. Period moves only when `GatewayPaymentCompleted` hits `HandleSubscriptionPaymentAsync`. That is correct against double-charge if the webhook never arrives (the row stays due and will be re-claimed; attempt count blocks a second attempt 1). It is wrong if Payments succeeds and Commerce never sees the event — the customer is charged, Commerce stays due, dunning can fire, a later retry can charge again unless Payments is idempotent on its side.

#### DunningEngineJob

Two claim modes per hourly cycle:

- **Pre-dunning:** `ACTIVE`, next bill in (now, now+14d], not paused (pause is **not** applied on this SQL — pre-dunning claim does **not** check `DunningPausedUntil`).
- **Past-due:** `Status = PAST_DUE` and (`DunningPausedUntil` is null or ≤ now).

Final action publishes typed `SubscriptionCanceledIntegrationEvent` / `SubscriptionSuspendedIntegrationEvent` (this **is** implemented; the old gap doc is wrong).

#### CheckoutSessionExpiryJob

Expires OPEN sessions past `ExpiresAt`, releases coupon reservations. 5-minute loop. No customer email (“your cart expired”).

### Endpoints that mutate subscription state

#### Admin (`/admin/commerce`, OrgAdmin)

| Method | Route | Handler | Effect |
|--------|-------|---------|--------|
| GET | `/subscribers` | query | Paginated list; **loads all org rows then filters search in memory** |
| GET | `/subscribers/export` | query | CSV cap 10_000, UTF-8 BOM |
| POST | `/subscribers` | `CreateManualSubscriber` | Reminder-only ACTIVE; optional ledger; optional activated webhook |
| POST | `/subscribers/portal-link` | Payments `GenerateCustomerPortalQuery` | **Stripe Billing Portal URL**, not Hub portal |
| POST | `/subscribers/{id}/cancel` | `CancelAdminSubscription` | Immediate `Cancel()` + typed canceled event |
| POST | `/subscribers/{id}/record-payment` | `RecordSubscriberPayment` | Advance period; ledger if amount > 0; resume/activated events |
| POST | `/subscribers/{id}/dunning/pause` | `PauseSubscriberDunning` | `DunningPausedUntil` |
| POST | `/subscribers/{id}/dunning/resume` | `ResumeSubscriberDunning` | Clear pause |
| POST | `/checkouts/{id}/mark-paid` | `MarkCheckoutAsPaidOffline` | Completes session; product path creates Sub/Order |
| POST | `/transactions/{id}/refund` | `RecordRefund` | Payments refund request; does not cancel the sub |

**Missing admin routes (still):** GET subscriber by id, ban, change-plan, set-quantity, pause-subscription, cancel-at-period-end, import CSV, attach coupon, set trial, set anchor.

Ops `SubscribersPage.tsx` no longer calls a `ban` route. Status filter is client-side (`ALL` vs `sub.status`). Payment history is global transactions filtered by **customer email**, not a true subscription ledger.

#### Public (`/public/commerce`)

| Method | Route | Effect |
|--------|-------|--------|
| GET | `/{tenantSlug}/products/{slug}` | Buy-link catalog |
| GET | `/{tenantSlug}/validate-coupon` | Preview |
| POST | `/checkout` | Initiate; quantity default 1; metadata persisted |
| GET | `/{tenantSlug}/checkout/{sessionId}/status` | Poll; **does not mint magic tokens** |
| GET | `/checkout/{subId}/status` | Legacy; requires `tenant_slug`; no token mint |
| GET | `/{tenantSlug}/custom-checkouts/{sessionId}` | Quote / payment link |
| GET | `/checkout/{subId}/arrears` | Product **list price**, not arrears balance |
| POST | `/checkout/{subId}/update-payment` | Hosted recovery checkout; metadata uses **real** sub id |
| GET | `/{tenantSlug}/portal?token=` | Aggregated subs + orders for the token’s client |
| POST | `/{tenantSlug}/portal/cancel` | Immediate cancel if token’s client owns the sub |

TypeSpec still documents `requestMagicLink` / `getBillingLink` in older mental models; **current `public-routes.tsp` does not include them**. Magic tokens are minted by Communications fulfillment (`IMagicLinkTokenService.GenerateToken`) when sending `{{portal_magic_link}}`, not by a public “email me a link” endpoint. Checkout status deliberately stopped minting tokens (anonymous poll must not become a login).

### Create / renew / recover paths (what actually writes a Subscription)

1. **Online first payment** — `InitiateCheckout` → Payments hosted page (`SetupFutureUsage = product.Interval != "one_time"`) → `GatewayPaymentCompleted` open-session path → new Subscription, vault if gateway returned ids, `SubscriptionActivated(IsFirstPayment: true)`.
2. **Zero-amount** — 100% coupon (or free price) → `ProcessZeroAmountCheckout` → activate + `ZeroAmountCheckoutCompleted` (Billing discount ledger) + activated event. **No vault.** Next cycle the billing engine will mark PAST_DUE (no token).
3. **Offline mark-paid on product session** — `MarkCheckoutAsPaidOffline` → Sub with `isReminderOnly: true`, activated event, tx log `MANUAL_OFFLINE`, ledger via `ManualSubscriberEnrolled`.
4. **Manual enroll** — always `isReminderOnly: true`. `COMPED` skips ledger. Optional welcome = activated event (webhooks), not a Communications “welcome” template (that template is an **orphan**).
5. **Renewal auto-debit** — billing engine → off-session charge → `HandleSubscriptionPaymentAsync` → `Activate` (if already ACTIVE) or `RecoverFromPayment` / `Resume`.
6. **Update-payment / arrears hosted checkout** — public update-payment → new Payments session with real `subscription_id` → same handler as (5).
7. **Record-payment (ops)** — cash / bank / COMPED against an existing sub. Rejects `PENDING` and `CANCELED`. Advances clock. COMPED amount forced to 0 and skips ledger.

Custom payment links (`type=custom_payment_link`) **never create a Subscription**. They complete the session, log a transaction, emit `payment_link.paid`.

### Customer portal (ours)

Two different “portals” exist. Mixing them is how reviews get dishonest.

#### A. Hub Commerce portal (`lazuar-portal`)

- Route: `/{tenantSlug}/portal?token=`.
- Token: HMAC-SHA256 over `{subscriptionId}:{expiryUnix}`, Base64, **24h TTL**, secret = `Jwt:Secret` (`MagicLinkTokenService`).
- Data: all non-PENDING subscriptions **and** orders for the **same ClientProfileId** as the token’s subscription (`CommerceQueryService.Portal.cs`).
- DTO: id, product id/name, status, `current_period_end` only. No invoices, no payment method, no quantity, no upcoming invoice, no plan picker.
- Cancel: `POST /public/commerce/{tenantSlug}/portal/cancel` with `{ subscription_id }`. Immediate `Cancel()`. Idempotent if already `CANCELED`. Allowed from `ACTIVE` | `PAST_DUE` | `SUSPENDED`.
- **`apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx`** — Cancel Plan button for `ACTIVE` or `PAST_DUE`. No confirmation copy about period end.
- **`CommunityPortalView.tsx`** (present in the repo, not wired by the current portal page) **lies**: confirm dialog says “You will lose access at the end of your billing cycle” and the danger-zone copy says the customer retains access until `current_period_end`. The API does not do that.
- Invoice download is `[MVP-HIDE]`.
- No self-serve update payment method on this page (that is a separate `/{tenantSlug}/update-payment/{subId}` flow for arrears only).
- No self-serve pause, plan change, quantity, coupon, resume.

If the token is missing, the page asks the user to “log in using a secure magic link”. There is no public `requestMagicLink` endpoint to send that email from the portal itself.

#### B. Gateway customer portal (Stripe Billing Portal)

`POST /admin/commerce/subscribers/portal-link` → `GenerateCustomerPortalQuery` → adapter:

- **Stripe:** real Billing Portal session (`Stripe.BillingPortal.SessionService`).
- **Billplz:** throws “does not provide a managed customer billing portal.”
- **CHIP / Razorpay:** adapter methods exist; CHIP’s is not a Stripe-class portal.

This is **Stripe’s** self-serve (card update, Stripe-hosted invoices, Stripe-hosted cancel rules), not Hub’s subscription state machine. Canceling inside Stripe Portal does **not** automatically call `subscription.Cancel()` unless a Stripe webhook is mapped — Commerce’s payment-failed/completed handlers look at `subscription_id` metadata on **Hub-originated** charges, not at Stripe Subscription objects. Hub Commerce subscriptions are **not** Stripe Subscription objects; they are Hub rows that sometimes vault a Stripe Customer + PaymentMethod.

### Credits wallet (related, different)

`Modules/Billing/Domain/Aggregates/TenantCreditBalance` is a **tenant prepaid utility wallet** (integer credits for LHDN submit / WhatsApp send). It has holds, clawback, starter grant. It is **not**:

- A customer usage meter
- A subscription entitlement
- A credit-burndown pricing model
- Attached to `commerce.Subscriptions`

Do not score “we have credits” as metered billing.

### Frontend quantity / PWYW honesty

`CheckoutView` keeps `quantity` state (default 1) and a PWYW `customPrice`. `OrderSummaryCard` renders a PWYW number input against `minimum_price`. `CheckoutForm` **sends `quantity`** and **does not send custom price**. There is **no quantity stepper in the form JSX** — `onQuantityChange` is never invoked by a control. First charge uses `product.Price * quantity` (almost always ×1). PWYW display price is cosmetic; the gateway charge is catalog `Price`.

### Interval and gateway coupling

| Interval | First charge | Vault? | Renewal |
|----------|--------------|--------|---------|
| `one_time` | Yes | `SetupFutureUsage=false` | Creates `Order`, not Subscription |
| `mo` / anything not `yr` | Yes | true if not one_time | `AddMonths(1)` |
| `yr` | Yes | true | `AddYears(1)` |

Billplz: checkout works; vault/off-session throws. Result: reminder-only subscription after first paid period; hourly job will PAST_DUE at next bill; customer must use update-payment / record-payment.

### Events actually published

| Event | Publishers |
|-------|------------|
| `SubscriptionActivatedIntegrationEvent` | Open checkout, zero-amount, offline product, manual welcome, record-payment (non-resume), renewal handler |
| `SubscriptionResumedIntegrationEvent` | Recovery from SUSPENDED (payment completed or record-payment) |
| `SubscriptionCanceledIntegrationEvent` | Admin cancel, portal cancel, dunning final CANCEL, GDPR anonymize |
| `SubscriptionSuspendedIntegrationEvent` | Dunning final SUSPEND |
| `subscription.past_due` (outbound webhook only, no typed C# event) | Billing engine no-vault; payment-failed handler first transition |
| `ManualSubscriberEnrolledIntegrationEvent` | Manual enroll, record-payment, offline mark-paid |
| `ZeroAmountCheckoutCompletedIntegrationEvent` | Free checkout |
| `OrderCompletedIntegrationEvent` | One-time complete |
| `FulfillmentRequestedIntegrationEvent` | Dunning comms, past-due internal targets, final-action internal targets |
| `OutboundWebhookRequestedIntegrationEvent` | Lifecycle handlers, engines |

Communications:

- `LifecycleEventHandlers` listens to **Suspended** (sends **Payment Failed** template with hard-coded `https://portal.lazuar.com/checkout/update`) and **Canceled** (Subscription Cancelled template).
- `FulfillmentRequested` for `reminder.due` / `reminder.dunning` populates `{{portal_magic_link}}` correctly (real token) and `{{renewal_link}}` as the **bare** portal URL without token.
- Catalog templates that exist: Payment Failed, Subscription Cancelled, Digital Product Delivery, Quotation Ready, Official Receipt.
- **Orphans (seeded historically, no consumer):** Community Welcome, Subscription Renewal (3 Days), Subscription Renewal Due Today, Subscription Renewal Overdue, Abandoned Cart, etc.

There is **no** Communications consumer of `SubscriptionActivated` for a welcome email. Manual “send welcome” only fires the activated **webhook**.

### Tests that lock current behavior

Under `tests/Lazuar.ModuleTests/Commerce/`:

- `SubscriptionLifecycleWebhookTests` — outbound payload shape / frozen event names
- `SubscriptionRecoveryTests` — PAST_DUE / SUSPENDED date advancement
- `GatewayPaymentFailedIntegrationEventHandlerTests` — PAST_DUE + campaign assign
- `BillingEngineJobTests` — claim / no-vault past_due
- `CouponLifecycleTests` — reserve / confirm / release
- `MagicLinkTokenServiceTests`
- `CommerceProductCompletenessTests` — checkout config enforcement

There is **no** test for cancel-at-period-end, proration, plan change, trial, quantity persistence, PWYW charge amount, or import.

---

## Competitor baseline

The three comparators are **billing engines**. Lazuar Pay Commerce is a **creator/merchant checkout + simple recurring entitlement** sitting on BYOK gateways. Feature parity with Stripe Billing is not the company shape — but the *jobs* below are how merchants describe “subscriptions,” so each job still gets a cell.

### Stripe Billing (2026)

Sources: Stripe docs “Recurring pricing models” (crawled 2026-08-15), Billing trials, cancel, pause, billing cycle, usage-based billing; Stripe Billing marketed as 15+ models; Billing fee **0.7% of billing volume** (Starter/Scale merge).

**Catalog.** Product + many Prices. A Price has currency, interval (`day` / `week` / `month` / `year` + `interval_count`), usage type (`licensed` vs `metered`), billing scheme (`per_unit` vs `tiered`), tiers with `up_to` + `unit_amount` + `flat_amount`, `tiers_mode` **`volume`** (one unit price for the whole quantity) or **`graduated`** (each tier’s slice). Package pricing (sell a pack of N). Transform quantity. Tax behavior.

**Stairstep** is the Chargebee name for “flat amount per tier, no per-unit inside the tier.” Stripe expresses it as tiered prices with `flat_amount` and `unit_amount = 0`.

**Trials.** Classic `trial_period_days` / `trial_end` on Subscription; `trial_settings.end_behavior.missing_payment_method` = cancel vs create invoice. 2026 preview **Trial Offer API**: paid or free intro price + duration, then transition to regular price. `status = trialing`. Trialing still occupies a billing period clock.

**Setup fees.** Not a first-class Price field; implemented as a one-time invoice item / first-invoice extra line / subscription schedule phase / `add_invoice_items`. Universally used.

**Free products.** `$0` prices; 100% forever coupons; `trialing` with no PM.

**Renewal / anchors / timezone.** Subscriptions have `billing_cycle_anchor`, `billing_cycle_anchor_config` (`day_of_month`, `month`), `proration_behavior` on changes. Account / customer tax timezone. Renewal is Stripe-hosted (not a merchant hourly job). `collection_method` `charge_automatically` vs `send_invoice`. Smart Retries + email dunning on the Billing product.

**Cancel.** `cancel` now (optional `invoice_now`, `prorate`) vs `cancel_at_period_end = true` (status stays `active`, `cancel_at` set). Customer Portal default is period-end. Cancel can be reversed before the timestamp.

**Pause.** `pause_collection` with behavior `keep_as_draft` / `mark_uncollectible` / `void`. Subscription remains; invoices pause. Resume clears it.

**Plan change + proration.** Update subscription items (swap Price, change quantity). `proration_behavior`: `create_prorations`, `none`, `always_invoice`. Credit proration deferral. Subscription Schedules for timed phase changes (intro → regular, annual flip).

**Quantity.** First-class on each Subscription Item. Per-seat model is quantity on a licensed price.

**Usage / metered.** Billing Meters + meter events; licensed vs metered items on the same subscription; overage, credit burndown. Not a prepaid tenant wallet.

**Multi-seat / entitlements.** Quantity + Stripe Entitlements (feature → product mapping, customer entitlement list). Integrators still enforce access; Stripe tells them what is active.

**Customer portal.** Hosted Billing Portal: payment method, invoices, cancel (configurable), plan switch (configurable), quantity (configurable), shipping. Deep links.

**Import.** Stripe has Data Migration / import subscriptions with `backdate_start_date`, `billing_cycle_anchor`, `proration_behavior=none`, trial_end tricks. Official “migrate to Stripe Billing” playbooks.

**Offline / manual.** `send_invoice` + out-of-band payment; customer balance; mark invoice paid out of band. Not “reminder-only with no invoice object.”

**Coupons / gift / student / lifetime.** Coupons + Promotion Codes: percent/amount, duration once/repeating/forever, first-time transaction, customer restrictions. No first-class student ID. No first-class gift-subscription (workarounds: coupon + separate customer). Lifetime = one-time payment or `$0` forever price / 100% forever coupon.

### Chargebee Billing 2.0 (2026)

Sources: Chargebee docs subscriptions, plans, trial management, proration (day-based vs millisecond-based; **day-based definition changed for sites created on/after 19 May 2026**), pause, cancellation, gift subscriptions, next billing date; product catalog pricing models.

**Catalog.** Plan + addons + charges. Price points per plan. Pricing models documented: **flat fee, per unit, volume, tiered (graduated), stairstep**. Frequency independent of plan via multi-frequency billing. Trials on the price point. Setup cost / setup fee on plan. Coupons, attached items.

**Statuses that matter vs us:** In Trial, Active, Non Renewing (cancel at term end), Paused, Cancelled, Transferred.

**Trials.** Plan checkbox + duration; card-on-file vs no-card; convert or cancel at trial end. First-class `in_trial`.

**Setup fees.** First-class plan/price setup cost.

**Renewal / anchors / timezone.** Site timezone. Next billing date override. Calendar billing / billing alignment (charge everyone on the 1st). Billing modes: day-based vs millisecond-based proration. Contract terms, billing cycles (N renewals then stop).

**Cancel.** Immediate vs end of term (`Non Renewing`). Cancellation settings (bill the cancel day or not — relevant for pre-2026-05-19 day-based sites). Reactivation of cancelled (Chargebee can reactivate; Paddle cannot).

**Pause / resume.** First-class paused status; pause date + resume date; optionally skip charges.

**Plan change + proration.** Change plan / addon / quantity mid-cycle with proration; price override; scheduled changes; ramps.

**Quantity.** Per-unit / volume / stairstep all take quantity. Seat families are quantity + entitlements add-on.

**Usage.** Metered billing, usage file upload, usage events. Separate from Coupons.

**Entitlements.** Feature entitlements attached to plans/addons; entitlements API.

**Portal.** Chargebee self-serve portal: cancel, update PM, change plan (configurable), invoices.

**Import.** Documented migration / import subscriptions (backdating is a first-class Chargebee feature).

**Offline.** Cash, check, bank transfer as payment methods; record payment against invoice; `offline` collection.

**Coupons / gift / student / lifetime.** Coupons with duration and constraints. **Gift Subscriptions** are a named Chargebee feature (gifter pays, giftee receives a subscription). Student typically = coupon + manual verification. Lifetime plans exist as non-renewing / one-time + entitlement.

**2026 commercial note (context only):** Starter free up to $250K cumulative billing, Performance ~$599/mo or $7,188/yr, 0.75% overage — Chargebee is a **revenue-share billing OS**, not a BYOK cashier.

### Paddle Billing (2026)

Sources: Paddle developer cancel page (crawled 2026-08-12), pause API, proration / replace products docs, customer portal concepts. Paddle is **Merchant of Record**.

**Catalog.** Product + Price. Recurring prices have `billing_cycle` `{ frequency, interval: day|week|month|year }` and optional `trial_period`. Quantity min/max on the price. Multiple items on one subscription (plan + addons). Custom data for feature flags (entitlements-by-convention).

**Pricing models.** Unit price × quantity is the native model. No first-class graduated/volume/stairstep catalog (you approximate with separate prices or quantity bands). Usage is limited compared with Stripe meters / Chargebee metered.

**Trials.** `trial_period` on price; subscription `status` includes trialing / trialing past due (Paddle trial statuses exist in the API).

**Setup fees.** One-time prices on the same subscription / first transaction line items. Not as tidy as Chargebee setup cost.

**Renewal / anchors / timezone.** Paddle runs the clock. `current_billing_period`, `next_billed_at`, `billing_cycle`. Collection mode `automatic` vs `manual` (invoice). Cannot change a subscription if next bill is within **30 minutes**, or if `past_due`.

**Cancel.** **Two modes, first-class:** `effective_from = next_billing_period` (default) creates `scheduled_change.action = cancel`, status stays `active`, `next_billed_at = null` until the date; or `immediately` → `status = canceled`, `canceled_at` set. Portal cancel = period-end. Emails from Paddle include a cancel link (compliance). **Canceled cannot be reinstated** — create a new subscription (Paddle documents the replay-from-items flow). Remove scheduled change to undo period-end cancel.

**Pause.** `POST /subscriptions/{id}/pause`. Default pause **at period end** via `scheduled_change`. `effective_from` can be immediate. Resume endpoint. Pause is the supported “temporary break”; cancel is permanent.

**Plan change + proration.** Replace products/prices; proration bill now vs next period vs do not prorate. Add/remove items (seats as quantity). One-time charges on the subscription.

**Quantity.** First-class on each item (Paddle examples use `quantity: 10` seats).

**Usage / metered.** Not Paddle’s center of gravity. Some overage via one-time charges / extra items.

**Entitlements.** Convention via `custom_data.features` on prices; you enforce. No Chargebee-style entitlements service.

**Customer portal.** Hosted. Portal sessions with deep links: `cancel_subscription`, `update_subscription_payment_method`, overview. Management URLs on the subscription entity (tokenized, expire, omitted from webhooks).

**Import.** `import_meta` exists on entities; migration support is real but thinner than Chargebee’s backdating suite. Paddle as MoR also means **tax/liability** migrates, not just rows.

**Offline / manual.** `collection_mode = manual` + invoices. Paddle still invoices as MoR; this is not “record a cash payment and keep a reminder-only row.”

**Coupons / gift / student / lifetime.** Discounts on transactions / subscriptions. No Chargebee Gift Subscriptions product. Student = discount. Lifetime = non-recurring product or manual.

**Company-shape clash with Lazuar Pay:** Paddle is MoR (they are the merchant). Lazuar Pay Commerce is **BYOK** (the creator is the merchant; Hub is not MoR for GMV). Copying Paddle’s portal UX is fine; copying Paddle’s tax/MoR model would contradict Hub’s product line and break LHDN (the Malaysian seller must issue the e-invoice).

### Side-by-side snapshot (jobs, not scores)

| Job | Stripe | Chargebee | Paddle | Lazuar Pay Commerce |
|-----|--------|-----------|--------|---------------------|
| Flat / per-unit price | Y | Y | Y | Y (one price on Product) |
| Graduated / volume / stairstep | Y | Y | N (approximate) | N |
| PWYW | N (custom amount Checkout, not a Price mode) | N | N | **Partial** (UI + column; charge ignores it) |
| Trials | Y | Y | Y | N (`TRIALING` string only) |
| Setup fee | P (invoice item) | Y | P (one-time item) | N |
| Free / $0 / 100% coupon | Y | Y | Y | Y (zero-amount path) |
| Renewal job | Stripe-hosted | Chargebee-hosted | Paddle-hosted | **Our** hourly `BillingEngineJob` |
| Billing anchors / calendar billing | Y | Y | P | N (UTC + AddMonths/AddYears) |
| Merchant timezone | Y | Y (site TZ) | P | N (UTC; MYT only on Billing B2C job) |
| Cancel at period end | Y | Y (Non Renewing) | Y (default) | N (immediate only) |
| Cancel immediate | Y | Y | Y | Y |
| Pause subscription | Y (`pause_collection`) | Y (Paused) | Y (scheduled/immediate) | N (dunning pause only) |
| Plan change + proration | Y | Y | Y | N |
| Quantity on the subscription | Y | Y | Y | Partial (checkout multiply; not persisted) |
| Metered usage | Y (Meters) | Y | P | N (wallet ≠ usage) |
| Multi-seat / entitlements | Y | Y | P (quantity + custom_data) | N (webhooks only) |
| Self-serve portal | Y | Y | Y | Partial (list + immediate cancel + arrears pay) |
| Import existing subscribers | Y | Y | P | N (manual one-by-one; CSV export only) |
| Offline / manual collection | Y (send_invoice) | Y | P (manual collection) | Y (reminder-only + record-payment) |
| Coupons | Y (duration, promo codes) | Y | Y (discounts) | Partial (first invoice only, no duration) |
| Gift subscriptions | N | Y | N | N |
| Student | N | N (coupon) | N | N |
| Lifetime | P | P | P | N (COMPED is complimentary enroll, not a SKU) |

---

## Gap table

Every required topic. **Src** is the primary evidence in *this* repo. Competitor cells are Stripe / Chargebee / Paddle.

Legend — **ours_depth:** shipped / partial / none. **V:** verdict for Lazuar Pay vs the job.

### 1. Plan models (flat, graduated, volume, stairstep)

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-001 | Flat / fixed recurring price | **shipped** | Y | Y | Y | Both | `Product.Price` + interval `mo`/`yr`; checkout and renewals charge it |
| SL-002 | One-time product (not a sub) | **shipped** | Y | Y | Y | Both | `Interval == "one_time"` → `Order` |
| SL-003 | Per-unit / quantity-priced plan | **partial** | Y | Y | Y | Partial | Checkout multiplies `Price * Quantity`; Subscription has no quantity; renewals ignore it |
| SL-004 | Graduated (tiered slices) | **none** | Y | Y | N | Later* | No tiers table, no `tiers_mode` |
| SL-005 | Volume (one price for whole qty) | **none** | Y | Y | N | Later* | Same |
| SL-006 | Stairstep (flat per band) | **none** | P | Y | N | Later* | Chargebee-named; Stripe via `flat_amount` |
| SL-007 | PWYW / name-your-price | **partial** | N | N | N | Partial | Column + ops + portal input; **InitiateCheckout uses `product.Price` only** |
| SL-008 | Multiple prices per product (mo/yr as prices, not products) | **none** | Y | Y | Y | Later | We duplicate products (`basic-mo` / `basic-yr`) |
| SL-009 | Weekly / daily / custom interval_count | **none** | Y | Y | Y | Later | Only `mo` / `yr` / `one_time`; non-`yr` collapses to month |
| SL-010 | Add-ons / extra subscription items | **none** | Y | Y | Y | Later | One `ProductId` on the sub |
| SL-011 | Grandfathered unit price on the sub | **none** | Y | Y | Y | Later | Renewals always re-read `product.Price` — **price edits bill every renewer** |

\*Later only if a merchant JTBD appears (API / seat bands). Not table-stakes for MY creator memberships.

**Evidence — PWYW not charged:**

```130:148:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
        decimal basePrice = product.Price * request.Quantity;
        ...
        coupon.Validate(product.Price, product.Id);
        discountAmount = coupon.CalculateDiscount(product.Price) * request.Quantity;
```

`PublicCheckoutRequestDto` has no `custom_amount` / `unit_price` field (`models/checkout.tsp`).

**Evidence — no tier model:** `Product.PricingModel` is a free string defaulting to `FIXED`; ops select options are `FIXED` | `PWYW` only (`ProductForm.tsx`).

**Honesty gap:** Changing `Product.Price` in ops silently changes every future auto-debit and every arrears checkout (`PublicArrearsEndpoints` selects `p."Price"`). Competitors snapshot the Price id on the subscription item.

### 2. Trials, setup fees, free products

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-012 | Free trial with card on file | **none** | Y | Y | Y | Later | No `trial_end`, no `TRIALING` write path |
| SL-013 | Free trial without card | **none** | Y | Y | P | Later | Manual COMPED is not a timed trial |
| SL-014 | Paid trial / intro price (Trial Offer) | **none** | Y | P | P | Never* | Stripe 2026 Trial Offer API |
| SL-015 | Trial-end behavior (cancel vs convert) | **none** | Y | Y | Y | Later | — |
| SL-016 | Setup / joining fee on first invoice | **none** | P | Y | P | Later | No first-invoice extra line |
| SL-017 | Free product / $0 price | **partial** | Y | Y | Y | Partial | `$0` or 100% coupon → zero-amount; then no vault → next cycle PAST_DUE |
| SL-018 | Complimentary enroll (staff grant) | **shipped** | P | Y | P | Both | `payment_method=COMPED` |

\*Never unless we decide to compete as a SaaS billing OS. Intro prices can be a second Product.

**Evidence — TRIALING is a ghost:**

```57:61:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs
            if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED" or "TRIALING" or "PENDING"))
            {
                // Still cancel any non-terminal status we do not model strictly.
```

Constructor always sets `PENDING`. `Activate` always sets `ACTIVE`. Billing engine would treat a hypothetical `TRIALING` row with a due `NextBillingDate` as billable (`NOT IN ('PAST_DUE', 'SUSPENDED', 'CANCELED')`).

**Evidence — free checkout exists:** `ProcessZeroAmountCheckoutCommand` confirms coupon, activates, publishes `ZeroAmountCheckoutCompleted`. Next `NextBillingDate` is +1 month/year. No token stored → `BillingEngineJob` else-branch → PAST_DUE. So “free first month via 100% coupon” is **not** a trial; it is a free first period then an unpaid renewal demand.

**Setup fee:** no field on Product, no `add_invoice_items`, no first-cycle adder in `InitiateCheckout` or billing engine. A merchant who wants “RM 50 join + RM 20/mo” must either bake join into the first product price (then renewals overcharge) or use a separate one-time product (two checkouts).

### 3. Renewal jobs, billing anchors, timezone

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-019 | Hosted / first-party renewal worker | **shipped** | Y | Y | Y | Both | `BillingEngineJob` hourly, SKIP LOCKED, batch 50 |
| SL-020 | Off-session auto-debit with vault | **shipped** | Y | Y | Y | Both | Attempt 1 + Payments `ExecuteOffSessionCharge` |
| SL-021 | Idempotent attempt per billing date | **shipped** | Y | Y | Y | Both | `ChargeAttemptLog` + max 4 |
| SL-022 | No-PM / reminder-only due handling | **shipped** | P | Y | P | Ours* | Mark PAST_DUE + `subscription.past_due` |
| SL-023 | Billing cycle anchor (day of month) | **none** | Y | Y | P | Later | `AddMonths` from payment instant |
| SL-024 | Calendar billing (everyone on the 1st) | **none** | P | Y | N | Later | — |
| SL-025 | Merchant / customer timezone | **none** | Y | Y | P | Later | All Commerce clocks UTC |
| SL-026 | Advance period only after paid | **shipped** | Y | Y | Y | Both | Engine does not bump dates on dispatch |
| SL-027 | Failed charge → PAST_DUE + dunning assign | **shipped** | Y | Y | Y | Both | `GatewayPaymentFailedIntegrationEventHandler` |
| SL-028 | Pre-renewal reminders | **partial** | Y | Y | Y | Partial | Dunning negative offsets, 14-day claim window; default renewal templates are **orphans** |

\*Ours in the sense that reminder-only is a first-class MY cash/bank path competitors treat as a second-class “offline collection method.”

**Evidence — UTC AddMonths, no anchor:**

```55:58:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
        var periodEnd = DateTime.UtcNow;
        var updatedNextBilling = productInfo.Interval == "yr"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);
```

Same snippet in zero-amount, offline, record-payment.

**Evidence — engine claim uses SQL `NOW()`** (Postgres session TZ, typically UTC in this host): `BillingEngineJob.ClaimDueSubscriptionAsync`.

**Evidence — timezone elsewhere, not here:** `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` resolves `Asia/Kuala_Lumpur`. Commerce workers do not.

**Pre-dunning window is 14 days hardcoded** in claim SQL (`NextBillingDate <= NOW() + INTERVAL '14 days'`). A “remind 21 days before annual renew” step will never dispatch.

**Billplz renewals cannot succeed silently.** Product form already warns. That is a **rail** gap, not a lifecycle-model gap: the state machine is correct (reminder-only → PAST_DUE); the auto-debit job is a no-op without a vaulting gateway.

### 4. Cancel at period end vs immediate

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-029 | Immediate cancel (admin) | **shipped** | Y | Y | Y | Both | `CancelAdminSubscriptionCommandHandler` |
| SL-030 | Immediate cancel (customer portal) | **shipped** | Y | Y | Y | Both | `CancelPortalSubscriptionCommandHandler` |
| SL-031 | Cancel at period end / Non Renewing / scheduled_change | **none** | Y | Y | Y | Later | `Cancel()` has no date; no `CancelAt` column |
| SL-032 | Undo scheduled cancel | **none** | Y | Y | Y | Later | Nothing to undo |
| SL-033 | Access until period end after cancel | **none** | Y | Y | Y | Later | Status is `CANCELED` now; integrators should revoke on `subscription.canceled` |
| SL-034 | Reactivate a canceled subscription | **none** | P | Y | N | Later | Must enroll again; Paddle also refuses reinstate |
| SL-035 | GDPR / anonymize cancels all subs | **shipped** | P | P | P | Both | `ClientProfileAnonymizedIntegrationEventHandler` |

**Evidence — cancel is a single assignment:**

```123:127:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs
    public void Cancel()
    {
        Status = "CANCELED";
        UpdatedAt = DateTime.UtcNow;
    }
```

Admin and portal handlers call that, then publish `SubscriptionCanceledIntegrationEvent`. Integrators (Aura SaaS webhook) are documented to **revoke on `subscription.canceled`**. There is no “canceled but paid-through” status.

**Copy bug (customer-facing):** `CommunityPortalView.tsx` lines 28–29 and 156–158 promise period-end access. The live portal page (`portal/page.tsx`) is quieter but still offers Cancel Plan on ACTIVE/PAST_DUE with no “you keep access until …” and no “this is immediate” warning either. Paddle’s portal **defaults** to period-end; Stripe Portal is configurable; we are immediate-only and one UI lies.

`NextBillingDate` is **not** cleared on cancel. Billing engine excludes `CANCELED`, so this is harmless for charges, but days-overdue math in the subscriber list includes `CANCELED` (`Status is "PAST_DUE" or "CANCELED"`), so canceled rows can show a growing overdue number.

### 5. Pause / resume

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-036 | Pause **subscription** (stop billing, keep entitlement) | **none** | Y | Y | Y | Later | No `PAUSED` status; `Resume()` is arrears recovery |
| SL-037 | Pause at period end (scheduled) | **none** | P | Y | Y | Later | Paddle default |
| SL-038 | Resume subscription from pause | **none** | Y | Y | Y | Later | `Resume(newNextBilling)` is for **SUSPENDED**, not a holiday pause |
| SL-039 | Pause **dunning** until a date | **shipped** | P | P | N | Both | `PauseDunning` / ops modal |
| SL-040 | Suspend for non-payment (dunning final) | **shipped** | Y | Y | Y | Both | `FinalAction=SUSPEND` + typed event |
| SL-041 | Resume from suspension on payment | **shipped** | Y | Y | Y | Both | `Resume` + `SubscriptionResumed` |

**Do not confuse these three verbs:**

| Verb | What we have | Competitor analog |
|------|----------------|-------------------|
| Pause dunning | `DunningPausedUntil` — recovery emails/charges (AUTO_CHARGE) wait | Stripe pause_collection is broader (all invoices) |
| Suspend | Access-revoke state after grace | Stripe `unpaid` / Chargebee cancelled-for-dunning / Paddle `past_due` then cancel |
| Pause subscription | **Missing** — “I’m travelling for 2 months, keep my account, don’t bill” | Chargebee Paused, Paddle pause, Stripe pause_collection |

Billing engine **ignores** `DunningPausedUntil`. If you pause dunning on an ACTIVE vaulted sub, the hourly job still fires attempt 1.

`Resume()` always requires a `newNextBillingDate` and clears dunning. There is no “resume on date X without a payment.”

### 6. Plan change + proration

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-042 | Change plan (swap ProductId) on a live sub | **none** | Y | Y | Y | Later | No command, no route, no UI |
| SL-043 | Prorate unused time as credit | **none** | Y | Y | Y | Later | No proration math |
| SL-044 | Prorate unused time as next-invoice credit | **none** | Y | Y | Y | Later | — |
| SL-045 | Change without proration (at next cycle) | **none** | Y | Y | Y | Later | — |
| SL-046 | Subscription schedules / ramps / phases | **none** | Y | Y | P | Never | SaaS-billing-OS feature |
| SL-047 | Price override on one subscriber | **none** | Y | Y | P | Later | Manual enroll amount is first ledger only |
| SL-048 | Preview upcoming invoice after change | **none** | Y | Y | Y | Later | — |

There is no `ChangePlanCommand`, no `ProductId` setter on Subscription after construction, no invoice object to attach a proration line to. The only “change” is: cancel + new checkout, or edit the Product (which mutates **everyone’s** next charge).

Manual `AmountPaid` on enroll / record-payment does **not** become a custom recurring price. Next auto-debit is still `product.Price`.

### 7. Quantity

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-049 | Quantity on first checkout charge | **partial** | Y | Y | Y | Partial | `Price * Quantity` sent to Payments |
| SL-050 | Quantity persisted on Subscription | **none** | Y | Y | Y | Later | No column |
| SL-051 | Quantity on renewal | **none** | Y | Y | Y | Later | Engine charges `product.Price` × 1 |
| SL-052 | Self-serve change quantity | **none** | Y | Y | Y | Later | — |
| SL-053 | Checkout quantity UI | **none** | Y | Y | Y | Later | State exists; **no stepper rendered** |
| SL-054 | Ad-hoc line quantity (custom checkout) | **shipped** | Y | Y | Y | Both | `AdHocLineItem.Quantity` — not a subscription |

`GenerateCheckoutSessionQuery` accepts `Quantity` and `InitiateCheckout` passes `request.Quantity`. Payments may put “×N” on the gateway description (`GatewayCommonTests.ProductDescription_DefaultsAndQuantitySuffix`). That is a **display** concern. Commerce never writes N onto the sub.

Portal `CheckoutForm` includes `quantity` in the POST body but never calls `onQuantityChange`. Effectively N=1 for humans.

### 8. Usage / metered (credits wallet is related but different)

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-055 | Metered subscription item (report usage, bill at period end) | **none** | Y | Y | P | Later / Never* | No meters, no usage events |
| SL-056 | Licensed + overage hybrid | **none** | Y | Y | P | Later / Never* | — |
| SL-057 | Credit burndown attached to a customer sub | **none** | Y | P | N | n/a | — |
| SL-058 | Tenant utility credit wallet (LHDN / WA) | **shipped** | — | — | — | N/A | `TenantCreditBalance` — **not a subscription feature** |
| SL-059 | Include usage on the same invoice as the recuring fee | **none** | Y | Y | P | Later / Never* | We have no invoice aggregate in Commerce |

\*Metered billing is how Stripe/Chargebee win API / AI / seat-overage companies. Lazuar Pay’s ICP in docs is creator memberships + Hub Commerce buy links. Scoring SL-055 as a Wave-0 hole would be marketplace-envy for a different company. Keep the row so we do not “accidentally” reuse the wallet as a fake meter.

**Do not implement usage by overloading `billing.TenantCreditBalance`.** That wallet is org-scoped, integer, fail-closed for platform actions, and already has a double-deduct history on LHDN. Customer-level meters need a different aggregate.

### 9. Multi-seat / entitlements

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-060 | Seats as quantity + licensed price | **none** | Y | Y | Y | Later | Same hole as SL-050 |
| SL-061 | Feature entitlements API | **none** | Y | Y | P | Later | We emit webhooks; integrator stores access |
| SL-062 | Fulfillment targets / outbound webhooks | **shipped** | P | P | P | Ours | Frozen `subscription.*` envelope; `internal:` + HTTP |
| SL-063 | Per-seat invite / assignment UI | **none** | P | P | N | Never | Not a Commerce v1 job; belongs in the integrator app |
| SL-064 | Metadata pass-through for integrator entitlements | **shipped** | Y | Y | Y | Both | `MetadataJson` / `aura_org_id` / `type=saas_subscription` |

The **designed** entitlement model is: Hub tells the integrator (Aura, or any app) that a subscription activated / past_due / canceled / suspended, with `metadata`. The integrator grants/revokes. That is the D5 “webhooks + public checkout first” contract (`docs-commerce.tsp`). Building a Chargebee entitlements service inside Hub would invert that.

`saas_subscription` type is a first-class metadata value (`CommerceCheckoutMetadata.TypeSaas`) so Aura can distinguish salon-Pro-on-Hub experiments from creator Commerce without mixing Paddle.

### 10. Customer portal self-serve (our portal page)

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-065 | Magic-link portal showing the customer’s subs + orders | **shipped** | Y | Y | Y | Both | `GET /{tenantSlug}/portal` |
| SL-066 | Cancel from portal | **partial** | Y | Y | Y | Partial | Works, but **immediate**; one UI promises period-end |
| SL-067 | Update payment method while healthy | **none** | Y | Y | Y | Later | Update-payment allowed only PAST_DUE / SUSPENDED |
| SL-068 | Update payment method in arrears | **shipped** | Y | Y | Y | Both | `POST /checkout/{id}/update-payment` |
| SL-069 | Invoice / receipt history in portal | **none** | Y | Y | Y | Later | `[MVP-HIDE]` on portal page; Billing signed PDFs exist off to the side |
| SL-070 | Self-serve plan switch | **none** | Y | Y | Y | Later | Depends on SL-042 |
| SL-071 | Request-magic-link by email from the portal | **none** | Y | Y | Y | Later | Spec drift; Communications can send a link, public API cannot request one |
| SL-072 | 24h HMAC token | **shipped** | P | P | P | Both | `MagicLinkTokenService` |
| SL-073 | Stripe-hosted Billing Portal from ops | **partial** | Y | — | — | Partial | Stripe only; does not mutate Hub status |
| SL-074 | Token mint on anonymous checkout poll | **killed** | — | — | — | Ours | Deliberately removed; do not put back |

**Arrears amount honesty:** `GET /checkout/{subId}/arrears` returns **catalog `p.Price`**, not “what they owe” (partial periods, failed retries, tax). Status is the subscription status. Fine for a single-price membership; wrong the moment we add quantity, coupons-on-renewal, or setup fees.

**Lifecycle email hard-code:** suspended → Payment Failed template replaces `{{renewal_link}}` with `https://portal.lazuar.com/checkout/update` (no tenant slug, no sub id). Dunning `FulfillmentRequested` path is better (`update-payment/{subId}`) but `{{renewal_link}}` is still the **untokenized** portal URL.

### 11. Import existing subscribers

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-075 | CSV / API import of in-flight subscribers | **none** | Y | Y | P | Later | No import route |
| SL-076 | Backdate start / paid-through | **partial** | Y | Y | P | Partial | Manual enroll `start_date` + `next_billing_date` overrides, one row at a time |
| SL-077 | Import with existing vault / PM | **none** | Y | Y | P | Later | Manual enroll is always reminder-only |
| SL-078 | Import without charging (proration none) | **partial** | Y | Y | P | Partial | COMPED / amount=0 + date overrides |
| SL-079 | CSV export | **shipped** | Y | Y | Y | Both | `GET /subscribers/export` |

Migration from Stripe/Chargebee/Paddle onto Hub is **not** a product. A creator moving 200 Telegram members off spreadsheet **is**. Today that is 200 clicks in `CreateSubscriberModal` (name, email, phone, product, method, amount, optional dates). No card import — they become reminder-only and will PAST_DUE unless ops record-payment each cycle or the member runs a new checkout to vault.

Export columns: id, name, email, phone, product_name, product_price, status, current_period_end, next_billing_date, created_at. No vault flags, no dunning, no metadata.

### 12. Offline / manual payment subscriptions

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-080 | Manual enroll (cash / bank / comped) | **shipped** | P | Y | P | Both | `POST /subscribers` + modal |
| SL-081 | Reminder-only flag (no vault) | **shipped** | N | P | N | Ours | `IsReminderOnly` |
| SL-082 | Record offline payment against a live sub | **shipped** | Y | Y | P | Both | `POST /subscribers/{id}/record-payment` |
| SL-083 | Mark checkout / quote paid offline | **shipped** | P | Y | P | Both | `POST /checkouts/{id}/mark-paid` — **now creates Sub/Order** |
| SL-084 | Send-invoice collection method (AR object) | **none** | Y | Y | Y | Later | No invoice aggregate; reminders are emails |
| SL-085 | Convert reminder-only → vaulted without losing the row | **partial** | Y | Y | Y | Partial | Update-payment / a new checkout that hits `HandleSubscriptionPaymentAsync` can `StoreVaultedToken` **if** metadata uses the existing sub id. A brand-new product checkout creates a **second** Subscription. |
| SL-086 | Custom payment link (not a sub) | **shipped** | Y | Y | Y | Both | Ad-hoc session; no entitlement |

This is the strongest differentiator versus Paddle (MoR automatic collection) and a real MY creator job (bank transfer + WhatsApp). Ops copy is honest: “This user will not have an auto-debit card on file. They will automatically be flagged as Reminder Only.”

`RecordSubscriberPayment` rejects `PENDING` and `CANCELED`, rejects `one_time` products, treats COMPED as 0, advances the clock, writes `CommerceTransactionLog`, optionally ledgers via `ManualSubscriberEnrolled` (the event name is now **overloaded** — it is also “manual payment on an existing sub”).

Offline mark-paid on a **product** session now creates the entitlement (old gap doc is stale). Custom sessions still do not.

### 13. Coupons, gift, student, lifetime

| ID | Feature | Ours | Stripe | CB | Paddle | V | Src |
|----|---------|------|:------:|:--:|:------:|---|-----|
| SL-087 | Percent / fixed coupon on first checkout | **shipped** | Y | Y | Y | Both | Reserve → confirm on paid / zero / offline |
| SL-088 | Coupon expiry + max uses + product allow-list | **shipped** | Y | Y | P | Both | `Coupon.Validate` |
| SL-089 | Coupon duration (repeating / forever on the sub) | **none** | Y | Y | P | Later | Not stored on Subscription; renewals full price |
| SL-090 | Per-customer / first-time-only coupon | **none** | Y | Y | P | Later | Global counters only |
| SL-091 | Promotion codes vs coupon objects | **none** | Y | Y | N | Never | One code = the coupon |
| SL-092 | Gift subscription (A pays, B receives) | **none** | N | Y | N | Later / Never | Chargebee-named; not ICP unless community SKUs return |
| SL-093 | Student verification / .edu automatic | **none** | N | N | N | Never | Coupon is enough if anyone asks |
| SL-094 | Lifetime SKU (pay once, status never due) | **none** | P | P | P | Later | `one_time` is an Order, not a forever ACTIVE sub |
| SL-095 | COMPED / complimentary access | **shipped** | P | Y | P | Both | Enroll path; still has NextBillingDate → will PAST_DUE |
| SL-096 | Abandoned checkout coupon release | **shipped** | Y | Y | Y | Both | `CheckoutSessionExpiryJob` |

**COMPED is not lifetime.** Manual enroll on a recurring product still sets `nextBillingDate` to +1 interval (or the override). When that date hits, billing engine marks PAST_DUE because there is no vault. Ops must record-payment / re-COMPED or the member enters dunning and can be canceled/suspended. A true lifetime SKU would be: interval `lifetime` | `none`, `NextBillingDate = null`, billing engine skip, status stays ACTIVE until admin cancel.

**Gift:** no gifter/giftee, no scheduled start, no gift code. A merchant can COMPED the giftee and take payment as a custom checkout — two objects, no link.

**Student:** no identity check. Create a `STUDENT50` percent coupon.

### Cross-cutting holes that affect several rows

| Hole | Hits | Notes |
|------|------|-------|
| No amount snapshot on Subscription | SL-003, SL-011, SL-047, SL-049 | Every charge re-reads Product |
| Stringly-typed status | SL-012, SL-036 | `TRIALING` / `PAUSED` / `NON_RENEWING` cannot exist without a migration |
| `subscription.updated` forbidden | SL-042, SL-050 | Plan/qty changes would need new event types or overloading activated |
| Webhook `amount` is catalog price | SL-003, SL-055 | `CommerceWebhookPayload` uses `product.Price` |
| Subscriber list loads entire org | ops scale | Search/filter not SQL |
| Dual portal (Hub vs Stripe) | SL-066, SL-073 | Two cancel semantics |
| Price edit = silent fleet change | SL-011 | No grandfather |

---

## Tracker IDs

Promote these into `00-checklist-tracker.md` as family **`SL`** (Subscriptions lifecycle). Do not invent a second prefix. If a later sibling file (dunning, coupons, portal) needs a row that is already here, **reuse the ID**.

`job_class`: table-stakes · differentiator · later-nice · trap · hygiene.  
`wave` is a **suggestion** for the Lazuar Pay program, not a ship date.

- **W1** — honesty / correctness of what we already sell (cancel copy, PWYW charge, COMPED due, price snapshot).
- **W2** — self-serve completeness (period-end cancel, healthy PM update, magic-link request, quantity persist).
- **W3** — growth / migration (CSV import, trial, setup fee, coupon duration).
- **W4** — billing-OS envy (tiers, meters, schedules, entitlements service). Default **Later** or **Never**.

| ID | Feature | ours_depth | V | W | P | job_class | Why implement / why not |
|----|---------|------------|---|--:|--:|-----------|-------------------------|
| SL-001 | Flat recurring price | shipped | Both | — | — | table-stakes | Already the product |
| SL-002 | One-time product | shipped | Both | — | — | table-stakes | Already the product |
| SL-003 | Per-unit priced plan | partial | Partial | 2 | 2 | later-nice | Only if a merchant sells seats |
| SL-004 | Graduated tiers | none | Later | 4 | 3 | later-nice | Chargebee/Stripe OS; not creator MVP |
| SL-005 | Volume tiers | none | Later | 4 | 3 | later-nice | Same |
| SL-006 | Stairstep | none | Later | 4 | 3 | later-nice | Same |
| SL-007 | PWYW actually charged | partial | Partial | 1 | 0 | hygiene | UI lies today; either wire `custom_amount` ≥ `MinimumPrice` or remove the control |
| SL-008 | Multi-price per product | none | Later | 3 | 2 | later-nice | Workaround: two products |
| SL-009 | Weekly / custom interval | none | Later | 3 | 3 | later-nice | `AddMonths` collapse is a footgun if we ever accept `wk` |
| SL-010 | Add-on items | none | Later | 4 | 3 | later-nice | Integrator can sell a second product |
| SL-011 | Grandfather / snapshot price | none | Later | 1 | 1 | table-stakes | Price edits currently rebill the fleet |
| SL-012 | Carded free trial | none | Later | 3 | 1 | table-stakes | Memberships ask; needs `TRIALING` + trial_end job |
| SL-013 | No-card trial | none | Later | 3 | 2 | later-nice | Overlaps COMPED |
| SL-014 | Paid intro / Trial Offer | none | Never | — | — | trap | Stripe 2026 API envy |
| SL-015 | Trial-end behavior | none | Later | 3 | 2 | later-nice | Depends on SL-012 |
| SL-016 | Setup fee | none | Later | 3 | 2 | later-nice | Join + monthly is a real gym/club ask |
| SL-017 | Free first period via $0 / 100% coupon | partial | Partial | 1 | 1 | hygiene | After free period, engine PAST_DUEs — document or add trial |
| SL-018 | COMPED enroll | shipped | Both | — | — | table-stakes | Keep |
| SL-019 | Renewal worker | shipped | Both | — | — | table-stakes | Keep SKIP LOCKED |
| SL-020 | Vault auto-debit | shipped | Both | — | — | table-stakes | Requires Stripe/CHIP, not Billplz |
| SL-021 | Charge attempt cap | shipped | Both | — | — | hygiene | Max 4 is fine |
| SL-022 | Reminder-only due | shipped | Ours | — | — | differentiator | Protect this; it is the MY cash path |
| SL-023 | Billing cycle anchor | none | Later | 3 | 2 | later-nice | “Bill on the 1st” |
| SL-024 | Calendar billing | none | Later | 4 | 3 | later-nice | Chargebee specialty |
| SL-025 | Timezone | none | Later | 3 | 2 | later-nice | MYT vs UTC surprises annual renewals |
| SL-026 | Period advance after paid | shipped | Both | — | — | table-stakes | Keep |
| SL-027 | Fail → PAST_DUE | shipped | Both | — | — | table-stakes | Implemented 2026-08 |
| SL-028 | Pre-renewal reminders | partial | Partial | 2 | 2 | table-stakes | Wire orphan templates or delete them |
| SL-029 | Admin immediate cancel | shipped | Both | — | — | table-stakes | Keep |
| SL-030 | Portal immediate cancel | shipped | Both | — | — | table-stakes | Keep as *a* mode |
| SL-031 | Cancel at period end | none | Later | 2 | 0 | table-stakes | Paddle/Stripe default; our UI already pretends |
| SL-032 | Undo scheduled cancel | none | Later | 2 | 1 | table-stakes | Pairs with SL-031 |
| SL-033 | Access until period end | none | Later | 2 | 0 | table-stakes | Requires NON_RENEWING or cancel_at + billing skip |
| SL-034 | Reactivate canceled | none | Later | 3 | 3 | later-nice | Paddle refuses; we can enroll again |
| SL-035 | Anonymize cancels | shipped | Both | — | — | hygiene | PDPA |
| SL-036 | Pause subscription | none | Later | 3 | 2 | later-nice | Distinct from dunning pause |
| SL-037 | Scheduled pause | none | Later | 3 | 3 | later-nice | Paddle-shaped |
| SL-038 | Resume from holiday pause | none | Later | 3 | 2 | later-nice | — |
| SL-039 | Pause dunning | shipped | Both | — | — | table-stakes | Keep; rename in UI so ops don’t think billing stopped |
| SL-040 | Suspend for non-pay | shipped | Both | — | — | table-stakes | Keep typed event |
| SL-041 | Resume from suspend on pay | shipped | Both | — | — | table-stakes | Keep |
| SL-042 | Change plan | none | Later | 3 | 1 | table-stakes | Without this, “upgrade” is cancel+rebuy |
| SL-043 | Prorate now | none | Later | 3 | 2 | later-nice | Depends on SL-042 + invoice |
| SL-044 | Prorate next invoice | none | Later | 3 | 2 | later-nice | Same |
| SL-045 | Change at next cycle | none | Later | 3 | 1 | table-stakes | Cheaper than full proration |
| SL-046 | Schedules / ramps | none | Never | — | — | trap | Billing-OS |
| SL-047 | Per-sub price override | none | Later | 3 | 2 | later-nice | Sales-negotiated |
| SL-048 | Upcoming invoice preview | none | Later | 3 | 3 | later-nice | — |
| SL-049 | Qty on first charge | partial | Partial | 2 | 2 | later-nice | Finish or remove API field |
| SL-050 | Qty on subscription | none | Later | 2 | 2 | later-nice | Seats |
| SL-051 | Qty on renewal | none | Later | 2 | 2 | later-nice | Depends on SL-050 |
| SL-052 | Self-serve qty | none | Later | 3 | 3 | later-nice | — |
| SL-053 | Qty stepper UI | none | Later | 2 | 3 | hygiene | Dead state in CheckoutView |
| SL-054 | Ad-hoc line qty | shipped | Both | — | — | table-stakes | Quotes |
| SL-055 | Metered usage | none | Later | 4 | 3 | later-nice | Do not fake with wallet |
| SL-056 | Licensed + overage | none | Later | 4 | 3 | later-nice | — |
| SL-057 | Customer credit burndown | none | N/A | — | — | n/a | — |
| SL-058 | Tenant utility wallet | shipped | N/A | — | — | n/a | Different module; do not score as usage |
| SL-059 | Hybrid invoice | none | Later | 4 | 3 | later-nice | No Commerce invoice object |
| SL-060 | Seats | none | Later | 3 | 2 | later-nice | = quantity + integrator enforcement |
| SL-061 | Entitlements API | none | Later | 4 | 3 | trap | Webhooks are the v1 entitlements API |
| SL-062 | Lifecycle webhooks | shipped | Both | — | — | table-stakes | Frozen names; no `subscription.updated` |
| SL-063 | Seat assignment UI | none | Never | — | — | trap | Integrator’s app |
| SL-064 | Metadata pass-through | shipped | Both | — | — | table-stakes | Keep |
| SL-065 | Portal list | shipped | Both | — | — | table-stakes | Keep |
| SL-066 | Portal cancel honesty | partial | Partial | 1 | 0 | hygiene | Fix copy or implement SL-031 |
| SL-067 | Healthy PM update | none | Later | 2 | 1 | table-stakes | Competitors’ portal always has this |
| SL-068 | Arrears PM update | shipped | Both | — | — | table-stakes | Keep |
| SL-069 | Portal invoices | none | Later | 2 | 2 | later-nice | Un-hide Billing links carefully |
| SL-070 | Portal plan switch | none | Later | 3 | 2 | later-nice | Depends on SL-042 |
| SL-071 | Request magic link | none | Later | 2 | 1 | table-stakes | Portal dead-ends without a token |
| SL-072 | HMAC 24h token | shipped | Both | — | — | table-stakes | Keep; don’t mint on poll |
| SL-073 | Stripe Billing Portal from ops | partial | Partial | 2 | 3 | later-nice | Document as Stripe-only; never imply Hub cancel |
| SL-074 | No token on status poll | killed | Ours | — | — | hygiene | Do not regress |
| SL-075 | Bulk import | none | Later | 3 | 0 | table-stakes | Switching cost off spreadsheet |
| SL-076 | Backdate one row | partial | Partial | 3 | 1 | table-stakes | Exists on manual enroll |
| SL-077 | Import with vault | none | Later | 4 | 3 | later-nice | PM migration is a payments program |
| SL-078 | Import without charge | partial | Partial | 3 | 1 | table-stakes | COMPED + dates |
| SL-079 | CSV export | shipped | Both | — | — | table-stakes | Keep |
| SL-080 | Manual enroll | shipped | Both | — | — | table-stakes | Differentiator vs Paddle |
| SL-081 | Reminder-only | shipped | Ours | — | — | differentiator | Keep |
| SL-082 | Record payment | shipped | Both | — | — | table-stakes | Keep |
| SL-083 | Mark session paid | shipped | Both | — | — | table-stakes | Product path now entitles |
| SL-084 | Send-invoice AR | none | Later | 4 | 3 | later-nice | Ledger is in Billing, not Commerce invoices |
| SL-085 | Vault onto existing row | partial | Partial | 2 | 1 | table-stakes | Prevents duplicate Subscriptions |
| SL-086 | Custom payment link | shipped | Both | — | — | table-stakes | Not a sub |
| SL-087 | First-checkout coupon | shipped | Both | — | — | table-stakes | Confirm/release now implemented |
| SL-088 | Coupon constraints | shipped | Both | — | — | table-stakes | Keep |
| SL-089 | Repeating coupon | none | Later | 3 | 2 | later-nice | Attach coupon id + remaining cycles |
| SL-090 | Per-customer coupon | none | Later | 3 | 3 | later-nice | — |
| SL-091 | Promo-code object split | none | Never | — | — | trap | One code is enough |
| SL-092 | Gift subscriptions | none | Later | 4 | 3 | later-nice | Chargebee-only named feature |
| SL-093 | Student ID | none | Never | — | — | trap | Use a coupon |
| SL-094 | Lifetime SKU | none | Later | 3 | 2 | later-nice | Needs `NextBillingDate` null + engine skip |
| SL-095 | COMPED ≠ lifetime | shipped | Both | 1 | 1 | hygiene | Document; COMPED still comes due |
| SL-096 | Expire checkout + release coupon | shipped | Both | — | — | hygiene | Keep the 5-minute job |

### Suggested first honesty pack (not a commitment)

If the living tracker needs a default sequence **inside this domain only**:

1. **SL-066 + SL-031/033** — stop lying about cancel. Either implement cancel-at-period-end (billing engine skip when `CancelAt > now` or status `NON_RENEWING`) or change every portal string to “access ends now” and emit canceled immediately (today’s truth).
2. **SL-007** — PWYW: persist and charge `custom_amount` clamped to `MinimumPrice`, or delete the input.
3. **SL-011** — snapshot `UnitAmount` + `Currency` + `Interval` on Subscription at activate; renewals use the snapshot unless an explicit “follow catalog” flag is set.
4. **SL-017 + SL-095** — COMPED / 100% coupon must not surprise-PAST_DUE; either null `NextBillingDate` for complimentary/lifetime or convert them to `TRIALING`/`COMPED` with no engine claim.
5. **SL-039 UI rename** — “Pause recovery emails” ≠ “Pause subscription.”
6. **SL-071** — public request-magic-link so the portal empty state is not a dead end.
7. **SL-075** — CSV import with the same columns as export + optional `next_billing_date` / `status` / `is_reminder_only`.
8. **SL-067 + SL-085** — healthy card update that writes tokens onto the **existing** row.
9. **SL-042 + SL-045** — change plan at next cycle (no proration) before building proration math.
10. **SL-012** — only after the status enum is real.

### Traps (do not promote to a wave because Stripe has them)

| Trap | Why |
|------|-----|
| Become Stripe Billing | 15+ pricing models, meters, schedules, entitlements service — different company |
| Become Chargebee | Gift subs + millisecond proration + site timezone billing OS + 0.75% take |
| Become Paddle MoR | Contradicts Hub BYOK product line (`guide/product-lines.md`); breaks LHDN seller-of-record |
| Emit `subscription.updated` | Frozen P09 contract; Aura claim key assumes the five named types |
| Use `TenantCreditBalance` as customer usage | Wrong aggregate, wrong concurrency story, LHDN already double-charges |
| Mint portal tokens on checkout status poll | Explicitly killed; anonymous GET would be a login oracle |
| Pause dunning as “pause plan” | Billing engine will still debit |
| Delete reminder-only to “match Stripe” | That is the MY bank-transfer product |

### File map (absolute)

| Concern | Path |
|---------|------|
| Subscription aggregate | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` |
| Product / plan | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` |
| Coupon | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Coupon.cs` |
| Billing engine | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` |
| Dunning engine | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` |
| Session expiry | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/CheckoutSessionExpiryJob.cs` |
| Initiate checkout | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` |
| Portal cancel | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/CancelPortalSubscriptionCommandHandler.cs` |
| Admin cancel | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/CancelAdminSubscriptionCommandHandler.cs` |
| Record payment | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/RecordSubscriberPaymentCommandHandler.cs` |
| Manual enroll | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/CreateManualSubscriberCommandHandler.cs` |
| Offline mark-paid | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` |
| Payment completed | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.cs` |
| Payment failed | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` |
| Public portal API | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs` |
| Portal query | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Portal.cs` |
| Portal page | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` |
| Lying cancel copy | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/community/components/CommunityPortalView.tsx` |
| TypeSpec public | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/public-routes.tsp` |
| Webhook freeze | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/webhooks.tsp` |
| Product line lock | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/product-lines.md` |
| This analysis | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/11-subscriptions-lifecycle.md` |

---

**Bottom line.** Lazuar Pay Commerce is a **real** subscription engine for a **narrow** job: one product, one MYR price, monthly or yearly, hosted first charge, hourly renewals on a vaulted token, reminder-only for cash/bank, dunning that can cancel or suspend, a magic-link portal that lists and **immediately** cancels, webhooks that tell the integrator to grant or revoke. That job is closer to “Gumroad / creator memberships + MY offline collection” than to Stripe Billing.

It is **not** a billing OS. The holes that actually hurt the current ICP are honesty holes (PWYW, cancel copy, COMPED/free coming due, catalog price edits rebilling everyone) and a missing **cancel-at-period-end** / **import** / **healthy card update** trio. Graduated/volume/stairstep, meters, gift subscriptions, student ID, and entitlements-as-a-service are competitor features to record, not to chase.