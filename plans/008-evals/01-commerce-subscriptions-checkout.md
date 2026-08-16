# 01 — Commerce: checkout, subscriptions, billing job, portal lifecycle

**Date:** 16 August 2026  
**Branch:** `feat/007-waves-1-4-implement` (`4624070` — `feat: Wave 4 wrap rails, receipt honesty, drop WhatsApp/Xero claims`)  
**Product slice:** Lazuar Pay Commerce (hosted checkout + subscriptions + billing engine + buyer portal)  
**Code read:** `apps/lazuar-api/Modules/Commerce/**` and `apps/lazuar-portal/**` as they sit on this branch. Payments adapters are cited only where Commerce calls them.

This is the uncondensed evidence for [008-evals/README.md](./README.md) report 01. It is **not** a rewrite of `plans/007-feats`. The 007 tracker and Wave `*-done.md` notes are historical claims. Where they disagree with the code, the code wins.

---

## Scope lock

This report covers **only**:

- Commerce aggregates `Product`, `ProductPrice`, `CheckoutSession`, `Subscription`, `Order`, `Coupon`, `DunningCampaign`, plus Wave 3 columns on those tables.
- First hop (portal checkout form) and second hop (gateway URL mint).
- `InitiateCheckout`, zero-amount bypass, offline mark-paid, manual enroll.
- Quantity, trial (`TRIALING`), TIN / company, branding, and wallets **on hop 1**.
- `BillingEngineJob` (claim, collection pause, cancel-at-period-end, pending plan/qty, trial convert, off-session vs mint).
- Dunning (pre-dunning + PAST_DUE + reminder-only + arrears page).
- `PlanChangePolicy` (next-renewal-only).
- Buyer portal: cancel, keep, plan change, documents, update-payment.
- MRR / ARR stats.
- What is honestly sellable vs a lie.
- P0 / P1 bugs that are in this slice today.
- Tracker cells in `plans/007-feats/00-checklist-tracker.md` that are stale versus this code.

Out of scope (other 008 reports): adapter internals, ledger/refunds/disputes as Billing-owned money, LHDN XML, One identity, WhatsApp transport, TypeSpec honesty, ops chrome except where it proves a Commerce API exists.

Skepticism rule: a Wave `*-done.md` that says “tracker can move N → Y” is **not** evidence that the tracker moved, and is **not** evidence the feature is sellable. The handler, the claim SQL, the portal button, and the test that pins the failure mode are evidence.

---

## Current files table

Paths are under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/` unless noted.

### Domain (the truth the job is allowed to write)

| File | What it owns |
|------|----------------|
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs` | Catalog: price, interval, SST, trial days, prices collection, checkout config, archive |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ProductPrice.cs` | Monthly / yearly / one-time price rows (`mo` / `yr` / `one_time`) |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/CheckoutSession.cs` | OPEN/COMPLETED/EXPIRED session: qty, price id, metadata, quote number, due date, idempotency |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs` | All Wave 3 subscription fields + dunning + cancel-at-period-end + vault |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Order.cs` | One-time entitlement (`PENDING` / `COMPLETED` / `REFUNDED`) |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Coupon.cs` | PERCENTAGE / FIXED, reserve / confirm / release |
| `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` | Campaign + steps + recovered/churn counters |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/DunningStep.cs` | DayOffset + action + copy |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` | Per-cycle off-session attempts |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/ReminderDispatchLog.cs` | Pre-dunning / PAST_DUE step receipts |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/InvoiceReminderDispatchLog.cs` | Quote AR reminder receipts |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` | Ops cash read-model |
| `apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceDispute.cs` | GMV dispute row (does **not** flip `HasOpenDispute`) |
| `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/CheckoutConfiguration.cs` | Requires address / tax id / phone |
| `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/AdHocLineItem.cs` | Custom quote lines |
| `apps/lazuar-api/Modules/Commerce/Domain/ValueObjects/DunningCampaignSnapshot.cs` | Frozen campaign JSON |
| `apps/lazuar-api/Modules/Commerce/Domain/DeclineClassifier.cs` | Stripe hard vs soft codes |
| `apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs` | Max 4 attempts per cycle |
| `apps/lazuar-api/Modules/Commerce/Domain/DunningCampaignMatcher.cs` | Campaign pick |

### Application (policies the HTTP layer is supposed to obey)

| File | What it owns |
|------|----------------|
| `.../Application/Commands/InitiateCheckoutCommandHandler.cs` | Public product + custom hop-2 mint, trial $0 vault, zero-amount bypass, SST on hop 1 only |
| `.../Application/Commands/ProcessZeroAmountCheckoutCommand.cs` | Completes $0 / 100% coupon / non-vaulting trial **and hard-codes reminder-only** |
| `.../Application/Commands/MarkCheckoutAsPaidOfflineCommandHandler.cs` | Clerk mark-paid for product or ad-hoc session |
| `.../Application/Commands/CreateManualSubscriberCommandHandler.cs` | Ops enroll, always reminder-only, qty 1 |
| `.../Application/Commands/CreateProductCommandHandler.cs` / `UpdateProductCommandHandler.cs` | Catalog write-through + SST + trial + yearly price |
| `.../Application/Commands/CreateCustomCheckoutCommandHandler.cs` | Quotes: lines, due_at, terms, QT- number |
| `.../Application/Commands/ChangePlanCommandHandler.cs` | Admin plan + quantity + collection pause/resume |
| `.../Application/Commands/ChangePortalPlanCommandHandler.cs` | Magic-link plan change |
| `.../Application/Commands/CancelAdminSubscriptionCommandHandler.cs` / `CancelPortalSubscriptionCommandHandler.cs` | Shared cancel table |
| `.../Application/Commands/KeepAdminSubscriptionCommandHandler.cs` / `KeepPortalSubscriptionCommandHandler.cs` | Undo cancel-at-period-end |
| `.../Application/Commands/RecordSubscriberPaymentCommandHandler.cs` | Clerk cash against an existing sub |
| `.../Application/Commands/RequestPortalMagicLinkCommandHandler.cs` | Always-200 magic link |
| `.../Application/Commands/DunningCampaignCommandHandlers.cs` | Campaign CRUD + AUTO_CHARGE guard |
| `.../Application/Commands/DunningCampaignAutoChargeGuard.cs` | Blocks AUTO_CHARGE on all-Billplz / MANUAL targets |
| `.../Application/Commands/ManageSubscriberDunningCommandHandlers.cs` | Per-sub dunning pause/resume |
| `.../Application/PlanChangePolicy.cs` | Next-renewal-only, no prorate, no immediate |
| `.../Application/SubscriptionActivation.cs` | Trial vs paid first activate |
| `.../Application/SubscriptionBillingAmount.cs` | Unit × seats, interval advance — **no SST** |
| `.../Application/SubscriptionCancelDecision.cs` / `SubscriptionCancelApplier.cs` | Schedule vs immediate |
| `.../Application/CommerceCheckoutQuantity.cs` | 1–99 FIXED one-time / mo / yr |
| `.../Application/SstTaxMath.cs` | Exclusive SST if merchant has SST ID |
| `.../Application/CommerceMrr.cs` | ACTIVE + not paused + mo/yr snapshot |
| `.../Application/RenewalCheckoutIssuer.cs` | Mint hosted bill bound to existing sub id |
| `.../Application/CommerceWebhookPayload.cs` | `subscription.*` payload |
| `.../Application/PortalSubscriptionAccess.cs` | Token owns client, not just one row |
| `.../Application/OfflinePaymentMethods.cs` | `BANK_TRANSFER` / `CASH` / `COMPED` |

### Infrastructure (HTTP + workers + SQL)

| File | What it owns |
|------|----------------|
| `.../Infrastructure/Endpoints.cs` | `/admin/commerce` + `/public/commerce` composer |
| `.../Infrastructure/Endpoints/PublicEndpoints.cs` | Public composer |
| `.../Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | `POST /public/commerce/checkout` + status |
| `.../Infrastructure/Endpoints/PublicProductEndpoints.cs` | Hop-1 product GET + coupon validate |
| `.../Infrastructure/Endpoints/PublicArrearsEndpoints.cs` | **Unauthenticated** GUID arrears + update-payment |
| `.../Infrastructure/Endpoints/PublicPortalEndpoints.cs` | Magic-link portal, cancel, keep, plans, documents |
| `.../Infrastructure/Endpoints/PublicCustomCheckoutEndpoints.cs` | Public quote GET + draft PDF URL |
| `.../Infrastructure/Endpoints/SubscriberEndpoints.cs` | Ops subscribers + lifecycle verbs |
| `.../Infrastructure/Endpoints/StatsEndpoints.cs` | `GET /admin/commerce/stats` |
| `.../Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs` | M2M list/get/cancel (**immediate only**) |
| `.../Infrastructure/Endpoints/ProductEndpoints.cs` | Ops catalog |
| `.../Infrastructure/Endpoints/DunningCampaignEndpoints.cs` | Ops campaigns |
| `.../Infrastructure/Workers/BillingEngineJob.cs` | Hourly due-cycle claim |
| `.../Infrastructure/Workers/DunningEngineJob*.cs` | Pre-dunning + PAST_DUE |
| `.../Infrastructure/Workers/InvoiceReminderJob.cs` | Quote −3 / 0 / +3 |
| `.../Infrastructure/Workers/CheckoutSessionExpiryJob.cs` | Expire OPEN + release coupon |
| `.../Infrastructure/Dunning/PastDueDunningProcessor.cs` | Assign snapshot, AUTO_CHARGE, terminal |
| `.../Infrastructure/Dunning/DunningStepDispatcher.cs` | Email payload (catalog `Price`, not snapshot) |
| `.../Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler*.cs` | First paid + renewal recover + method update |
| `.../Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs` | Fail attempt → PAST_DUE + dunning |
| `.../Infrastructure/EventHandlers/CommerceGatewayDisputeCreatedHandler.cs` | Persist dispute, **never sets** `HasOpenDispute` |
| `.../Infrastructure/Services/CommerceQueryService.Products.cs` | Product DTO + `supports_off_session` |
| `.../Infrastructure/Services/CommerceQueryService.Subscribers.cs` | Ops subscriber list (Wave 3 columns) |
| `.../Infrastructure/Services/CommerceQueryService.Portal.cs` | Portal list (`NextBillingDate` aliased as period end) |
| `.../Infrastructure/Services/CommerceQueryService.Stats.cs` | MRR/ARR/churn/ARPU |
| `.../Infrastructure/Services/PortalDocumentQueryService.cs` | Signed receipt / tax invoice / proforma links |
| `.../Infrastructure/Migrations/20260820120000_AddWave3SubscriptionBilling.cs` | Wave 3 columns + `ProductPrices` |
| `.../Infrastructure/CommerceDbContext.cs` | EF map for every column above |

### Portal (what a buyer actually sees)

| File | What it owns |
|------|----------------|
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | Hop 1 page: product GET + branding name |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | Header logo/name, EN\|BM |
| `apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` | `--brand` CSS variable |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | Interval toggle, qty, trial $0 display, coupon |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | Identity + TIN + address + branded CTA |
| `apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Stepper, trial copy, reminder-only warning |
| `apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx` | Logo header + locale |
| `apps/lazuar-portal/src/modules/core/lib/branding.ts` | `GET /public/one/{slug}/branding` |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | Cancel / keep / plan / documents / update-payment link |
| `apps/lazuar-portal/src/modules/portal/components/PortalPlanChange.tsx` | “No charge today” plan picker |
| `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Public GUID interstitial |
| `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | Custom quote pay page |

### Tests that pin this slice (not a compliment — a map)

Under `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/`:

`BillingEngineJobTests`, `DunningEngineJobTests`, `InvoiceReminderJobTests`, `SubscriptionTrialTests`, `SubscriptionCancelAtPeriodEndTests`, `SubscriptionCollectionPauseTests`, `SubscriptionRecoveryTests`, `ChangePlanCommandHandlerTests`, `ChangePortalPlanCommandHandlerTests`, `PlanChangePolicyTests`, `CommerceCheckoutQuantityTests`, `CommerceMrrTests`, `SstTaxMathTests`, `CommerceProductCompletenessTests` (includes `ProcessZeroAmount_Recurring_ActivatesReminderOnly`), `CreateManualSubscriberCommandHandlerTests`, `RecordSubscriberPaymentCommandHandlerTests`, `PublicArrearsEndpointsBoundaryTests`, `CheckoutB2bIdentityTests`, `DeclineClassifierTests`, `DunningCampaignCommandHandlerTests`, `DunningCampaignSnapshotTests`, `GatewayPaymentFailedIntegrationEventHandlerTests`, `GatewayPaymentCompletedRecoveryMetricsTests`, `CommerceGatewayDisputeCreatedHandlerTests`, `RequestPortalMagicLinkCommandHandlerTests`, `CommerceHonestyDtoTests`.

Portal: `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` (EN/BM key parity only).

---

## What the code actually does

### 1. Product aggregate (catalog the merchant edits)

`Product` is still one row per sellable thing. The default commercial fields are `Price`, `PricingModel` (`FIXED` default), `MinimumPrice`, `Currency`, `Interval` (`one_time` / `mo` / `yr`), `GatewayName`, `IsActive`, `CheckoutConfiguration` (address / tax id / phone), `FulfillmentTargets` jsonb, SST, trial, and a child collection of `ProductPrice`.

```15:36:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs
    public decimal Price { get; private set; }
    public string PricingModel { get; private set; }
    public decimal MinimumPrice { get; private set; }
    public string Currency { get; private set; }
    public string Interval { get; private set; }
    public bool IsActive { get; private set; }
    public string GatewayName { get; private set; }
    public CheckoutConfiguration CheckoutConfiguration { get; private set; }

    /// <summary>MyInvois tax type: 06 (N/A) or 02 (Service Tax).</summary>
    public string SstTaxType { get; private set; } = "06";

    public decimal SstRatePercent { get; private set; }

    public int TrialDays { get; private set; }
    // ...
    public IReadOnlyCollection<ProductPrice> Prices => _prices.AsReadOnly();
```

Wave 3 additions that actually persist:

| Field | Constraint in code | Who writes it |
|-------|--------------------|---------------|
| `SstTaxType` / `SstRatePercent` | Type `06` or `02`. `06` or `rate <= 0` forces 0. | `SetSst` from create/update product |
| `TrialDays` | 0–90. `> 0` illegal on `one_time`. | `SetTrialDays` |
| `Prices` | At most monthly + yearly (or one-time). Unique `(ProductId, Interval)`. | `UpsertPrice` / `SetYearlyPrice` / `SyncDefaultPrice` |

`Product.Price` + `Product.Interval` remain the **default write-through**. `UpdateDetails` calls `SyncDefaultPrice()`, which upserts the default `ProductPrice` to match `Interval`/`Price` and clears other defaults (`Product.cs` 201–216). A yearly add-on is `SetYearlyPrice` (`181–199`), rejected on one-time products.

Create/update handlers apply SST, trial, and yearly in that order (`CreateProductCommandHandler.cs` 49–51, `UpdateProductCommandHandler.cs` 57–63). Create **archives** the product if the org has no Resend config (`43–47`). Update **refuses to activate** without email (`34–40`). Checkout is also refused at initiate if email is missing (`InitiateCheckoutCommandHandler.cs` 54–58). That is a real product rule: no Resend, no live checkout, even for Stripe.

Public GET maps SST, trial, prices, checkout flags, and `supports_off_session` from the Payments capability matrix — **not** from a wallet list (`CommerceQueryService.Products.cs` 120–144):

```131:143:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Products.cs
            Gateway_name = raw.GatewayName,
            Supports_off_session = Modules.Payments.Contracts.PaymentGatewayCapabilities.SupportsOffSession(raw.GatewayName),
            Checkout_configuration = new CheckoutConfigurationDto
            {
                Requires_address = raw.RequiresAddress,
                Requires_phone = raw.RequiresPhone,
                Requires_tax_id = raw.RequiresTaxId
            },
            // ...
            Trial_days = raw.TrialDays,
            Prices = prices?[raw.Id].ToList() ?? new List<ProductPriceDto>()
```

`PaymentGatewayCapabilities.SupportsOffSession` is **only** `STRIPE` or `CHIP` (`PaymentGatewayCapabilities.cs` 10–14). Billplz, Razorpay, Xendit, blank, OFFLINE are reminder-only. E-mandate is hard-`false` for every name (`45–49`). Hosted wallets are a Payments flag for Xendit/CHIP (`32–42`); Commerce never puts those flags on the hop-1 DTO.

What Product is **not**: usage meters, add-ons, setup fees, multiple currencies, per-price tax, per-price trial, per-price gateway. LP-061 / LP-062 remain honest `N`.

### 2. CheckoutSession aggregate (the first-hop receipt)

Two constructors: product checkout and custom (ad-hoc) quote.

Product session (`CheckoutSession.cs` 53–75): `ProductId`, optional `CouponId`, `Quantity` (min 1), optional `PriceId`, 24h expiry set by the handler, status `OPEN`. Custom session (`100–132`): `ProductId = null`, `Quantity` forced to 1, line items in jsonb, optional `GatewayName`, `IsB2bRequired`.

Wave 3 / Wave 2 columns that are real:

| Field | Meaning |
|-------|---------|
| `Quantity` | Buyer N for **product** checkout. Custom stays 1; line qty lives in jsonb. |
| `PriceId` | Which `ProductPrice` the buyer picked. |
| `DocumentNumber` | Sequential `QT-yyyy-#####`, assigned once (`AssignDocumentNumber` no-ops if already set). |
| `DueAt` | Quote AR date. `SetDueAt` raises `ExpiresAt` to `DueAt + 14d` if the link would die first (`175–187`). |
| `IdempotencyKey` + `RequestFingerprint` | Replay of the same key + payload returns the stored gateway URL. |
| `GatewayCheckoutUrl` | Last minted hop-2 URL. |
| `MetadataJson` | Copied onto the Subscription at first activate. |
| `IsB2bRequired` | Custom quotes only at construct time. Product sessions infer B2B from a non-empty TIN at initiate and stamp metadata. |

Statuses: `OPEN` → `COMPLETED` (`Complete`) or `EXPIRED` (`Expire`). There is no `PAST_DUE` on a session. `InvoiceReminderJob` emails OPEN custom sessions; it does not change status (`InvoiceReminderJob.cs` 18–20, 67–70).

`CheckoutSessionExpiryJob` every 5 minutes expires `OPEN` rows past `ExpiresAt` and `ReleaseReservation()` on the coupon (`CheckoutSessionExpiryJob.cs` 56–88). A buyer who sits on hop 2 past 24h loses the reservation. Quotes with a due date keep the link alive at least 14 days after due.

### 3. Subscription aggregate (all Wave 3 fields, no marketing)

Constructor (`Subscription.cs` 69–85): `PENDING`, `IsReminderOnly = false`, `CancelAtPeriodEnd = false`, `Quantity = 1`, `UnitAmount = 0`, `HasOpenDispute = false`. Nothing else is set until an activation path runs.

**Statuses the code writes:** `PENDING`, `ACTIVE`, `TRIALING`, `PAST_DUE`, `SUSPENDED`, `CANCELED`. There is **no** `PAUSED`. Collection pause is a date flag on an otherwise `ACTIVE` row (`171–199`).

Wave 3 columns from migration `20260820120000_AddWave3SubscriptionBilling.cs` 14–81, mapped in `CommerceDbContext.cs` 170–185:

| Column | Domain API | What it actually does |
|--------|------------|------------------------|
| `Quantity` | `SetSnapshot` / `ScheduleQuantity` / `ApplyPendingQuantity` | Seats 1–99. Live qty used for charge line. Pending applied on the due tick **before** charge. |
| `PendingQuantity` | `ScheduleQuantity` | Cleared if new qty == current. |
| `PendingProductId` | `SchedulePlanChange` / `ApplyPendingPlanChange` | Swap catalog product on the due tick. Same-id clears. |
| `PriceId` | `SetPriceId` | Snapshot of which catalog price was sold. |
| `UnitAmount` | `SetSnapshot` / `RefreshSnapshot` | Per-seat amount used by billing. `RefreshSnapshot` on successful paid renewal from **catalog**, not from the charged amount. |
| `BillingInterval` | `SetBillingInterval` | `mo` / `yr`. Billing prefers this over `product.Interval`. |
| `TrialEndsAt` | `ActivateTrial` | Clock = `NextBillingDate` = `CurrentPeriodEnd` = trial end. |
| `CollectionPausedUntil` | `PauseCollection` / `ResumeCollection` | Only from `ACTIVE`. Resume may push `NextBillingDate` forward if it is in the past. |
| `HasOpenDispute` | **no setter** | Default `false`. Dispute handler never writes it. Dead column. |
| `CancelAtPeriodEnd` | `ScheduleCancelAtPeriodEnd` / `ClearScheduledCancel` / `Cancel` | Domain allows `ACTIVE` or `TRIALING` with a **future** `NextBillingDate`. |
| `IsReminderOnly` | `Activate` / `ActivateTrial` / `StoreVaultedToken` | Vault store **clears** the flag (`273–278`). |
| `CurrentRenewalCheckoutUrl` + `ForDate` | `SetCurrentRenewalCheckout` | Minted Billplz/CHIP/Stripe hosted URL for this cycle. |
| `DunningCampaignSnapshotJson` | `AssignDunningCampaign` | Frozen plan. Engine must not re-read live steps. |
| `MetadataJson` | `SetMetadataJson` | Empty no-op so renewals keep first-checkout map. |

`Activate` (`87–116`) is the most important lie in the type names: for a healthy row it sets `CurrentPeriodEnd = currentPeriodEnd` (callers pass **now**) and `NextBillingDate = next`. For `PAST_DUE` / `SUSPENDED` it **refuses to advance dates**. Recovery must call `RecoverFromPayment` (`309–319`), which always advances, clears dunning, clears scheduled cancel, clears the minted URL.

`ActivateTrial` (`118–134`) requires a future end, sets status `TRIALING`, parks the first charge at `endsAt`.

Portal SQL **aliases** `NextBillingDate AS CurrentPeriodEnd` (`CommerceQueryService.Portal.cs` 41–44). Ops SQL returns both columns honestly (`CommerceQueryService.Subscribers.cs` 56). Webhook payload documents `current_period_end` as paid-through = `NextBillingDate` (`CommerceWebhookPayload.cs` 11–12, 72–84). If you sell “period end” you must say **next bill date**, not the poorly named `CurrentPeriodEnd` column (which is “activated at” after a paid hop).

### 4. InitiateCheckout — the only honest money door for a new buyer

`POST /public/commerce/checkout` (`PublicCheckoutEndpoints.cs` 20–88) binds `InitiateCheckoutCommand`. There is no tenant slug on the path; the handler looks up the workspace, then **requires Resend** (`InitiateCheckoutCommandHandler.cs` 48–58).

Idempotency (`60–88`, `261–283`): header `Idempotency-Key` + fingerprint of tenant, slug, email, coupon, qty, session id, interval, price id. Same key, different payload → `IDEMPOTENCY_CONFLICT` (HTTP 409). Same key, same payload, stored URL → replay.

Two shapes:

**A. Custom quote (`SessionId` set).** Loads the OPEN session, sums `UnitPrice * Quantity`, optionally resolves CRM with TIN if `IsB2bRequired`, mints `GenerateCheckoutSessionQuery` with `SetupFutureUsage: false`, quantity 1, amount = **line total** (not unit). Success URL is `/checkout/custom/success?sub_id={sessionId}` — `sub_id` is a **session** id. No Commerce `Subscription` is created for custom links (`145–163`).

**B. Product slug.** Resolve catalog, then:

1. `CommerceCheckoutQuantity.NormalizeOrThrow` — 1–99; N≠1 only for FIXED + `one_time|mo|yr` (`CommerceCheckoutQuantity.cs` 14–35).
2. `ResolveCheckoutPrice` — `price_id` wins, else `interval`, else default (`InitiateCheckoutCommandHandler.cs` 372–406).
3. Trial + one-time price is rejected (`175–178`).
4. `isTrial = TrialDays > 0 && product.Interval is mo|yr && resolved interval is mo|yr` (`180–181`). Coupon is **skipped** on trial (`215`).
5. `EnforceCheckoutConfiguration` — phone / TIN+id type+id value+company / full address (`408–446`).
6. CRM `ResolveClientProfileCommand` with TIN, id type/value, company, address (`198–210`).
7. Coupon lock + reserve on the **resolved unit**, not `product.Price` (`215–227`). (Zero-amount later re-discounts `product.Price`. That is a separate lie.)
8. SST: `SstTaxMath.Compute` only if Billing profile has an SST registration number (`230–238`). Exclusive tax, 2 dp away-from-zero.
9. Persist session with qty + `PriceId` + metadata (`is_b2b_required` if TIN present).
10. Money fork (`288–369`):
    - `lineNet == 0` **and** trial **and** Stripe/CHIP → mint **$0 setup-future** checkout (`type=trial`), return hop 2. Session stays OPEN until webhook.
    - `lineNet == 0` otherwise → `ProcessZeroAmountCheckoutCommand`, stamp success URL, `Is_zero_amount_bypass = true`.
    - else mint hop 2 with **unit gross** (net + SST) and `Quantity: N`. Comment at 349 is the contract: adapters multiply. `SetupFutureUsage` is true iff interval ≠ `one_time`.

Hop 1 does **not** send a buyer-typed PWYW amount. There is no field on `InitiateCheckoutCommand` for it. `OrderSummaryCard` lets the buyer type a number; `CheckoutForm` never posts it. The charge is catalog `Price`. W1-LP-014-done already admitted this (`CK-012 / LP-013`). Still true.

Hop 1 does **not** display SST. `OrderSummaryCard` totals `currentPrice` / coupon math. SST is added only in the handler after submit. A buyer on an 8% SST product sees RM 100 then pays RM 108 on Billplz/Stripe. That is a conversion lie, not a rounding bug.

### 5. Zero-amount, offline, manual enroll

#### ProcessZeroAmount

Used for: 100% coupon, $0 catalog price, **non-vaulting trial** (Billplz trial, or any trial when `SupportsOffSession` is false).

```57:99:apps/lazuar-api/Modules/Commerce/Application/Commands/ProcessZeroAmountCheckoutCommand.cs
        var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
        var unitAmount = chosen?.Amount ?? product.Price;
        var lineGross = unitAmount * quantity;
        var lineDiscount = unitDiscount * quantity;
        var isTrial = SubscriptionActivation.IsTrialOffer(product);
        var finalPrice = isTrial ? 0m : Math.Max(0, lineGross - lineDiscount);
        // ...
            var reminderOnly = !PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName);
            SubscriptionActivation.Start(
                subscription,
                product,
                quantity,
                unitAmount,
                reminderOnly: true,
```

The local `reminderOnly` is computed and **discarded**. The call always passes `reminderOnly: true`. A Stripe $0 plan or a Stripe 100% coupon therefore creates an `ACTIVE` (or `TRIALING`) row that **cannot auto-debit**, even though the product gateway can vault. The test suite **pins this as intended**:

```684:715:apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceProductCompletenessTests.cs
    public async Task ProcessZeroAmount_Recurring_ActivatesReminderOnly()
    {
        // product gatewayName: "STRIPE", price 0
        // ...
        created!.IsReminderOnly.Should().BeTrue();
        created.Status.Should().Be("ACTIVE");
        created.VaultedTokenId.Should().BeNull();
    }
```

Coupon discount inside this handler uses `coupon.CalculateDiscount(product.Price)` (`51`), not the chosen yearly amount. A 100% coupon on a yearly price whose default monthly is cheaper can fail the `finalPrice > 0` guard (`63–66`) and refuse the bypass. Inverse: a coupon sized to the default can zero a more expensive yearly line incorrectly. Either way the path is not price-row honest.

#### Offline mark-paid

`POST /admin/commerce/checkouts/{id}/mark-paid` (`Endpoints.cs` 58–65). Product path (`MarkCheckoutAsPaidOfflineCommandHandler.cs` 66–179): complete session; one-time → `Order`; recurring → `SubscriptionActivation.Start(..., reminderOnly: true)` with chosen price if `PriceId` matches, **else `product.Price`**. Discount again uses `product.Price` (`85`). Tx log `gatewayName: OFFLINE`, `recordedByName: MANUAL_OFFLINE`. Ledger event only if `totalAmount > 0`. Custom path: no subscription, session id stuffed into `ManualSubscriberEnrolledIntegrationEvent.SubscriptionId` as CRM correlation (`209–220`).

Offline recurring is **always reminder-only**. Marking a Stripe product paid in cash does not vault a card. Correct. It also never offers “collect a card later on this same sub” except via the public update-payment GUID.

#### Manual enroll

`POST /admin/commerce/subscribers` (`SubscriberEndpoints.cs` 49–86). Recurring `mo`/`yr` only (`CreateManualSubscriberCommandHandler.cs` 71–74). Methods `BANK_TRANSFER` / `CASH` / `COMPED` (`OfflinePaymentMethods.cs` 7–19). COMPED forces amount 0; anything else must be `> 0`. Duplicate active (client+product) rejected (`82–85`). Quantity is **hard 1**. Unit is **`product.Price`**, not a yearly row. If the product has `TrialDays > 0` **and** the clerk omitted `NextBillingDate`, it starts a trial (`91–94`); if they typed a next bill date, trial is skipped and the sub is `ACTIVE` reminder-only (`96–99`). Welcome email is the `SubscriptionActivated` event, optional (`134–143`).

This is sellable as “put last month’s bank transfer on the books.” It is not sellable as “import Stripe subscribers with vaults.”

### 6. Quantity, trial, TIN, branding, wallets on hop 1

#### Quantity

Hop 1 stepper is shown when `pricing_model === "FIXED"` **and** `product.interval` is `one_time|mo|yr` (`CheckoutView.tsx` 41, 55–93). That uses the **catalog default interval**, not the selected yearly toggle. A product whose default is `mo` and that also has a yearly price still shows the stepper (good). A product whose default is something else would not, even if a `mo` price exists (the domain no longer allows that).

API accepts N on FIXED recurring (`CommerceCheckoutQuantityTests.cs` 42–51). Session stores N. Paid hop 2 sends unit × N via adapter multiply. Renewals use `Subscription.Quantity` (`SubscriptionBillingAmount.Line`). Admin `POST /subscribers/{id}/quantity` schedules `PendingQuantity` (`ChangePlanCommandHandler.cs` 54–86). Portal has **no** seat stepper.

#### Trial (`TRIALING`)

`Product.TrialDays` 0–90. Hop 1: `currentPrice` becomes 0 when `trialDays > 0 && selectedInterval !== "one_time"` (`CheckoutView.tsx` 117). Summary prints `summary.trialThen` (`OrderSummaryCard.tsx` 143–147). Coupon UI still works on hop 1 but initiate **ignores** the coupon when `isTrial` (`InitiateCheckoutCommandHandler.cs` 215).

Vaulting gateways: $0 setup-future hop 2, `type=trial`. Webhook `HandleOpenCheckoutSessionAsync` then `SubscriptionActivation.Start` → `ActivateTrial` if `TrialDays > 0`, `reminderOnly: !hasVault` (`GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs` 87–98). If Stripe returns customer + token, the trial is **not** reminder-only.

Non-vaulting / zero-amount path: `ProcessZeroAmount` → trial with `reminderOnly: true` (see §5).

Billing **does not exclude** `TRIALING` from the claim SQL (`BillingEngineJob.cs` 129–137). A due trial is a due subscription. If vaulted and not reminder-only, attempt 1 off-session fires and status stays `TRIALING` until the payment webhook `Activate`s it to `ACTIVE`. If not vaulted, the job mints a hosted bill and marks `PAST_DUE` — the buyer never becomes `ACTIVE` first. That is conversion, not a grace period.

Cancel: domain `ScheduleCancelAtPeriodEnd` **allows** `TRIALING` (`Subscription.cs` 335–338; test `SubscriptionTrialTests.cs` 38–47). The HTTP/command table **does not**:

```22:26:apps/lazuar-api/Modules/Commerce/Application/SubscriptionCancelDecision.cs
        if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED"))
        {
            throw new InvalidOperationException(
                $"Subscription cannot be canceled from status '{subscription.Status}'.");
        }
```

Portal chrome only treats `status === "ACTIVE"` as cancelable (`portal/page.tsx` 73–74, 128–155). `TRIALING` gets a badge (`85–89`) and no buttons. Immediate cancel is also blocked. A trial buyer cannot leave. An admin cannot cancel a trial without a raw SQL update or waiting for the billing job after somehow setting the flag (and nothing in HTTP sets the flag on `TRIALING`).

#### TIN / company

Not `[MVP-HIDE]`. When `checkout_configuration.requires_tax_id`, hop 1 shows company, TIN, ID type (BRN/NRIC/PASSPORT/ARMY), ID value (`CheckoutForm.tsx` 198–253). Submit calls `validateTin` against MyInvois **before** `submitCheckout` (`96–110`). Initiate enforces the same fields (`426–434`). CRM stores company + TIN. Metadata `is_b2b_required=true` when TIN is present (`251–256`). W2-LP-022-done is accurate about collection. It is **not** a filed e-invoice. Tracker cell `LP-022 = B` is stale; this is at least **P**.

Address is a raw ISO-ish text grid, not a validated Malaysian state picker.

#### Branding

Workspace `name` + `logo_url` + `primary_color` from `GET /public/one/{slug}/branding` (`branding.ts` 10–20). Tenant layout sets `--brand` (`[tenantSlug]/layout.tsx` 13–15). Checkout header shows logo or name (`CheckoutI18n.tsx` 107–126). Pay CTA uses `backgroundColor: var(--brand, var(--foreground))` (`CheckoutForm.tsx` 360). Update-payment page also shows the mark (`update-payment/[subId]/page.tsx` 48–57). “Powered by Lazuar” remains. This is a cash-register skin, not a custom domain (LP-017 still N) and not PDF branding (LP-107). Tracker `LP-025 = P` is conservative; the sold claim “logo + colour on hosted checkout” is **Y**.

#### Wallets on hop 1

**None.** Grep of `apps/lazuar-portal` for wallet / DuitNow / GrabPay / Apple Pay in checkout yields only `supportsOffSession` used to pick reminder-only copy (`CheckoutView.tsx` 119, `OrderSummaryCard.tsx` 155–164). W1-LP-021-done is explicit: CSS/layout only; “Wallet QR still absent.” W4-LP-033-done: capabilities are wrap flags; hop 2 is still the processor page. Tracker `LP-021 = Y` is a **lie** if the row is read as “wallet QR on our page.” The feature title is `Mobile-first / wallet QR on our page`. Mobile-first CSS shipped. Wallet QR did not. Mark should be **P** (mobile) or split the row.

Apple Pay / Google Pay (LP-037) are whatever Stripe Checkout shows on hop 2. We do not render them.

### 7. BillingEngineJob — the hourly money loop

Hosted loop: `BackgroundWorkerOptions.BillingEngineInterval`, batch 50, one row per inner transaction (`BillingEngineJob.cs` 45–117).

#### Claim

Relational SQL (`120–144`):

```sql
SELECT * FROM commerce."Subscriptions"
WHERE "NextBillingDate" IS NOT NULL
  AND "NextBillingDate" <= NOW()
  AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
ORDER BY "NextBillingDate"
LIMIT 1
FOR UPDATE SKIP LOCKED;
```

Included: `ACTIVE`, `TRIALING`, and anything else we invent later.  
Excluded: `PENDING`, `PAST_DUE`, `SUSPENDED`, `CANCELED`.  
**Not excluded:** `CollectionPausedUntil`, `CancelAtPeriodEnd` (those are handled after claim), `IsReminderOnly`, `HasOpenDispute`.

In-memory test DB uses the same status filter (`146–163`).

#### ProcessOne (order matters)

1. Load product + prices. Missing product → add to `failedIds`, skip (`176–184`). Sibling still processed (`BillingEngineJobTests` 458–477).
2. `product.Interval == one_time` → skip, failedIds (`186–191`). A one-time “sub” with a due date is a zombie that will be reclaimed every tick until someone deletes it.
3. **`IsCollectionPaused(now)` → log and `return` without touching `NextBillingDate`** (`193–197`).
4. `CancelAtPeriodEnd` → `Cancel()` + `SubscriptionCanceledIntegrationEvent`, no charge, no mint, no PAST_DUE (`199–212`). Tests: vaulted flagged due cancels, reminder-only flagged due does not mint (`479–539`). Future flagged is not claimed (`542–561`).
5. `ApplyPendingPlanChange()` — if pending product missing, skip (`214–223`). Else snapshot **default** price of the **new** product (`226–232`): `product.Prices.FirstOrDefault(p => p.Interval == product.Interval) ?? DefaultPrice()`. Not “same interval as the subscription.” `PlanChangePolicy.GuardTargetProduct` already requires matching interval (`PlanChangePolicy.cs` 85–88), so this is consistent **if** nobody writes `PendingProductId` by hand.
6. `ApplyPendingQuantity()`.
7. `chargeAmount = SubscriptionBillingAmount.Line` = `(UnitAmount > 0 ? UnitAmount : product.Price) * max(1, Quantity)` — **no SST** (`SubscriptionBillingAmount.cs` 7–22).
8. Off-session iff Stripe/CHIP **and** not reminder-only **and** both vault ids present (`238–241`).
   - If yes: count `ChargeAttemptLog` for this `NextBillingDate.Date`. If 0, insert attempt 1 (`SourceBilling`) and publish `ExecuteOffSessionChargeIntegrationEvent`. **Do not** advance dates. **Do not** mark PAST_DUE. Return (`243–276`). A second tick with attempt 1 already present is a no-op (`227–252` in tests).
   - If no: mint `RenewalCheckoutIssuer` (quantity **1**, amount = **already multiplied line**), store URL+date, `MarkAsPastDue`, start PAST_DUE dunning, emit `subscription.past_due` (`279–325`). Mint failure rolls back; status stays ACTIVE for retry (`318–355` in tests). No CRM email → PAST_DUE **without** a URL (`287–291`).

`RenewalCheckoutIssuer` (`36–59`) success URL is `/{slug}/portal` (no token). Cancel URL is `/{slug}/update-payment/{sub.Id}` (the public GUID page). Metadata `type=commerce_subscription`. `SetupFutureUsage: true`. Amount is line, quantity 1 — **do not** also multiply in the adapter or you square seats.

Trial not due: not claimed (`564–580` in tests). Trial due + vault: same as ACTIVE vault (attempt 1, status remains `TRIALING` until webhook). Trial due + reminder-only: mint + PAST_DUE.

#### Collection-pause reclaim (P0)

`RunOnce_CollectionPaused_SkipsChargeAndKeepsActive` (`583–601`) proves the skip: status stays ACTIVE, `NextBillingDate` stays in the past, no off-session. It does **not** prove the next claim.

Because claim SQL does not exclude `CollectionPausedUntil`, a paused-and-due row is the **first** `ORDER BY NextBillingDate` candidate forever. The job claims it, returns, commits (no date change), and the next of the 50 slots can claim it again. A single paused-due sub can occupy the entire batch of 50. Other due work starves until the pause expires or someone resumes.

Dunning pre-dunning **does** exclude collection pause (`DunningEngineJob.Claim.cs` 107, 158). Billing does not. W3-LP-057-done said “does not roll `NextBillingDate`” as if that were a feature. Rolling is optional. **Leaving the row claimable** is the bug.

Resume handler (`ChangePlanCommandHandler.cs` 128–134`) pushes `NextBillingDate` to now+interval only if it is already in the past. That is the correct place to unstick the clock — if a human hits resume.

### 8. Off-session vs mint vs reminder-only

Physics, not copy:

| Condition at due tick | What happens |
|-----------------------|----------------|
| Stripe/CHIP + vault + `IsReminderOnly=false` | Attempt 1 off-session. Stay ACTIVE/TRIALING. Success webhook advances. Fail webhook → PAST_DUE + dunning AUTO_CHARGE 2–4. |
| Stripe/CHIP + vault + `IsReminderOnly=true` | **Mint + PAST_DUE.** Reminder-only wins. (Zero-amount Stripe lands here.) |
| Stripe/CHIP + no vault | Mint + PAST_DUE. |
| Billplz / Xendit / Razorpay / blank | Mint + PAST_DUE. Vault ids if present are ignored (`BillingEngineJobTests` 159–165: Billplz with junk vault still PAST_DUE). |
| `CancelAtPeriodEnd` | Cancel. No money. |
| Collection paused | No-op reclaim (above). |

`StoreVaultedToken` clears reminder-only (`Subscription.cs` 273–278`). Stripe/CHIP pay-again of a reminder-only sub **can** graduate to auto-debit (`CommerceProductCompletenessTests` `SubscriptionPayment_Stripe_MayVaultAndClearReminderOnly`, 740–758). Billplz pay-again must not (`719–737`). That split is honest.

Dunning AUTO_CHARGE uses the same capability + reminder-only + vault + max 4 + hard-decline skip (`PastDueDunningProcessor.cs` 107–188). Campaign create/update refuses AUTO_CHARGE if every targeted product is non-vaulting, or if methods are all `MANUAL` (`DunningCampaignAutoChargeGuard.cs` 15–55). Empty product list **allows** AUTO_CHARGE (org-wide campaign). A Billplz-only org can still save an org-wide AUTO_CHARGE campaign; the engine will skip at runtime and log (`144–152`).

Hard vs soft: static Stripe code table (`DeclineClassifier.cs` 15–31). A hard FAILED attempt in the cycle skips later AUTO_CHARGE but still consumes the DayOffset (`127–141`). EMAIL steps still send. NSF / unknown / generic `card_declined` are soft.

### 9. Dunning + reminder-only + arrears

`DunningEngineJob` hourly: load active campaigns, run pre-dunning batch, then PAST_DUE batch (`DunningEngineJob.cs` 62–87).

**Pre-dunning claim** (`DunningEngineJob.Claim.cs` 103–115): `ACTIVE`, not cancel-at-period-end, not collection-paused, `NextBillingDate` in `(now, now+14d]`. Matches campaign, fires `DayOffset < 0` comms steps once per `(DayOffset, TargetBillingDate)`. WhatsApp disabled → EMAIL if body exists, else consume offset and skip (`DunningEngineJob.PreDunning.cs` 43–52). **Live campaign**, not snapshot. Mid-flight campaign edits **do** change pre-dunning. Snapshot is PAST_DUE-only.

**PAST_DUE claim** (`116–125`): `PAST_DUE`, has `NextBillingDate`, dunning not paused. Collection pause is irrelevant (you cannot pause collection from PAST_DUE — domain throws).

`PastDueDunningProcessor`: if no campaign id, `FindBest` + assign **with snapshot** (`58–76`). If none, warn and return (sub stays PAST_DUE forever with no email — ops must notice). Then snapshot resolve / lazy backfill (`297–333`). Due steps `DayOffset >= 0 && <= daysOverdue` not yet logged. AUTO_CHARGE / EMAIL / WHATSAPP / ALL as above. Unknown action types are logged and **still consume the offset** (`193–222`). Terminal day = `max(grace, last past-due offset)` (`338–342`). `CANCEL` / `SUSPEND` only; anything else is a no-op terminal.

Reminder-only recovery is the minted URL:

- Billing stores `CurrentRenewalCheckoutUrl` for `NextBillingDate.Date`.
- Dispatcher includes `checkout_url` only when that date still matches (`DunningStepDispatcher.cs` 41–53).
- Communications hydrates `{{renewal_link}}` from that URL, else the update-payment **page**.
- Dispatcher `amount` / `total_price` are `product.Price` (`78–79`) — **not** `UnitAmount × Quantity`, **not** SST. A 3-seat yearly sub gets dunning email copy for the catalog default. That is a lie in the email.

**Arrears / update-payment (public GUID).**

`GET /public/commerce/checkout/{subId}/arrears` and `POST .../update-payment` (`PublicArrearsEndpoints.cs`) take a **raw subscription GUID**. No magic token. No tenant slug. No auth. The GET returns product name, `unit × qty` (no SST), currency, status, and `is_reminder_only` derived from **gateway name**, not from `Subscription.IsReminderOnly` (`50`). A Stripe reminder-only zero-amount sub is advertised as “has a card to update.”

POST (`55–178`):

- Canceled → 400.
- ACTIVE + reminder-only **gateway** → 400 `REMINDER_ONLY`.
- Status must be PAST_DUE / SUSPENDED / ACTIVE.
- Cache: reuse minted URL if `CurrentRenewalCheckoutForDate` matches today (ACTIVE) or `NextBillingDate.Date` (arrears).
- Else mint. ACTIVE update charges **RM 1** verification (`118–131`, metadata `update_payment=1`). Arrears charges the **line without SST**.
- Success webhook for `update_payment=1` + ACTIVE stores the new vault and **does not** advance dates (`GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` 38–52).

Portal update-payment page (`update-payment/[subId]/page.tsx`) is this GUID. Magic-link portal links to it (`portal/page.tsx` 170–177`) whenever the sub is not reminder-only. Dunning emails link to it. Billing mint cancel URL is it. **Anyone who sees a V7 GUID** (time-ordered) can: read amount due, mint a checkout in the merchant’s Stripe/Billplz, and for ACTIVE Stripe/CHIP charge RM 1. Tracker `LP-075 = Y` (“Magic update-payment link”) is operationally true and security-false. The boundary test (`PublicArrearsEndpointsBoundaryTests.cs`) only forbids CRM/One SQL joins. It does not require a token.

### 10. PlanChangePolicy — next-renewal-only, no theatre

```14:34:apps/lazuar-api/Modules/Commerce/Application/PlanChangePolicy.cs
        return new PlanChangePreview(
            sub.ProductId,
            currentUnit * Math.Max(1, sub.Quantity),
            currentProduct.Currency,
            currentProduct.Interval,
            targetProduct.Id,
            nextUnit * qty,
            sub.NextBillingDate,
            AmountDueNow: 0m,
            Policy: NextRenewal);
```

`nextUnit = targetProduct.Price` — the **default** catalog price, not a yearly `ProductPrice` row. Preview interval is the **current** product interval. `prorate=true` and `apply=immediate` are 400 (`36–47`). Live status must be `ACTIVE` or `TRIALING` (`50–56`). Target must be same org, active, `mo|yr`, same gateway, same currency, **same interval** (`58–89`). Interval change “requires a new checkout.”

Admin and portal handlers schedule `PendingProductId` only (`ChangePlanCommandHandler.cs` 47–50). Billing applies on the due tick then charges the new default price (`BillingEngineJobTests` 604–626). Undo = POST with null product id.

Portal extra guards: PAST_DUE must update payment first; cancel-at-period-end must Keep first (`ChangePortalPlanCommandHandler.cs` 40–48). Plan picker lists other active recurring products same gateway+currency (`CommerceQueryService.Portal.cs` 110–119`) using **`p.Price`**, not the matching `ProductPrice` for the subscription’s interval.

This is Chargebee-shaped vocabulary with a Chargebee-shaped hole: no unused-time credit, no immediate upgrade, no interval swap, no mid-cycle charge. Sell “change plan at next renewal.” Do not sell “proration.”

### 11. Portal lifecycle

Magic link: `POST /{tenantSlug}/portal/magic-link` always 200 (`PublicPortalEndpoints.cs` 65–73). Handler no-ops on bad email / unknown tenant / no profile / no sub (`RequestPortalMagicLinkCommandHandler.cs` 34–55). Newest sub for that client is the token subject. Token validates to a subscription id; mutating another sub is allowed only if `ClientProfileId` matches (`PortalSubscriptionAccess.cs` 45–48).

`GET /{tenantSlug}/portal?token=` returns all non-PENDING subs + orders for that client (`CommerceQueryService.Portal.cs` 41–49). `current_period_end` is `NextBillingDate`. Documents are attached via `PortalDocumentQueryService`: ledger rows by tx external ref / log id, classified Official Receipt / Tax Invoice / Credit Note; plus OPEN/any custom sessions with a document number as Proforma. Download URLs are HMAC-signed Billing public routes (`203–220`). Portal page also lists them in a table (`portal/page.tsx` 200–237). Tracker `LP-175 = B` is stale; buyer download exists. Whether the PDF is a legal tax invoice is a Billing/LHDN question (report 04).

Cancel: portal default `at_period_end ?? true` (`PublicPortalEndpoints.cs` 166`). Admin default `false` (`SubscriberEndpoints.cs` 109`). Integration M2M cancel is **hard immediate** (`IntegrationSubscriptionEndpoints.cs` 87`). Decision table (`SubscriptionCancelDecision.cs`): already CANCELED → idempotent; not ACTIVE/PAST_DUE/SUSPENDED → throw (kills TRIALING); `atPeriodEnd` + ACTIVE + future next → flag; otherwise immediate + event. PAST_DUE schedule request falls through to immediate. Keep clears the flag; 400 if already CANCELED.

Portal UI (`portal/page.tsx`):

- Healthy ACTIVE: period-end Cancel, optional immediate, plan change, update-payment (if not reminder-only).
- Flagged ACTIVE: Keep + update-payment.
- PAST_DUE: immediate cancel + update-payment.
- TRIALING: badge only.
- SUSPENDED: shown in the list (status ≠ PENDING) with no dedicated actions except whatever the GUID page does.

Plan change UI: `PortalPlanChange.tsx` fetches plans, POSTs without `prorate`/`apply`, copy “No charge today.”

### 12. MRR / ARR stats

`GET /admin/commerce/stats` (`StatsEndpoints.cs` 16–22`) → `GetStatsAsync`.

```47:61:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs
        var mrr = subs.Sum(s => CommerceMrr.MonthlyEquivalent(
            s.Status,
            s.CollectionPausedUntil,
            now,
            s.Interval,          // <-- p."Interval", NOT s."BillingInterval"
            s.UnitAmount,
            s.Quantity,
            s.Price));
        // ...
        double arpu = activeSubs.Count > 0 ? (double)(mrr / activeSubs.Count) : 0;
```

`CommerceMrr.MonthlyEquivalent` (`CommerceMrr.cs` 11–38`): 0 unless status `ACTIVE`; 0 if collection-paused; 0 unless interval `mo` or `yr`; else `unit × qty`, yearly ÷ 12. `TRIALING` is 0 (tested). `PAST_DUE` is 0 (tested). Snapshot unit wins over catalog fallback (tested).

ARR is `mrr * 12` (`121`). That is not independently earned ARR.

SQL interval is **`p.Interval`** (`36`). A yearly subscription on a product whose default is monthly is counted as monthly (12× too much). `BillingInterval` exists and is ignored here. W3-LP-161-done said “snapshot, catalog edits do not move MRR.” Unit snapshot is true. Interval snapshot is **false**.

`activeSubs` for the count / ARPU denominator is `ACTIVE || PAST_DUE` (`46`). MRR excludes PAST_DUE; ARPU includes them. Churn is `canceledLast30 / (activeNow + canceledLast30 - newActiveLast30)` and `newActiveLast30` is counted on that same ACTIVE+PAST_DUE set. Directional, as LP-162 says.

Recovered revenue is `SUM(DunningCampaigns.RecoveredRevenue)` (`108–116`) — campaign counters, not a ledger rollup. Title of LP-161 is “Honest MRR / ARR **(ledger-based)**.” The implementation is **subscription-snapshot-based**. Tracker `LP-161 = Y` oversells the parenthetical.

Cash KPIs (`total_revenue_collected`, cash-flow trend, payment methods) come from `TransactionLogs` CONFIRMED, which is closer to cash than MRR is.

### 13. Adjacent Commerce facts that change the sell

- **Email is a hard gate** on create-product activate and on initiate. No Resend, no checkout.
- **Coupons** are real (reserve on initiate, confirm on pay / zero-amount / offline, release on expiry). Not stacked. Not on trials.
- **Quotes** (`CreateCustomCheckout`) allocate `QT-` numbers, optional `due_at` / `net_7|15|30`, hourly invoice reminders at −3/0/+3, draft PDF URL. Session never becomes a subscription.
- **Disputes** persist `commerce.Disputes`, stamp the tx log, emit a refund-shaped event into Billing, **do not cancel** the sub, **do not set** `HasOpenDispute`. Billing job will keep charging a disputed ACTIVE vault.
- **Integration API** can list/filter/get/cancel. Cancel is immediate only. No enroll, no plan change, no pause.
- **GetSubscriberByIdAsync** loads up to 10_000 rows and filters in memory (`CommerceQueryService.Subscribers.cs` 106–110).
- **Fulfillment** is a string list. `internal:FOO` becomes a typed event; everything else is outbound webhook fodder. Commerce does not provision Telegram.

---

## Gaps

Grouped by whether a buyer or a merchant can be hurt this week.

### P0 — money or security, not taste

1. **Billing collection-pause reclaim / batch starvation.** Claim SQL ignores `CollectionPausedUntil`. Process returns without advancing `NextBillingDate`. A paused-due row is claimed forever and can fill the 50-slot batch. Evidence: `BillingEngineJob.cs` 129–137 + 193–197; test 583–601 only asserts skip, not isolation. Fix: exclude paused-until > now in claim SQL **or** bump `NextBillingDate` to `CollectionPausedUntil` when skipping.

2. **Public GUID update-payment / arrears.** `GET/POST /public/commerce/checkout/{guid}/…` has no token. V7 GUIDs are time-ordered. ACTIVE Stripe/CHIP can be charged RM 1 by a stranger. PAST_DUE can have a bill minted on the merchant’s gateway. Evidence: `PublicArrearsEndpoints.cs` 25–53, 55–178; portal `update-payment/[subId]/page.tsx` 15–33. Fix: require the magic-link token (or a single-use arrears token) on both routes; stop putting the raw sub id in emails as the only secret.

3. **Zero-amount Stripe/CHIP is forced reminder-only.** `ProcessZeroAmountCheckoutCommand.cs` 89–95 computes then ignores capability. 100% coupon and $0 Stripe plans never vault. Next cycle they mint and go PAST_DUE instead of off-session. Test **asserts** the bug (`ProcessZeroAmount_Recurring_ActivatesReminderOnly`). Fix: pass the computed `reminderOnly`, or (better) send $0 setup-future like the trial path so a card exists.

4. **Trial cannot be canceled through any HTTP path.** Domain allows it; `SubscriptionCancelDecision` rejects any status other than ACTIVE/PAST_DUE/SUSPENDED; portal hides the buttons. A 90-day trial is a trap. Evidence: `SubscriptionCancelDecision.cs` 22–26 vs `Subscription.cs` 337–338 vs `portal/page.tsx` 73–155.

### P1 — wrong money, wrong tax, wrong metric

5. **SST on hop 1 only.** `SstTaxMath` is called solely from `InitiateCheckoutCommandHandler.cs` 237–241. Renewals, off-session, mint, arrears GET/POST, webhook `amount`, dunning email `amount` all use `UnitAmount × Quantity` or `product.Price`. A legal SST merchant undercharges every renewal by the tax rate. Tests cover hop-1 math only (`SstTaxMathTests.cs`).

6. **Hop 1 total omits SST.** Buyer sees pre-tax, pays post-tax. `OrderSummaryCard.tsx` 136–141.

7. **MRR interval is catalog default, not `BillingInterval`.** Yearly seats on a monthly-default product inflate MRR ×12. `CommerceQueryService.Stats.cs` 36, 51. ARR is just `×12`.

8. **ARPU denominator includes PAST_DUE** while MRR excludes them (`Stats.cs` 46–61).

9. **Dunning / invoice email amounts ignore seats, snapshot, and SST.** `DunningStepDispatcher.cs` 78–79.

10. **Coupon vs chosen price inconsistency** in zero-amount and offline (`ProcessZeroAmount` 51, offline 85) vs initiate (224).

11. **`HasOpenDispute` is dead.** Dispute handler never sets it. Billing will off-session charge through an OPEN dispute. `CommerceGatewayDisputeCreatedHandler.cs` 85–121 vs `Subscription.cs` 31.

12. **PWYW is a decorative input.** Catalog `Price` is charged. Hop 1 lies.

13. **Manual enroll / record-payment interval** uses `product.Interval`, not `BillingInterval` (`CreateManualSubscriberCommandHandler.cs` 87–88; `RecordSubscriberPaymentCommandHandler.cs` 90).

14. **Plan-change preview / apply uses `target.Price`**, not the price row that matches the subscription interval (`PlanChangePolicy.cs` 22; `BillingEngineJob.cs` 226). Safe today only because interval change is forbidden.

15. **Portal `is_reminder_only` on arrears GET is gateway-derived**, not row-derived (`PublicArrearsEndpoints.cs` 50). Stripe-but-reminder-only (the zero-amount bug) is shown as “update your card.”

### Product holes (not bugs — missing, do not sell)

- No unused-time proration, no immediate plan change, no interval swap, no setup fee, no add-ons, no usage, no import-with-vault, no e-mandate, no wallet/QR on our page, no custom domain, no overlay checkout, no abandoned-cart mail, no `PAUSED` status, no trial cancel UX, no portal seat change, no M2M enroll.
- WhatsApp dunning is a demote-to-email stub (`DunningStepDispatcher.cs` 18–30). `Messaging:WhatsAppEnabled` defaults false.
- Success page is a session poller, not fulfillment. Integrators must take the webhook.

---

## Tests that exist

What is actually pinned (so a future revert would go red):

| Area | What the tests prove | What they do **not** prove |
|------|----------------------|------------------------------|
| Billing claim/status | PAST_DUE/SUSPENDED/CANCELED/PENDING/future skipped; due ACTIVE → PAST_DUE if no vault | Pause exclusion from claim; batch starvation |
| Billing off-session | Stripe/CHIP vault → attempt 1, dates frozen; second tick no re-fire | SST on amount; BillingInterval vs catalog |
| Billing mint | Reminder-only / Billplz mints URL bound to **existing** sub id; generate throw keeps ACTIVE | Quantity 1 + line amount not squared in a real adapter |
| Billing cancel-at-period-end | Flagged due cancels without charge/mint; future flagged untouched | TRIALING flagged (HTTP cannot set the flag) |
| Billing trial | Trial not due → no charge | Trial due convert + failed convert |
| Billing pause | Skip + keep ACTIVE + keep past NextBillingDate | Next tick does not reclaim |
| Billing plan/qty | Pending product then charge new default; 3 × 50 = 150 | SST; yearly row vs default |
| Cancel decision | ACTIVE schedule/immediate/keep; portal foreign client 401; due schedule → immediate | TRIALING cancel (untested because rejected) |
| Trial domain | ActivateTrial clocks; `ScheduleCancelAtPeriodEnd` allows TRIALING; one-time SetTrialDays throws | HTTP cancel; vaulting $0 hop |
| Quantity | 1–99 FIXED one-time/mo/yr; PWYW N≠1 throws | Hop-1 selected interval vs default |
| Plan policy | Preview amount_due_now=0; prorate/immediate 400; gateway mismatch | Yearly price row |
| Change-plan handler | Pending set, undo, foreign org, one-time target | Portal PAST_DUE / flagged guards (those are in ChangePortalPlan tests) |
| MRR helper | ACTIVE mo/yr; yearly/12; PAST_DUE/TRIALING/paused 0; snapshot vs catalog | Stats SQL using `p.Interval` |
| SST helper | 06=0; 02+reg=8; 02 no-reg=0 | Renewal / billing / arrears |
| Zero-amount | **Stripe $0 → reminder-only ACTIVE** | That this is undesirable |
| Completeness | Initiate unit×qty not squared; coupon; offline qty; webhook order qty | PWYW custom amount |
| Arrears boundary | No crm/one SQL in the file | Authn |
| Dunning | Snapshot, day-0, hard-decline skip, WhatsApp demote, pre-dunning excludes flagged + pause | Email amount = seats |
| Portal i18n | EN/BM key parity | Any money |

Wave done notes claiming “Commerce filter **355 passed**” are a point-in-time CI brag, not a contract. This report does not re-run the suite.

---

## What is sellable vs a lie

### Sell (with the sentence in the contract)

| Claim | Honest sentence |
|-------|-----------------|
| Hosted checkout link | Name, email, optional phone/address/TIN, coupon, qty 1–99 on FIXED, monthly/yearly toggle if both prices exist, then redirect to **one** BYOK gateway. |
| Guest checkout | Yes. No buyer account required. |
| BM / EN | Dictionary + toggle on hop 1. Address is still a raw text box. |
| Branding | Workspace name, https logo, hex accent on hop 1 header/CTA. Not a custom domain. |
| Company + TIN | When the product requires it, collected and TIN-checked against MyInvois. Not an e-invoice. |
| One-time + monthly + yearly | Yes. At most two recurring prices. Default write-through still `Product.Price`. |
| Subscription statuses | PENDING, ACTIVE, TRIALING, PAST_DUE, SUSPENDED, CANCELED. Pause is a date on ACTIVE. |
| Auto-renew | Stripe/CHIP + saved token + not reminder-only. Attempt 1 on the due tick; dunning retries 2–4; hard decline stops AUTO_CHARGE. |
| Reminder-only | Billplz (and any non-vault gateway): we email a hosted bill each cycle. First-class, not an apology. |
| Offline / manual | Clerk enroll + mark-paid + record-payment. Always reminder-only. BANK_TRANSFER / CASH / COMPED. |
| Free trial | 1–90 days, recurring only. Vaulting gateways take a $0 setup-future. Convert is the normal due tick. |
| Cancel now | ACTIVE / PAST_DUE / SUSPENDED. |
| Cancel at period end | ACTIVE with a future next bill. Portal default. Admin default is immediate. Undo = Keep. |
| Plan change / seats | Next renewal, same gateway/currency/interval, RM 0 today. No proration. |
| Dunning campaigns | Schedule, snapshot on PAST_DUE, EMAIL, AUTO_CHARGE (Stripe/CHIP), terminal cancel/suspend, per-sub pause. |
| Magic-link portal | List, period-end cancel, keep, plan change, signed document links, link out to update-payment. |
| Quotes | Custom lines, QT- number, due date, −3/0/+3 email, draft PDF. Not a subscription. |
| MRR card | Sum of ACTIVE unpaused snapshot monthly equivalents. Not cash. Not ledger. |

### Do not sell (the code will embarrass you)

| Claim | Why it is a lie today |
|-------|------------------------|
| “Wallet QR / DuitNow / TnG on our checkout” | Hop 1 has zero wallet UI. Hop 2 is Billplz/Stripe/CHIP/Xendit. LP-021’s Y is mobile CSS. |
| “Apple Pay on Lazuar” | Stripe Checkout maybe. Not us. |
| “PWYW” | Input is ignored. Catalog price is charged. |
| “SST on every invoice / renewal” | Tax is hop-1 metadata only. Renewals are net. |
| “Proration” | Policy hard-codes `amount_due_now=0`. `prorate=true` is 400. |
| “Pause membership” | Collection holiday. Status stays ACTIVE. Access webhooks are not paused. Billing reclaim bug. |
| “Cancel your trial” | No HTTP path. |
| “100% coupon on Stripe still saves the card” | Forced reminder-only. Next cycle is a Billplz-shaped email. |
| “Update-payment is a secure customer portal card” | It is an unauthenticated GUID. LP-075 Y is unsafe. |
| “WhatsApp dunning” | Console stub. Campaign steps demote to email. |
| “Ledger-based ARR” | Snapshot × 12. Interval from catalog. |
| “Import subscribers” | Manual enroll qty 1, no vault, reminder-only. |
| “FPX e-mandate / auto-debit FPX” | `SupportsEmandate` is false. Billplz cannot vault. |
| “Usage / add-ons / setup fee” | No columns. |
| “Dispute stops billing” | Dispute row only. `HasOpenDispute` never flips. |

---

## Tracker cells that are stale versus this code

`plans/007-feats/00-checklist-tracker.md` is a living file that was **not** flipped when Waves 1–4 landed (the done notes say “can move”). After reading the code:

| ID | Tracker today | After this read | Why |
|----|---------------|-----------------|-----|
| **LP-014** Quantity on checkout | **P** | **Y** (FIXED one-time + mo/yr, 1–99). Seats-on-renewal is LP-060 Y. | `CommerceCheckoutQuantity` + hop 1 stepper. PWYW still 1. |
| **LP-021** Mobile-first / wallet QR | **Y** | **P** | Mobile CSS yes; wallet QR no. Title is a compound lie. |
| **LP-022** Company + TIN | **B** | **P** | Hop 1 fields + MyInvois validate. Not a tax invoice. |
| **LP-025** Branding | **P** | **Y** for logo/name/hex. Custom domain remains N. | Portal header + `--brand`. |
| **LP-052** Automatic renewal | **P** | **P** (keep) | Stripe/CHIP works; Billplz is mint; zero-amount Stripe regresses to mint; pause reclaim. |
| **LP-053** Reminder-only | **P** | **Y** as a collection mode; **P** if you include the Stripe-$0 accident | Ops + hop 1 copy + minted `{{renewal_link}}`. |
| **LP-054** Trial | **Y** | **Y** with a P0 cancel hole | Status + convert path exist. Cancel does not. |
| **LP-056** Cancel at period end | **N** | **Y** | Entire W1-LP-056 stack is in the tree. Tracker never flipped. |
| **LP-057** Pause | **Y** | **P** | Flag + endpoints exist; billing reclaim is P0. |
| **LP-059** Proration / next-renewal | **Y** | **Y** only with the footnote **next-renewal-only** (already in the statuses note). | Do not drop the footnote. |
| **LP-075** Magic update-payment | **Y** | **P** | Works, unauthenticated GUID. |
| **LP-105** Payment terms / AR reminders | **Y** | **Y** for **quotes only** | `InvoiceReminderJob` ignores product sessions. |
| **LP-161** Honest MRR/ARR (ledger-based) | **Y** | **P** | Snapshot MRR yes; not ledger; interval from catalog; ARR = ×12. |
| **LP-173** Update payment from portal | **P** | **P** (keep) | Link exists; it is the GUID page, hidden for reminder-only. |
| **LP-174** Change plan from portal | **Y** | **Y** | Next-renewal only, same caveats as LP-058/059. |
| **LP-175** Invoice / receipt history | **B** | **P** or **Y** depending on whether signed PDF = “history” | `PortalDocumentQueryService` + portal table. |

Cells that remain honestly N/R for this slice: LP-015 bump, LP-016 abandoned, LP-017 custom domain, LP-018 overlay, LP-032 e-mandate, LP-061 usage, LP-062 add-ons, LP-064 import.

007 Wave done notes that said “tracker can move” and were ignored: W1-LP-025, W1-LP-056, W2-LP-022 (`B → P`), W3-LP-054/057/058/059/060/063, W3-LP-161. Treat 007 as archaeology.

---

## Verdict

Commerce after Waves 0–4 is a **real** hosted checkout plus a **real** subscription state machine plus a **real** hourly billing job. That is more than the August 16 parent evaluation described (that file still says “`TRIALING` is mentioned once, no trial product field, no proration, no plan change”). The parent evaluation is stale. This report replaces it for this slice.

It is **not** Chargebee and it is **not** Stripe Billing. The honest product is:

> Malaysian BYOK checkout that can sell a fixed one-time or a monthly/yearly seat, take FPX via Billplz as a pay-link-each-cycle, take cards via Stripe/CHIP with off-session renewals, run a dunning campaign, and let the buyer cancel at period end.

Four defects stop that sentence from being sellable without an asterisk:

1. Collection pause can stall the billing worker.  
2. Update-payment is a public object-id.  
3. Trials cannot be canceled.  
4. SST and $0-Stripe both break the second charge.

Until those four are closed, sell reminder-only Billplz retainers and Stripe card subs **without** promising SST-on-renewal, trials-you-can-leave, collection holidays, or “secure update card” as a security property.

Do not flip tracker cells in 007 from this report. Flip them when the code and the cell are re-checked together, or replace 007 with 008 as the living honesty file (the 008 README already says that).

---

## Recommended next actions

Order is harm, not wave number. Do not start wallets, usage, or proration while the billing worker can be wedged and a GUID can mint a PaymentIntent.

1. **P0 — Billing claim SQL.** Add `AND ("CollectionPausedUntil" IS NULL OR "CollectionPausedUntil" <= NOW())` (and the in-memory equivalent). Add a test: two due rows, one paused, batch processes the unpaused sibling **and** a second `RunOnce` still does not starve. Optionally on skip set `NextBillingDate = CollectionPausedUntil` so the row is not even a candidate.

2. **P0 — Authenticate arrears / update-payment.** Require the portal magic token (or a signed arrears token with expiry) on `GET/POST /public/commerce/checkout/{subId}/…`. Change email and portal links to carry the token. Keep the GUID in the path if you want, but it must not be sufficient. Add a test that no token → 401.

3. **P0 — Trial cancel.** Put `TRIALING` in `SubscriptionCancelDecision` (schedule if trial end is in the future; immediate allowed). Show the same portal buttons as healthy ACTIVE. Add the test that W1-LP-056 forgot.

4. **P0 — Zero-amount vaulting path.** Stop hard-coding `reminderOnly: true`. For Stripe/CHIP $0 / 100% coupon, reuse the trial $0 setup-future mint so a PaymentMethod exists. Change `ProcessZeroAmount_Recurring_ActivatesReminderOnly` so a Stripe $0 plan is **not** reminder-only, and add a Billplz $0 case that still is.

5. **P1 — SST on the second charge.** `SubscriptionBillingAmount.Line` (or a sibling) must apply `SstTaxMath` with the merchant SST registration + product rate. Thread it through billing off-session, mint, arrears, webhook amount, dunning payload. Show tax on hop 1 summary. Tests: initiate 108; renewal 108; arrears 108.

6. **P1 — Stats honesty.** `GetStatsAsync` must use `COALESCE(s.BillingInterval, p.Interval)`. Exclude PAST_DUE from the ARPU denominator. Rename the dashboard tooltip from “ledger-based” to “committed snapshot.” Tracker LP-161 → P until then.

7. **P1 — Dispute flag or billing skip.** Either write `HasOpenDispute` and exclude those rows from off-session, or delete the column. A dead boolean next to a live charge job is how someone will “fix” a dashboard filter and think they paused collections.

8. **P1 — Email amounts.** Dispatcher should send `SubscriptionBillingAmount.Line` (and SST once (5) exists), not `product.Price`.

9. **Honesty pass (no feature work).** Flip or footnote the stale tracker cells listed above **or** mark 007 historical in its header the way 008’s README already does. Fix parent `00-evaluation.md` §4 “no trial / no plan change” so sales does not quote it.

10. **Do not do next:** wallet pixels on hop 1, usage, add-ons, immediate proration, FPX e-mandate, subscriber import, WhatsApp as a sold channel. Those are Wave-shaped ambitions. The money loop is still leaking.

---

*End of report 01. Payments adapters, ledger/refunds, and LHDN documents are reports 02–04. Do not summarize this file into a bullet list and throw away the file:line evidence.*
