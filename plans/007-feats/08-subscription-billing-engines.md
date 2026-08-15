# 08 — Subscription billing engines vs Lazuar

**Program:** `plans/007-feats` — competitor features vs **Lazuar Pay** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`).  
**File role:** Canonical billing-engine feature checklist extracted from Chargebee, Recurly, Maxio/Chargify, Zuora, Lago, Orb, and Stripe Billing, then compared item-by-item to Lazuar Commerce + Billing + Payments + One as they exist in the repo on **16 August 2026**.  
**Not:** a commitment to ship Chargebee. Not a rewrite of Aura System A (Paddle). Not a claim that Lazuar is a subscription billing company yet.  
**Status:** Full uncondensed analysis. No product code from this file.

Sibling reports in this folder: `01` inventory, `04` Stripe (rail + rival), `07` MoR, `11` subscription lifecycle, `12` dunning, `15` invoicing. This file is the **engine checklist**. Do not merge it into those reports.

---

## Method

### Question this file answers

When a SaaS founder, indie hacker, or MY/SEA operator evaluates Lazuar as **Checkout-as-a-Service with subscriptions**, they do not compare it to Fresha. They compare it — consciously or not — to the engines that trained the market:

| Engine | What it trained the market to expect |
|--------|--------------------------------------|
| **Stripe Billing** | Products + Prices, invoices as first-class objects, Smart Retries, hosted Customer Portal, meters, quotes, Stripe Tax. “Just use Stripe.” |
| **Chargebee** | Product Catalog 2.0 (plan / addon / charge / price point), entitlements, CPQ, Smart Dunning, RevRec, RevenueStory, multi-entity, 30+ gateways. The default “we outgrew Stripe Billing” answer. |
| **Recurly** | High-volume digital commerce, surgical proration (“only bill what changed”), dunning that actually expires, add-ons + usage components, Compass. |
| **Maxio (Chargify + SaaSOptics)** | Advanced Billing states (`trialing` → `past_due` → `unpaid` → `canceled`), components, invoice vs automatic collection, billing portal, RevRec in the same vendor. |
| **Zuora** | Order-to-revenue. Amendments version subscriptions. CPQ in Salesforce. Zuora Revenue as a separate close product. Enterprise collections. |
| **Lago** | Open-source usage-first billing. Events → billable metrics → invoices. Hybrid plans, coupons, entitlements, payment-agnostic. Self-host or cloud. |
| **Orb** | Invoice-based usage platform. SQL metrics, dimensional prices, credit ledgers, retroactive re-rating, pricing simulation. Vercel / Replit / Supabase class. |

Lazuar’s own ADRs (`019` Checkout-as-a-Service, `021` Compliance CaaS, `023` Pure CaaS MVP “UI lobotomy”) already chose a **different company**: BYOK (not MoR), MY LHDN + WhatsApp dunning as the wedge, product catalog as **checkout links**, financial truth in a double-entry ledger that is **not** a Chargebee invoice object. This file does not reopen those decisions. It records **what the engines sell**, **what the repo actually implements**, and **which gaps a founder will treat as blockers**.

### Evidence, not marketing

| Layer | What was read |
|-------|----------------|
| Competitor product / docs (2026) | Chargebee Features + Billing 2.0 nav (subscriptions, entitlements, usage, invoices/credit notes, taxes, hosted portal, multi-business-entity, Time Machine, migration). Recurly change-subscription + statuses + usage-based billing. Maxio subscription states + dunning. Zuora Billing / CPQ / Revenue / 2026.Q2 usage. Lago homepage + GitHub positioning. Orb core concepts + enterprise billing. Stripe Billing overview + subscription statuses + meters + customer portal. Secondary 2026 reviews used only to confirm public pricing/module splits. |
| Lazuar Commerce | `Modules/Commerce/Domain/Aggregates/{Product,Subscription,Coupon,CheckoutSession,Order,DunningCampaign}.cs`, `ChargeAttemptLimits.cs`, `Entities/ChargeAttemptLog.cs`, workers (`BillingEngineJob`, `DunningEngineJob.*`, `CheckoutSessionExpiryJob`), endpoints (`SubscriberEndpoints`, `PublicPortalEndpoints`, admin TypeSpec), `CommerceQueryService.Stats.cs`, `GatewayPaymentFailedIntegrationEventHandler`, `CancelAdminSubscriptionCommandHandler`, `RecordSubscriberPaymentCommandHandler`. |
| Lazuar Billing | `Modules/Billing/README.md`, `LedgerEntry.cs`, `AccountTypes.cs`, `DeferredRevenueSchedule.cs`, `InvoiceIssuedHandler.cs`, TypeSpec `modules/billing/{models,routes}.tsp`. |
| Lazuar Payments / One / Communications / Lhdn | Off-session charge + `PAYMENT_FAILED` publish, Stripe/CHIP/Billplz/Razorpay adapters, `TenantAppEntitlement`, dunning variable fill in `FulfillmentRequestedIntegrationEventHandler`, LHDN CreditNote UBL strategies. |
| Lazuar UIs | `apps/lazuar-ops` commerce + hidden invoicing (`[MVP-HIDE]` ADR 023), `apps/lazuar-portal` portal/cancel/update-payment/QuoteView. |
| Contracts | `packages/api-spec/docs-commerce.tsp`, `modules/commerce/models/{subscriber,product,dunning,portal,stats,webhooks}.tsp`, admin + public routes. |
| Prior internal gap notes | `docs/001-gaps/01-dunning-engine.md`, `05-billing-module.md`, `07-commerce-module.md` — **treated as historical**. Several P0s in those notes are **closed in the current tree** (payment-failed → PAST_DUE, multi-attempt charge logs, coupon confirm on paid path, portal cancel, admin cancel/record-payment, catch-up dunning, `{{plan_name}}` fill). This file scores **current source**, not the June/July 2026 gap memos. |

No runtime soak was executed. Statuses are **code-and-contract** statuses.

### How a cell is marked

Same honesty vocabulary as `20-sequencing-and-tracker-schema.md`, applied to **billing engines**:

| Mark | Meaning in this file |
|------|----------------------|
| **Y** | Engine has a production, marketed version of the job |
| **P** | Engine has a slice, add-on module, or awkward path |
| **N** | Engine does not ship the job as a product |
| **—** | Not applicable to that engine’s category |

**Lazuar depth** (ours):

| Value | Use when |
|-------|----------|
| **shipped** | Tenant or buyer can complete the job on a production-shaped path **and** we are willing to say so |
| **partial** | Real slice (API, worker, or UI). Missing a first-class object, a closed loop, or a claim |
| **stub** | Field, table, or handler exists; no producer or no product path |
| **none** | No product path |
| **doc_off** | Code exists, **not** registered / **not** claimed (Billing `RevenueRecognitionJob`) |
| **killed** | UI severed on purpose (ADR 023 invoicing nav) while backend remains |
| **n/a** | Out of company-shape (become MoR, clone Zuora Revenue, take GMV) |

**Row verdict** (owner decision about Lazuar Pay):

| Verdict | Meaning |
|---------|---------|
| **Ours** | Shipped and claimable as CaaS |
| **Both** | Shipped and the engines also have it — table-stakes parity |
| **Partial** | Slice exists; an engine is deeper **or** our slice is not the job founders name |
| **Theirs** | Engines have it; we do not; Later vs Never not yet frozen |
| **Later** | Intend to implement after a named wave |
| **Never** | Refuse as Lazuar Pay (trap). Engine having it is not a gap |
| **N/A** | Wrong category (Aura Paddle Plan desk, gym membership OS) |

### Standing constraints (do not contradict)

From Lazuar ADRs 019 / 021 / 022 / 023, this folder’s README, and Aura `PADDLE-BOUNDARY.md` (System A is **not** this product):

1. Lazuar is **BYOK**, not Merchant of Record. Money lands in the tenant’s Stripe / Billplz / CHIP / Razorpay account. Do not score Paddle/Lemon Squeezy MoR as a feature to copy into Commerce.
2. Aura salon SaaS (System A, Paddle RM 149) is a **different money plane**. Aura `SA-*` rows stay on Paddle. This file’s `BE-*` rows are **Lazuar Commerce/Billing as a product other SaaS companies buy**.
3. ADR 023 hid Quotes / Tax Invoices / Credit Notes / Billing Profile from ops and hid tax-invoice download + custom-quote `/pay` from portal. Backend is **dark matter**, not deleted. Scoring those rows **killed** (UI) + **partial** (API) is honest; scoring them **shipped** is not.
4. Revenue recognition job is **parked** (`Billing/README.md` §6). Do not claim RevRec.
5. WhatsApp dunning is a **claimed differentiator** and is **gated** (`Messaging:WhatsAppEnabled`). Score the orchestration that exists; do not pretend Meta utility templates + interactive pay buttons ship.
6. Do not become Zuora. Do not become a marketplace. Do not take GMV.

### Absolute paths (Lazuar)

| Concern | Path |
|---------|------|
| Commerce domain | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/` |
| Billing domain | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/` |
| Payments gateways | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` |
| Commerce TypeSpec | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/` |
| Ops commerce UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/` |
| Portal | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/` |
| This file | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/08-subscription-billing-engines.md` |
| Folder index | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/README.md` |
| Parent eval | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-evaluation.md` |
| Living tracker (to be filled) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md` |

---

## Canonical billing feature taxonomy

The seven engines do not share a data model. They share a **job list**. Founders who have used any one of them will ask for these jobs by name. This section freezes the list so later tracker rows do not invent a second vocabulary.

### Architectural split the engines actually implement

Three gravity wells. Mixing them in one sentence is how billing products become unshippable.

| Gravity | First-class object | Engines | What “done” means |
|---------|--------------------|---------|-------------------|
| **Subscription-first** | Subscription + Plan + Invoice as a child | Stripe Billing, Chargebee, Recurly, Maxio | Recurring relationship owns the clock. Invoice is emitted from the period. |
| **Invoice / usage-first** | Event → Metric → Invoice; subscription is a container | Lago, Orb, late Chargebee usage, Zuora Usage | Usage is the product. Subscription is “who is on which price book this period.” |
| **Order-to-revenue** | Order / Amendment / Revenue arrangement | Zuora (+ Chargebee RevRec, Maxio SaaSOptics) | Finance close is the product. Billing is an input. |

Lazuar today is a **fourth shape**: **Checkout-session-first**.

- `Product` is a sellable **buy link** (slug + price + interval + gateway + fulfillment URLs).
- `CheckoutSession` is the payment unit.
- `Subscription` is **access + next bill date + vault + dunning pointers**, not an invoice schedule.
- `Billing.LedgerEntry` is **financial truth after money moves**, not an AR invoice the customer can pay.

That shape is a valid CaaS. It is **not** Chargebee. Every “gap” below is either a real founder demand or a category error. The taxonomy names the demand. The comparison table says which.

### Family 1 — Catalog: plans, add-ons, charges, coupons, trials

What engines mean:

- **Plan (or Product + Price):** a recurring commercial offer. Interval, currency, trial days, billing alignment, setup fee, pricing model (flat / per-unit / tiered / volume / stairstep / package). Chargebee PC 2.0: Item → Item price; a subscription has **one plan** plus optional addons and charges. Recurly: Plan with add-on definitions baked in. Stripe: Product + one or more Price objects (including metered). Lago/Orb: Plan is a bundle of charges/prices (some recurring, some usage).
- **Add-on / component:** a second recurring or metered line on the same subscription (extra seat, extra GB, SMS pack). Recurly and Maxio treat these as first-class. Stripe treats them as extra subscription items. Chargebee has Addon + Charge (one-time or recurring).
- **Charge / one-off:** setup fee, professional services, onboarding, mid-cycle “just bill them $2,400.” Chargebee Charges, Recurly one-time charges, Stripe Invoice items, Lago one-off invoices, Orb one-off invoice.
- **Coupon / discount:** percentage or fixed; duration (once / repeating / forever); product eligibility; max redemptions; coupon codes vs automatic discounts; stackability. Recurly and Chargebee have deep coupon-on-change rules. Stripe has Coupons + Promotion Codes.
- **Trial:** time-boxed `in_trial` / `trialing` with or without a card. Convert trial, change plan during trial without invoicing (Recurly), trial end behavior if no PM (Stripe `pause`). Chargebee Trial Management is a whole doc section.

Founder sentence: “I need a Pro plan, a seats add-on, a 14-day trial, and a `LAUNCH30` coupon.”

### Family 2 — Subscription states

Canonical set the prompt asked for, mapped to what each engine actually names:

| Canonical | Chargebee | Recurly | Maxio | Zuora | Stripe | Lago / Orb | Meaning |
|-----------|-----------|---------|-------|-------|--------|------------|---------|
| **future** | `future` | `Future` | `awaiting_signup_date` | Pending Activation | (schedule / backdate) | upcoming / pending | Start date not reached |
| **trial** | `in_trial` | `Active` + trial flag / Trial filter | `trialing` | Active + trial charge | `trialing` | often a plan property, not a status | In trial; provision access |
| **active** | `active` | `Active` (live) | `active` | `Active` (current version) | `active` | `active` | Current, billable |
| **past_due** | invoice in dunning; sub often still `active` | `Past Due` filter on live sub | `past_due` | Collections on account / invoice | `past_due` | unpaid invoice + dunning | Latest collection failed |
| **paused** | `paused` | `Paused` | `on_hold` (product) / `paused` (account suspended) | `Suspended` | `paused` (narrow: trial without PM) | pause collection | Clock stopped on purpose |
| **cancelled** | `cancelled` / `non_renewing` | `Canceled` = will expire at term end | `canceled` (immediate) | `Cancelled` | `canceled` | `canceled` / `terminated` | Will not renew, or already ended |
| **expired** | term ended / `cancelled` after term | `Expired` (terminal, no reactivate) | `expired`, `trial_ended` | `Expired` = **old version** (trap) | `incomplete_expired` (failed activate) | ended | Terminal or version artifact |

Two traps the taxonomy must keep separate:

1. **Cancel-at-period-end vs cancel-now.** Recurly `Canceled` still has access until term end. Chargebee `non_renewing` is the same job. Stripe `cancel_at_period_end`. Maxio `canceled` is often immediate. Founders will say “cancel” and mean either.
2. **Zuora `Expired` is not churn.** It is the previous amendment version. Reporting “expired” as churn from Zuora exports is a famous finance bug.

Also present in engines, not in the seven-word list, and still required in a serious checklist:

- **incomplete / incomplete_expired** (Stripe first invoice unpaid in 23h)
- **unpaid** (Stripe / Maxio: retries exhausted, invoices still generate, collection stopped)
- **soft_failure / assessing / pending** (Maxio transients)
- **transferred** (Chargebee account hierarchy)

### Family 3 — Proration, upgrades/downgrades, scheduled changes

The job: change plan, price, quantity, or add-ons **without** inventing a new customer.

Engines expose three timing knobs:

| Timing | Typical use | Proration? |
|--------|-------------|------------|
| Immediate | Upgrade now | Yes (or “full rebill”) |
| Next bill date | Downgrade | No |
| Term renewal / ramp step | Annual contract, scheduled price | No / ramp invoice |

Recurly is the reference implementation: timeframe + credit option (prorated / full / none) + charge option (prorated / full / none) + “Only Bill What Changed” + preview invoice + modification enforcement (must be current to upgrade). Chargebee: prorate mid-cycle or defer unbilled charges; ramps; price override. Stripe: `proration_behavior`, subscription schedules (phases). Zuora: **amendments** create a new subscription version. Lago/Orb: plan change + invoice recalculation; Orb advertises retroactive re-rating.

Also in this family: **quantity / seats**, **price override**, **grandfathering**, **backdating**, **billing alignment / bill cycle day**, **contract terms**.

### Family 4 — Usage / metered / hybrid

| Layer | Job |
|-------|-----|
| Ingest | High-volume events (Chargebee claims 200k/s; Orb/Lago are built for this) |
| Metric | Count / unique / max / sum / custom SQL (Orb) |
| Price | Per-unit, tiered, volume, package, matrix / dimensional, committed spend + overage |
| Credits | Prepaid packs, rollover, overdraft, multiple credit units (Orb) |
| Hybrid | Seat + usage on one invoice (Chargebee, Lago, Stripe subscription items + meters) |
| Alerts | Threshold webhooks so the app can block or upsell |

If the founder is an AI / infra company, this family **is** the product. If the founder sells a RM 49/mo community, it is Later or Never.

### Family 5 — Invoices, credit notes, one-off charges

This is the object founders mean by “billing.”

- **Invoice:** numbered, line-itemed, tax-inclusive or exclusive, statuses (draft / open / paid / void / uncollectible / past_due), collection method (`charge_automatically` vs `send_invoice` / Net D), consolidated invoices, advance invoices, backdated invoices, hosted invoice page.
- **Credit note:** allocated to an invoice, refunded, or left as customer credit. Created by proration, refund, write-off, or manual adjustment. Chargebee and Recurly treat credit notes as first-class. Stripe has Credit Notes on invoices. Orb attaches credit notes to invoices.
- **One-off:** invoice items, custom line invoices, “payment links” that are not subscriptions.

Without this family, MRR tools, accountants, and B2B buyers will say “you don’t have billing.” A **receipt PDF** and a **ledger journal** are not an invoice.

### Family 6 — Payment methods vault, retries, dunning

| Layer | Job |
|-------|-----|
| Vault | Store PM off-session (cards, tokens). Update PM. Default PM. Multiple PMs. Account updater. |
| Collection method | Auto-charge vs email-me-an-invoice (ACH / wire / FPX manual). |
| Retry | Fixed schedule and/or Smart Retries (Stripe ML; Chargebee Smart Dunning). Decline-code awareness (hard vs soft). SCA / 3DS off-session. |
| Dunning | Day-offset or attempt-offset campaign: email / SMS / in-app / portal / retry charge. Final action: cancel, mark unpaid, restrict, keep trying. Pause dunning. Version the run. |
| Recovery UX | Hosted update-payment page, invoice pay link, customer portal. |

Dunning is **invoice-driven** at Chargebee/Recurly/Stripe (the open invoice is the thing being collected) and **status-driven** at naive implementations (flip the sub to `past_due` and scan a calendar). Founders who have used Stripe will assume Smart Retries exist.

### Family 7 — Revenue recognition, MRR / ARR analytics

| Job | What “done” means |
|-----|-------------------|
| RevRec | ASC 606 / IFRS 15: performance obligations, ratable vs point-in-time, contract mods, refunds. Chargebee RevRec module, Zuora Revenue, Maxio SaaSOptics, Stripe Revenue Recognition (US). |
| Deferred revenue | Unearned balance that amortizes over the service period. |
| SaaS metrics | MRR, ARR, new / expansion / contraction / churn MRR, NRR, ARPU, logo churn, quick ratio, cohort retention. Chargebee RevenueStory, Recurly analytics, Stripe Sigma / dashboard. |
| Finance export | GL mapping, NetSuite / Xero / QuickBooks. |

A dashboard that sums `product.Price` for `ACTIVE` rows is **not** RevenueStory.

### Family 8 — Quotes and CPQ

Chargebee made this a named module (CPQ Lite free for 50 quotes; full CPQ paid). Zuora CPQ lives in Salesforce. Stripe has Quotes that convert to invoices/subscriptions (sales-led, not Salesforce CPQ). Recurly/Maxio are weaker here; Maxio talks quote-to-cash via HubSpot. Lago/Orb generally expect the quote to happen elsewhere.

CPQ jobs: catalog-backed quote, multi-year ramp, bundle/dependency rules, discount caps + approval, e-sign, convert quote → subscription **without re-entry**.

A **custom payment link with line items** is a quote the way a PDF in email is a contract.

### Family 9 — Entitlements

Chargebee: Features → Product entitlements → Subscription entitlements → Customer entitlements; grandfathering; usage caps. Stripe Billing Entitlements: feature → product → active entitlement when subscription is `active`. Lago: entitlements on plans. Schematic / Orb-adjacent tools exist because billing engines were late to this.

The job: **the same record that bills also answers “can this customer call GPT-4 / invite the 6th seat?”** Webhooks that say `subscription.activated` are the 2018 version of this job.

Workspace-module flags (`TenantAppEntitlement` for `PAYMENTS` / `BILLING`) are **not** this job.

### Family 10 — Multi-entity, multi-currency, tax

| Job | Engines |
|-----|---------|
| Multi-business-entity | Chargebee MBE (separate legal entities, tax IDs, branding, customer transfer). Zuora multi-entity. Stripe Connect / separate accounts (different shape). |
| Multi-currency | Price points per currency (Chargebee 100+). Presentment vs settlement. FX on the invoice. |
| Tax | Chargebee + Avalara/Anrok. Stripe Tax. Recurly tax. Zuora tax engines. Country-specific VAT/GST. Exemption certificates. Tax-inclusive vs exclusive. E-invoicing integrations (Chargebee has an e-invoicing index; none of the Western engines natively file **LHDN MyInvois UBL 2.1**). |

Malaysia-specific: SST vs service tax, TIN, consolidated B2C e-invoice on the 28th, 72-hour credit-note rules. This is Lazuar’s **actual** tax product, not Avalara.

### Family 11 — Customer portal self-serve

Stripe Customer Portal is the bar: update PM, view invoices, pay open invoices, upgrade/downgrade (if you enable it), cancel, pause (if enabled). Chargebee hosted self-serve portal. Recurly hosted account management. Maxio Billing Portal. Orb invoice portal (invoice-centric, weak self-serve top-up).

Minimum founder demand: “My user can update the card and cancel without emailing me.”

### Family 12 — Sandbox, import, migrations

| Job | Engines |
|-----|---------|
| Test clock / Time Machine | Chargebee Time Machine; Stripe test clocks. Advance time to fire trials, renewals, dunning. |
| Sandbox site | Chargebee test site; Stripe test mode; Recurly sandbox; gateway sandboxes (Billplz sandbox host). |
| Import / migrate | Chargebee Migration + bulk ops. Stripe Dashboard CSV toolkit (customers, products, prices, PMs, subscriptions, trials, backdating). Recurly imports. Zuora professional services. |
| Grandfathering | Keep old price for existing subs when catalog changes. |

Without import, switching **to** Lazuar is a science project. Without a test clock, QA of dunning is “wait 14 days.”

### Family 13 (implied, still required) — Developer surface

Not in the user bullet list, but every engine’s real product:

- Versioned REST (and sometimes GraphQL / RPC)
- Webhooks with signed envelopes and a frozen event catalog
- Idempotent writes
- Customer / subscription / invoice IDs that do not collide with checkout session IDs
- Docs that describe **integrator** jobs, not the modular monolith

Lazuar already froze a SaaS webhook catalog (`subscription.activated|resumed|past_due|canceled|suspended` — **no** `subscription.updated`). That is a product decision, not a missing Chargebee clone.

---

## Dossiers

Each dossier is the engine as a **checklist source**, then the implication for Lazuar. Pricing figures are public 2026 list prices and will move; they are included because founders use them as a switching argument.

---

### 1. Chargebee

**Who it is.** The default independent subscription billing platform for B2B SaaS that has outgrown “just Stripe Checkout.” Gartner-mentioned recurring billing leader. Public claim: 6,500+ businesses, 30+ payment gateways, 100+ currencies, up to 200k usage events/second (higher as add-on).

**Packaging (2026).** Modular, not one SKU:

| Module | Role | Public price signal |
|--------|------|---------------------|
| **Billing** | Catalog, subscriptions, invoices, dunning, taxes, hosted pages | Starter $0 until $250k lifetime billing then 0.75%; Performance $599/mo ($7,188/yr) up to $100k/mo then 0.75%; Enterprise custom |
| **CPQ** | Salesforce/HubSpot quotes, approvals, ramps | CPQ Lite free first 50 quotes; full CPQ paid |
| **RevRec** | ASC 606 / IFRS 15 | Custom; billing customers only |
| **Growth / Retention** | Cancel flows, offers, dunning sessions | Retention from ~$250/mo / 50–149 sessions |
| **Receivables / Payments / Reveal** | Collections UX, pay-by-link, payment performance | Add-ons |

**Catalog.** Product Catalog 2.0: Items (plan, addon, charge) × Item prices (currency, period, model). Pricing models: flat fee, per-unit, volume, tiered, stairstep, plus usage/token/credit/outcome. Hybrid: plan + addons + charges on one subscription. One plan per subscription (hard limit). Multi-frequency billing: each item can have its own period (annual commit, monthly overage). Variant pricing / grandfathering. Price override per subscription. Contract terms.

**States.** `future`, `in_trial`, `active`, `non_renewing`, `paused`, `cancelled`, plus `transferred` under account hierarchy. Past-due is primarily an **invoice dunning** state; the subscription can remain `active` while invoices are in dunning — operators filter “active with invoice in dunning.” Pause is a first-class subscription action. Cancellation is a dedicated doc with end-of-term vs immediate.

**Changes.** Mid-cycle prorations with MRR updated instantly, or defer charges to next renewal. Credit notes for the unused portion. Ramps (scheduled price/quantity over time). Backdating. Gift subscriptions. Reactivation.

**Usage.** First-class. Stream via API / S3 / file. SQL meters. Prepaid credits with rollover and overdraft. Usage alerts and entitlements-enforced caps. Agentic / outcome pricing is 2025–2026 marketing, but the underlying objects are usage + credits.

**Invoices.** Recurring, consolidated, backdated, prorated, Net D. Credit notes. Transactions. Quotes exist as a **legacy** Billing object; new sales-led motion is **CPQ**.

**Payments / dunning.** 30+ gateways. Smart Dunning on Performance+. Card updater via gateways. Hosted checkout + Chargebee.js + payment components. Retention module for cancel-intent offers.

**RevRec / analytics.** RevRec module: invoice-based, contract-based, credit-based, usage-based; point-in-time and ratable. RevenueStory: MRR/ARR/churn dashboards. Reveal: payment performance. Copilot / MCP (2026) query the live record.

**CPQ.** Live catalog, bundle rules, discount caps, approval routing, quote → subscription with no re-key. Amendment quotes can override entitlements. Agentforce draft-from-transcript is the 2026 story.

**Entitlements.** Full stack: features, product entitlements, subscription overrides, customer entitlements, grandfathering.

**Multi-entity / tax / currency.** Multi Business Entity. Multicurrency price points. Tax configuration + Avalara-class integrations + e-invoicing index. Time zone + Time Machine.

**Portal.** Hosted self-serve portal, hosted checkout, pricing table, additional hosted pages, mobile-optimized.

**Sandbox / import.** Separate test site. Time Machine. Bulk operations. Migration toolkit. Config transfer between sites.

**What Chargebee is bad at (so we do not worship it).** Percentage-of-billing at scale. RevRec/CPQ/Retention are extra invoices. Implementation projects are real. WhatsApp-native dunning is not the product (email/in-app/Retention). LHDN MyInvois is not the product. BYOK to Billplz/CHIP is not the product. You are on Chargebee’s record, not yours.

**Implication for Lazuar.** Chargebee is the **checklist**, not the roadmap. If we try to match PC 2.0 + CPQ + RevRec + MBE, we will die. If we cannot explain — in one sentence each — why we lack invoices, trials, and plan-change, we will lose every founder who has opened Chargebee’s docs.

---

### 2. Recurly

**Who it is.** High-volume subscription commerce (streaming, digital goods, consumer subs) that grew up as “billing that will not embarrass you at 1M renewals.” Compass is the 2026 AI configuration layer (natural-language plan setup).

**Catalog.** Plan is a rich object: trial, setup fee, add-ons (required and optional), per-currency overrides, quantity, usage-based add-ons (measured units), tiered/volume/stairstep on add-ons. Not PC 2.0’s item/price split; more “plan-centric.”

**States (dashboard).**

| Status | Recurly meaning |
|--------|-----------------|
| Future | Start date not reached |
| Active | Live — paying **or** in trial |
| Canceled | Will expire at end of current term; can reactivate before that date |
| Expired | Terminal churn; cannot reactivate |
| Paused | No invoices for N cycles; pause starts at **next** bill date (no mid-cycle pause) |
| Past Due | Filter on live subs with a past-due invoice — not always a mutually exclusive status |

Export filters also include Trial, Renewing, Last Billing Period. This is why “map Recurly 1:1 onto seven enums” is slightly wrong: **trial is a property of Active**, **past due is a property of Live**.

**Changes.** The gold standard. Immediate / next bill date / term renewal. Credit and charge each: prorated, full, or none. Only Bill What Changed (default on sites after 2017-06-30). Preview invoice. Plan change always full-rebills. Quantity or price-only can charge/credit the delta. Usage add-on removed → unbilled usage invoices immediately. Discounts reverse proportionally. Trials: **no change invoice during trial**; original trial rules persist across plan change; Convert Trial API to end trial now. Modification enforcement: require paid invoices to upgrade and/or downgrade.

**Usage.** Usage-based add-ons billed in arrears. Record usage against the add-on. Corrections invoice at next cycle unless the add-on is altered.

**Invoices / credits.** Credit invoices as a site setting. One-off charges. Collection method per subscription (automatic vs manual + net terms + PO).

**Dunning.** Configurable campaigns. Can **expire** the subscription at end of dunning. Important Recurly footgun: past-due invoices are **not** auto-failed if the sub expires **outside** dunning; they sit open. Operators must understand invoice vs subscription death.

**Payments.** Multi-gateway (Stripe, Braintree, Adyen, PayPal, …). Not a processor.

**RevRec.** Add-on SKU in some editions; not the identity of the company. Analytics exist; Compass is the 2026 surface.

**CPQ / entitlements / MBE.** Not Chargebee. Recurly wins **runtime billing**, not Salesforce quoting or feature flags.

**Portal.** Hosted account management; email templates for change / invoice / decline.

**Implication for Lazuar.** Every “change plan” conversation should be scored against Recurly’s three timeframes and three credit/charge options — not against “UPDATE subscriptions SET product_id.” Recurly’s **Canceled ≠ Expired** is the model we should steal **names** from before we steal features: founders will say cancel and mean non-renewing. Lazuar’s `Cancel()` is Maxio-immediate, not Recurly-canceled.

---

### 3. Maxio (Chargify Advanced Billing + SaaSOptics)

**Who it is.** Chargify rebranded and merged with SaaSOptics. Pitch: Advanced Billing **and** subscription financial ops (RevRec, SaaS metrics) in one vendor. HubSpot quote-to-cash listings exist. Grow list price in 2026 reviews: ~$599/mo up to $100k monthly billings (same band as Chargebee Performance — founders will comparison-shop them as peers).

**Catalog.** Product (the plan) + **Components** (quantity, on/off, metered) + coupons. Trial period on the product. Invoice billing vs automatic. Taxation on the product. “Offers” to customize without cluttering the catalog.

**States (documented 2025–2026).**

Live: `active`, `trialing`, `past_due`.  
Inactive: `canceled` (billing stops **immediately**), `expired` (term end), `trial_ended`, `on_hold` (billing paused, can return to active), `unpaid` (dunning ended; charges may accrue; collections paused), `awaiting_signup`, `awaiting_signup_date`.  
Admin/transient: `pending`, `assessing`, `paused` (**Maxio account** suspended — naming collision), `soft_failure` (gateway timeout, auto-retry), `failed_to_create`. Prepaid products can **suspend** when balance is exhausted.

This is the richest public state machine of the seven. It is also the one most likely to leak into a naive “we’ll just add an enum.”

**Dunning.** Global or per-product cadence. Different rules for automatic vs remittance. End states: keep active, restrict, cancel, mark unpaid. **Cancel Dunning** action moves `past_due` → `active` without payment (CS tool). Trial ending without a card → `trial_ended`, not auto-`past_due`.

**Portal.** Advanced Billing Billing Portal link on customer emails.

**RevRec.** The SaaSOptics half is why finance teams pick Maxio over Recurly.

**Implication for Lazuar.** Our `SUSPENDED` is closer to Maxio `on_hold` / prepaid suspend than to Stripe `paused`. Our `CANCELED` is Maxio-immediate. We have no `unpaid`, no `trialing`, no `expired`, no `awaiting_signup_date`. If we add states, steal **Maxio’s live vs inactive split**, not their 15-value enum wholesale.

---

### 4. Zuora

**Who it is.** Enterprise order-to-revenue. Billing, Collect, Revenue, CPQ, Tax — often **separate products and implementations**. November 2025 “Monetization Catalog” was an attempt to stop CPQ / Billing / Revenue from drifting. 2026.Q2: dynamic pricing for individually rated usage; Commitments in CPQ quotes; Salesforce CPQ Orders → Zuora Subscription mapping.

**Catalog / orders.** Product catalog + rate plans + charges (one-time, recurring, usage) + units of measure. **Subscribe and amend**: every commercial change is an amendment; the previous subscription version becomes `Expired` and the new version is `Active`. This is the single most important Zuora fact. `Expired` ≠ customer left.

**States (operational).** Draft, Pending Activation, Pending Acceptance, Active, Suspended, Cancelled. Plus the versioning trap (`Expired` = old version). Future starts are trigger dates (contract effective, service activation, customer acceptance) — more knobs than Chargebee `future`.

**Collections.** Account-level dunning / Collections. Grace before cancel. Collect is its own product.

**CPQ.** Zuora CPQ and Salesforce CPQ connectors. 2026: commitments-only quotes, finance field mapping (AR / deferred / recognized / rule) on the quote.

**RevRec.** Zuora Revenue is the reason enterprises still buy Zuora. Rip-and-replace (2026.Q3 notes) exists because contract replacement is a real close problem.

**Usage.** Zuora Usage Billing + 2026 dynamic pricing for rated usage.

**Implication for Lazuar.** Do **not** implement amendment versioning. Do **not** name a status `EXPIRED` unless we mean terminal churn (Recurly) or we will confuse every report. Zuora is the **Never** north star: if a feature only exists to win a RFP written by a Zuora consultant, refuse. Steal one idea only: **orders as an explicit commercial event** if we ever do mid-life plan changes, so we do not overwrite history in place.

---

### 5. Lago

**Who it is.** Open-source usage-first billing (GitHub `getlago/lago`). Cloud or self-host / VPC. Customers cited on the 2026 homepage: Mistral, PayPal (flexibility story), 1NCE (IoT). SOC2, RBAC. Payment-agnostic (you bring Stripe etc.).

**Catalog.** Plans composed of charges: standard (recurring), charge (one-off), usage (billable metrics). Hybrid plans are the headline. Coupons. Prepaid credits. Entitlements. Custom contracts (overrides).

**Metering.** Event ingest → billable metrics (aggregation) → invoice. This **is** the core loop. Subscriptions exist to attach a customer to a plan and a billing cadence.

**Invoices.** First-class. Draft / finalized. One-off invoices. Taxes as a configuration + integrations. Dunning and alerting are listed as platform pillars (manual dunning guide exists).

**Payments.** Not a vault. Lago emits invoices; a payment provider collects. Same BYOK philosophy as Lazuar, different object (invoice vs checkout session).

**Analytics.** Usage-centric. Not RevenueStory.

**What Lago refuses.** Being a processor. Being a Salesforce CPQ. Being a black box (source is the product).

**Implication for Lazuar.** Lago is the **closest philosophical cousin** (open control, payment-agnostic, developer-first) and the **farthest data model** (we have no events, no metrics, no invoice object). Founders who say “I’ll just self-host Lago” are the competitive set for usage-heavy indie SaaS. Founders who say “I need a Billplz checkout link and WhatsApp when the card fails” are not Lago’s ICP — they are ours.

---

### 6. Orb

**Who it is.** Managed usage billing for high-event-volume AI/infra (public names: Vercel, Replit, Supabase, Neo4j, LaunchDarkly). Center of gravity: metering + rating + invoicing, **not** the accounting close (finance teams still ask about ERP/RevRec). Custom pricing. Percentage-of-revenue at low tiers is the TCO complaint.

**Catalog.** Plan = set of prices. Each price: cadence + pricing function. Dimensional / matrix prices. Committed spend + overage. Custom pricing units with **separate credit ledgers** (compute credits vs API credits).

**Subscriptions.** Customer ↔ Plan. Invoices generate every period. Upcoming / active / ended.

**Invoices.** First-class. Draft-editable. Credit notes. Hosted invoice links + email. Upcoming-invoice API for in-product spend widgets. Custom memos (PO, contract, department).

**Credits.** Prepaid blocks, conversion to invoice currency, overage when exhausted. Retroactive price change → invoice recalculation + credit adjustments (enterprise story).

**Simulation.** Price a plan against historical events before launch. This is why product teams pick Orb over Chargebee usage add-ons.

**Portal.** Invoice-oriented hosted portal. Weak self-serve “buy more credits” compared to Stripe Billing.

**Implication for Lazuar.** Do not build SQL meters to “match Orb.” If we ever do usage, the Lago-shaped loop (event → metric → invoice line) is enough; Orb’s simulation and retroactive re-rating are enterprise sequels. Orb customers will not pick Lazuar for metering. They might pick Lazuar for **MY tax + FPX checkout** in front of a usage engine they already have — that is a compose story, not a clone story.

---

### 7. Stripe Billing

**Who it is.** The default. Billing is a paid Stripe product on top of Payments. One account: Prices, Subscriptions, Invoices, Meters, Tax, Revenue Recovery (Smart Retries), Customer Portal, Quotes, Entitlements, Revenue Recognition (limited regions), Sigma.

**2026 public Billing page jobs:** unify subscriptions + usage; hosted invoices; customer portal; paid trials (preview); e-invoicing; Smart Retries (Stripe cites ~56% recovery on failed recurring; AI retries +~9% vs fixed schedule in their marketing). Meters: 100M events/month in the 0.7% band (fine for SaaS, not for token firehoses).

**Catalog.** Product + Price (recurring or one-time; licensed or metered). Multiple prices per product (monthly/yearly, currencies). Subscription items = the add-on mechanism. Coupons + promotion codes. Subscription schedules = phases (trial → intro price → full, or scheduled downgrade).

**States.**

| Status | Meaning |
|--------|---------|
| `incomplete` | First invoice unpaid / SCA required; 23h window if `charge_automatically` |
| `incomplete_expired` | Failed to activate; terminal for that attempt |
| `trialing` | In trial; provision access |
| `active` | In good standing (outstanding older invoices may still exist) |
| `past_due` | Latest finalized invoice unpaid; Smart Retries running |
| `unpaid` | Retries exhausted; invoices still generated; collection stopped |
| `paused` | Narrow: trial ended, no default PM, `end_behavior.missing_payment_method=pause` |
| `canceled` | Terminal; collection on open invoices disabled |

This is the state machine every developer tutorial copies. Note **`paused` is not a holiday-hold product** in Stripe the way Chargebee/Recurly pause is.

**Changes.** Update subscription items; `proration_behavior` (`create_prorations`, `none`, `always_invoice`). Credit proration deferral. Pause **collection** vs cancel. Backdating. Billing mode / mixed intervals (2025–2026 additions).

**Usage.** Billing Meters: event name + aggregation + attach to a metered Price. Invoice at period end. Not Orb.

**Invoices.** The center of the model. Every subscription period produces an Invoice + PaymentIntent. Hosted invoice page. Credit notes. `send_invoice` for net terms. Invoice drafts for upcoming.

**Dunning.** Smart Retries + email reminders + failed-payment settings (leave `past_due`, mark `unpaid`, or cancel). No Chargebee-quality campaign builder (this is a known limitation; branded dunning is why people leave).

**Tax.** Stripe Tax. Customer tax IDs. Not LHDN.

**Portal.** The bar. Update PM, invoices, cancel, optionally switch prices. Custom domain extra. White-label is limited — another reason people leave.

**Quotes.** Stripe Quotes → invoice or subscription. Not Salesforce CPQ.

**Entitlements.** Features on products; active when subscription is active. Look up by customer.

**Sandbox / import.** Test mode + test clocks. Dashboard migration toolkit (CSV customers/prices/PMs/subscriptions). This is the switching cost **off** Chargebee **onto** Stripe, and the thing we do not have **onto** Lazuar.

**Limitations founders already know (2026 recap articles).** Weak branded dunning editor, weak portal white-label, no multi-processor failover, usage UI is API-first, quote-to-cash is thin, percentage fee on Billing, meters not for AI firehoses, no Billplz.

**Implication for Lazuar.** Stripe Billing is both competitor and **dependency** (we vault and off-session through Stripe). We must never pretend our Commerce subscription **is** a Stripe Subscription object. Dual records (Lazuar `Subscription` + Stripe PM token, not Stripe Subscription) is the point of BYOK CaaS. The founder demand we cannot dodge: **invoices + portal + retries** at Stripe’s level of obviousness, even if the implementation is ours.

---

### Cross-engine map (what “table-stakes” means in 2026)

| Job | Stripe | Chargebee | Recurly | Maxio | Zuora | Lago | Orb |
|-----|:------:|:---------:|:-------:|:-----:|:-----:|:----:|:---:|
| Plan + price catalog | Y | Y | Y | Y | Y | Y | Y |
| Add-ons / extra items | Y | Y | Y | Y | Y | Y | Y |
| Coupons | Y | Y | Y | Y | Y | Y | Y |
| Trials as a state | Y | Y | P | Y | P | P | P |
| Future / scheduled start | P | Y | Y | Y | Y | P | P |
| Pause subscription | P | Y | Y | Y | Y | P | N |
| Cancel-at-term-end | Y | Y | Y | P | Y | P | P |
| Past due / unpaid | Y | Y | Y | Y | Y | Y | Y |
| Proration + preview | Y | Y | Y | Y | Y | P | Y |
| Scheduled changes / ramps | Y | Y | Y | P | Y | P | P |
| Usage / meters | Y | Y | Y | Y | Y | Y | Y |
| Hybrid seat+usage | Y | Y | Y | Y | Y | Y | Y |
| Invoice object | Y | Y | Y | Y | Y | Y | Y |
| Credit notes | Y | Y | Y | Y | Y | P | Y |
| One-off invoice / charges | Y | Y | Y | Y | Y | Y | Y |
| PM vault + off-session | Y | Y | Y | Y | Y | — | — |
| Smart / configurable dunning | Y | Y | Y | Y | Y | P | P |
| Hosted customer portal | Y | Y | Y | Y | P | P | P |
| Entitlements | Y | Y | N | N | P | Y | P |
| CPQ / quotes | P | Y | N | P | Y | N | N |
| RevRec | P | Y | P | Y | Y | N | N |
| MRR/ARR analytics | P | Y | Y | Y | Y | P | P |
| Multi-entity | P | Y | P | P | Y | P | P |
| Multi-currency prices | Y | Y | Y | Y | Y | Y | Y |
| Tax engine | Y | Y | Y | Y | Y | P | P |
| E-invoice (country) | P | P | N | N | P | N | N |
| Time machine / test clock | Y | Y | P | P | P | P | P |
| Import / migrate in | Y | Y | Y | Y | Y | P | P |
| WhatsApp dunning | N | N | N | N | N | N | N |
| LHDN MyInvois | N | N | N | N | N | N | N |
| Billplz / FPX BYOK | N | N | N | N | N | — | — |

`—` on Lago/Orb payments = they are payment-agnostic invoicers, not vaults.

The last three rows are **Lazuar’s only unique Y-column**. Everything above them is the expected checklist.

---

## Lazuar Commerce/Billing vs taxonomy (table)

### How to read this table

- Competitor columns use **Y / P / N / —** as in Method.
- **Lz** = Lazuar depth (`shipped` · `partial` · `stub` · `none` · `killed` · `doc_off` · `n/a`).
- **V** = verdict for Lazuar Pay (not Aura).
- **W** = suggested Lazuar billing wave (`B0`–`B6` or `—`). Defined after the table.
- Evidence paths are under `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/` unless noted.

**Waves (Lazuar Pay billing, not Aura salon waves 0–12):**

| Wave | Name | Gate |
|------|------|------|
| **B0** | Closed-loop honesty | Failed renewal → PAST_DUE → dunning → pay/cancel actually works in sandbox for Stripe **and** CHIP; Billplz path documented as reminder-only |
| **B1** | Lifecycle words founders already use | Trial, cancel-at-period-end, subscription pause ≠ dunning pause, FUTURE start |
| **B2** | Invoice as a customer-facing object | Numbered invoice the buyer can view/pay; credit note that is not “a ledger filter named Credit Notes” |
| **B3** | Plan change | Upgrade/downgrade + proration policy + scheduled change. Requires B2 (you cannot prorate without an invoice/credit) |
| **B4** | Usage | Only if a paying tenant has events. Event → line. Do not start with Orb SQL |
| **B5** | Entitlements + quote resurface | Feature flags on products; un-hide ADR 023 quotes when B2 exists |
| **B6** | Finance close | Wire `DeferredRevenueSchedule` producers; Xero; do not build Zuora Revenue |
| **—** | Never / N/A | Traps |

---

### Master comparison

| ID | Feature | St | Cb | Re | Mx | Zu | Lg | Ob | Lz | V | W |
|----|---------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|----|---|---|
| BE-001 | Plan / product catalog | Y | Y | Y | Y | Y | Y | Y | partial | Partial | B1 |
| BE-002 | Add-ons / extra subscription items | Y | Y | Y | Y | Y | Y | Y | none | Later | B3 |
| BE-003 | One-time charges / setup fees on a plan | Y | Y | Y | Y | Y | Y | Y | none | Later | B2 |
| BE-004 | Pricing models (tiered / volume / per-unit / stairstep) | Y | Y | Y | Y | Y | Y | Y | stub | Later | B4 |
| BE-005 | PWYW / minimum price actually applied | N | N | N | N | N | N | N | stub | Partial | B1 |
| BE-006 | Coupons (percent / fixed, caps, product scope) | Y | Y | Y | Y | Y | Y | Y | partial | Partial | B1 |
| BE-007 | Coupon duration (once / repeating / forever) | Y | Y | Y | Y | P | Y | P | none | Later | B3 |
| BE-008 | Trials (`trialing` / `in_trial`) | Y | Y | P | Y | P | P | P | none | Later | B1 |
| BE-009 | Trial without card + end behavior | Y | Y | Y | Y | P | P | N | none | Later | B1 |
| BE-010 | Status: future / scheduled start | P | Y | Y | Y | Y | P | P | none | Later | B1 |
| BE-011 | Status: trial | Y | Y | P | Y | P | P | P | stub | Later | B1 |
| BE-012 | Status: active | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-013 | Status: past_due | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-014 | Status: paused (subscription clock) | P | Y | Y | Y | Y | P | N | none | Later | B1 |
| BE-015 | Status: cancelled (immediate) | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-016 | Status: non_renewing / cancel-at-period-end | Y | Y | Y | P | Y | P | P | none | Later | B1 |
| BE-017 | Status: expired (terminal) | P | P | Y | Y | N | P | P | none | Later | B1 |
| BE-018 | Status: unpaid (retries exhausted, still open) | Y | P | P | Y | P | P | P | none | Later | B2 |
| BE-019 | Status: pending / incomplete first invoice | Y | P | N | Y | P | P | N | partial | Partial | B0 |
| BE-020 | Hard cancel now (admin + portal) | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-021 | Proration on plan/qty/price change | Y | Y | Y | Y | Y | P | Y | none | Later | B3 |
| BE-022 | Upgrade / downgrade API | Y | Y | Y | Y | Y | Y | Y | none | Later | B3 |
| BE-023 | Scheduled changes / ramps | Y | Y | Y | P | Y | P | P | none | Later | B3 |
| BE-024 | Quantity / seats on the subscription | Y | Y | Y | Y | Y | Y | Y | none | Later | B3 |
| BE-025 | Price override per subscriber | Y | Y | Y | Y | Y | Y | Y | none | Later | B3 |
| BE-026 | Usage ingest + meters | Y | Y | Y | Y | Y | Y | Y | none | Later | B4 |
| BE-027 | Hybrid subscription + usage | Y | Y | Y | Y | Y | Y | Y | none | Later | B4 |
| BE-028 | Prepaid usage credits (customer-facing) | P | Y | P | P | P | Y | Y | n/a | Never | — |
| BE-029 | Invoice as first-class object | Y | Y | Y | Y | Y | Y | Y | none | Later | B2 |
| BE-030 | Credit notes (allocable) | Y | Y | Y | Y | Y | P | Y | partial | Partial | B2 |
| BE-031 | One-off / custom line invoices | Y | Y | Y | Y | Y | Y | Y | partial | Partial | B2 |
| BE-032 | Hosted invoice pay page | Y | Y | Y | Y | P | P | Y | partial | Partial | B2 |
| BE-033 | Net terms / send_invoice (no auto-charge) | Y | Y | Y | Y | Y | P | P | partial | Partial | B2 |
| BE-034 | Receipt PDF / legal document | Y | Y | Y | Y | Y | P | Y | shipped | Both | B0 |
| BE-035 | Double-entry ledger (finance truth) | P | P | P | Y | Y | N | N | partial | Ours | B6 |
| BE-036 | Payment method vault | Y | Y | Y | Y | Y | — | — | partial | Partial | B0 |
| BE-037 | Off-session auto-renew | Y | Y | Y | Y | Y | — | — | partial | Partial | B0 |
| BE-038 | Configurable dunning campaigns | P | Y | Y | Y | Y | P | P | shipped | Ours | B0 |
| BE-039 | Smart retries / decline-code policy | Y | Y | P | P | P | N | N | none | Later | B2 |
| BE-040 | Dunning catch-up + max attempts | Y | Y | Y | Y | Y | P | P | shipped | Both | B0 |
| BE-041 | WhatsApp as a dunning channel | N | N | N | N | N | N | N | partial | Ours | B0 |
| BE-042 | Update-payment recovery checkout | Y | Y | Y | Y | P | P | P | shipped | Both | B0 |
| BE-043 | Pause / resume dunning (CS) | P | Y | Y | Y | Y | P | N | shipped | Both | B0 |
| BE-044 | MRR / ARR dashboard (honest) | P | Y | Y | Y | Y | P | P | partial | Partial | B2 |
| BE-045 | Expansion / contraction / churn MRR | P | Y | Y | Y | Y | N | N | none | Later | B6 |
| BE-046 | Revenue recognition schedules | P | Y | P | Y | Y | N | N | doc_off | Later | B6 |
| BE-047 | Quotes / CPQ | P | Y | N | P | Y | N | N | killed | Later | B5 |
| BE-048 | Feature entitlements (gating) | Y | Y | N | N | P | Y | P | none | Later | B5 |
| BE-049 | Workspace / app entitlements | — | — | — | — | — | — | — | shipped | Ours | — |
| BE-050 | Multi-business-entity | P | Y | P | P | Y | P | P | none | Never | — |
| BE-051 | Multi-currency price points | Y | Y | Y | Y | Y | Y | Y | stub | Later | B3 |
| BE-052 | Tax engine (Avalara / Stripe Tax) | Y | Y | Y | Y | Y | P | P | none | Never | — |
| BE-053 | LHDN e-invoice / SST liability | N | N | N | N | N | N | N | partial | Ours | B6 |
| BE-054 | Customer portal: list + cancel | Y | Y | Y | Y | P | P | P | shipped | Both | B0 |
| BE-055 | Customer portal: invoices + update PM | Y | Y | Y | Y | P | P | Y | partial | Partial | B2 |
| BE-056 | Customer portal: self-serve plan change | Y | Y | P | P | N | N | N | none | Later | B3 |
| BE-057 | Sandbox / test mode (gateways) | Y | Y | Y | Y | Y | Y | Y | partial | Partial | B0 |
| BE-058 | Test clock / Time Machine | Y | Y | P | P | P | P | P | none | Later | B1 |
| BE-059 | Import / migrate subscriptions in | Y | Y | Y | Y | Y | P | P | none | Later | B3 |
| BE-060 | Outbound subscription webhooks | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-061 | Fulfillment targets / unlock | P | P | N | N | P | P | P | shipped | Ours | B0 |
| BE-062 | Manual enroll + record offline payment | P | Y | Y | Y | Y | Y | P | shipped | Both | B0 |
| BE-063 | Subscriber CSV export | Y | Y | Y | Y | Y | Y | Y | shipped | Both | B0 |
| BE-064 | Become MoR / take GMV | N | N | N | N | N | N | N | n/a | Never | — |
| BE-065 | Clone Zuora amendments / Zuora Revenue | N | N | N | N | Y | N | N | n/a | Never | — |

Column key: **St** Stripe Billing · **Cb** Chargebee · **Re** Recurly · **Mx** Maxio · **Zu** Zuora · **Lg** Lago · **Ob** Orb · **Lz** Lazuar depth.

---

### Evidence notes (uncondensed, by family)

#### Catalog (BE-001–BE-009)

**What exists.** `Product` (`Modules/Commerce/Domain/Aggregates/Product.cs`) is a tenant-scoped buy-link: `Name`, `Slug` (unique per org), `Price`, `PricingModel` (default `FIXED`), `MinimumPrice`, `Currency`, `Interval`, `GatewayName`, `CheckoutConfiguration` (address / tax id / phone flags), `FulfillmentTargets` (URL list or `internal:…`). Intervals in the ops form are `one_time` | `mo` | `yr` (`ProductForm.tsx`). Currency is **hardcoded `"MYR"`** on submit. `pricing_model` can be set to `PWYW` in the form; `InitiateCheckoutCommandHandler` charges `product.Price * quantity` and never reads `PricingModel` / `MinimumPrice`.

There is **no** Plan/Addon/Charge split, **no** second item on a subscription, **no** setup fee field, **no** trial_days on the product, **no** per-currency price points.

**Coupons** (`Coupon.cs`) are the strongest catalog object: `PERCENTAGE` | `FIXED`, `MaxUses` with reserve/used split, `MinimumOriginalPrice`, `ExpiresAt`, optional `ApplicableProductIds`, archive, immutability of code/type/amount after first redeem. `ConfirmReservation()` now runs on paid completion (`GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs`), zero-amount checkout, and offline mark-paid. `CheckoutSessionExpiryJob` (every 5 minutes) expires `OPEN` sessions and is documented to release coupon reservations — the June 2026 “reserve leak” P0 is **addressed in source**. Still missing vs engines: repeating/forever duration, first-N-months, once-per-customer, stackability, coupon-on-subscription-change, currency-specific coupons.

**Trials.** No `trial_days` on Product. No `TRIALING` assignment anywhere except a defensive status list in `ClientProfileAnonymizedIntegrationEventHandler` (`ACTIVE|PAST_DUE|SUSPENDED|TRIALING|PENDING`). That is a **stub mention**, not a trial product.

**Founder translation.** “Plans” in Lazuar = checkout links. If they need seats, a setup fee, or a 14-day trial, we currently say “make another product” or “do it in your app.”

#### States and lifecycle (BE-010–BE-020)

**Implemented statuses** on `Subscription.Status` (stringly typed): `PENDING` → `ACTIVE` → `PAST_DUE` → `SUSPENDED` | `CANCELED`.

| Status | How you get there | How you leave |
|--------|-------------------|---------------|
| `PENDING` | Constructor | First successful payment / zero-amount / some manual paths `Activate` |
| `ACTIVE` | Activate, RecoverFromPayment, Resume, record-payment | Billing due + no vault → PAST_DUE; payment failed → PAST_DUE; admin/portal/dunning cancel → CANCELED; dunning grace + SUSPEND → SUSPENDED |
| `PAST_DUE` | `MarkAsPastDue` from BillingEngine (no token) or `GatewayPaymentFailedIntegrationEventHandler` | RecoverFromPayment / record-payment → ACTIVE; dunning CANCEL / admin cancel → CANCELED; dunning SUSPEND → SUSPENDED |
| `SUSPENDED` | Dunning final action | Resume / record-payment / update-payment success |
| `CANCELED` | `Cancel()` immediate | Terminal (no reactivate API) |

**Not implemented:** `FUTURE`, `TRIALING` as a real state, subscription `PAUSED`, `EXPIRED`, `UNPAID`, `NON_RENEWING`.

**Cancel is immediate.** `CancelAdminSubscriptionCommandHandler` and portal `CancelPortalSubscriptionCommand` call `subscription.Cancel()` now. Access webhooks fire `subscription.canceled` immediately. There is no `cancel_at_period_end`, no Recurly-style “canceled but live until term.” Portal copy says “Cancel Plan” (`portal/page.tsx`).

**PENDING** is the closest thing to Stripe `incomplete`, but we do not expire it after 23 hours as a subscription state (checkout sessions expire; leftover PENDING subs can linger — old gap, still structurally true).

**Webhook contract** (`webhooks.tsp`) only admits `ACTIVE | PAST_DUE | CANCELED | SUSPENDED`. Adding trial/pause/expired **is a contract change**, not just a column.

#### Proration and changes (BE-021–BE-025)

There is **no** change-plan command, **no** proration math, **no** pending change row, **no** quantity column on `Subscription`. Checkout `Quantity` multiplies the first charge and is passed to the gateway query; it is **not** stored on the subscription. Renewals always charge `product.Price` (`BillingEngineJob` → `ExecuteOffSessionCharge` with `product.Price`).

`RecordSubscriberPaymentCommandHandler` advances `+1 month` or `+1 year` from **now**, not from `CurrentPeriodEnd`. That is offline collection, not a plan change.

To “upgrade” today: cancel (immediate) + new checkout. That is unacceptable to any founder who has used Recurly for ten minutes.

#### Usage (BE-026–BE-028)

No events table, no billable metric, no meter API, no usage line on renewal. Tenant **utility credits** (`TenantCreditBalance`) are **Lazuar’s own prepaid wallet** for WhatsApp/LHDN actions — not customer usage credits. Scoring BE-028 as **Never** means: do not reuse the tenant wallet as “AI tokens for their users.” Different plane.

#### Invoices, credit notes, one-offs (BE-029–BE-035)

**There is no `Invoice` aggregate in Commerce.** Money objects are:

| Object | What it is |
|--------|------------|
| `CheckoutSession` | 24h payment intent (product or ad-hoc lines) |
| `Order` | Minimal one-time completion (amount, currency, COMPLETED/REFUNDED) |
| `CommerceTransactionLog` | Ops payment log |
| `Billing.LedgerEntry` + `LedgerLine` | Double-entry journal after the fact |
| QuestPDF + R2 | Receipt / draft proforma |

`InvoiceIssuedIntegrationEvent` has a **handler** that books AR + deferred revenue (`InvoiceIssuedHandler`) and an Lhdn consumer — **no in-repo Commerce publisher**. Custom checkouts are the “quote”: ad-hoc line items, optional B2B flag, ops `QuotesPage` / `CreateQuoteModal` / `QuoteView`. **Routes are `[MVP-HIDE]`** (ADR 023). Portal `/pay/[sessionId]` was lobotomized. So quotes are **killed UI + live API** (`POST /admin/commerce/custom-checkouts`).

Credit notes: ops `CreditNotesPage` lists ledger entries with `type_filter=reversals`. LHDN has real UBL CreditNote + SelfBilledCredit strategies and a refund handler that can emit a credit note after 72h. That is **compliance credit note**, not Chargebee “allocate this CN to invoice INV-1042.”

Receipts: **shipped**. Draft proforma HMAC URL is in Billing TypeSpec. Final signed PDF is intentionally **not** in TypeSpec (redirect-only; honesty allowlist). Portal “Download Tax Invoice” is commented out.

Ledger: real balanced posts for gateway payment/refund, manual enroll, zero-amount, LHDN cancel reverse. README still over-claims “terminal sink” (it publishes `DocumentPublished` / `ConsolidatedInvoiceIssued`). RevRec job **unregistered**. This is **Ours** as a wedge (founders in MY do not get a balanced SST ledger from Stripe), **Partial** as RevRec.

#### Vault, retries, dunning (BE-036–BE-043)

**Vault.** `Subscription.VaultedCustomerId` + `VaultedTokenId`. Stripe and CHIP Collect implement off-session. Billplz is checkout-only (`NotSupportedException` / ops copy: cannot vault). Razorpay adapter exists (treat as partial). Admin can generate a **Stripe Customer Portal** link (`POST /admin/commerce/subscribers/portal-link`) — Stripe-hosted PM update, not Lazuar portal.

**Renewal.** `BillingEngineJob`: `FOR UPDATE SKIP LOCKED`, batch 50, attempt 1 only, then Payments `ExecuteOffSessionCharge`. No token → `PAST_DUE` + `subscription.past_due` (event name **fixed** vs the old `subscription.suspended` bug).

**Failure bridge (closed in current tree).** `ProcessGatewayWebhookCommandHandler` publishes `GatewayPaymentFailedIntegrationEvent` on `PAYMENT_FAILED`. Off-session handler also publishes on charge failure. Commerce `GatewayPaymentFailedIntegrationEventHandler` marks the pending `ChargeAttemptLog` failed, flips `PAST_DUE`, assigns a campaign (same targeting as the engine), emits `subscription.past_due` once. This is the June 2026 P0, **done**.

**Retries.** Unique index is now `(SubscriptionId, TargetBillingDate, AttemptNumber)` — multi-retry is possible. Cap `ChargeAttemptLimits.MaxAttemptsPerBillingCycle = 4` (billing owns 1, dunning AUTO_CHARGE owns 2–4). No decline-code matrix, no Smart Retries, no SCA special case.

**Dunning engine (current).** Hourly (configurable interval). Claim/lock batches. Pre-dunning: negative offsets, catch-up (`|DayOffset| <= daysUntilDue`), communication only. Past-due: assign campaign by priority + product + ONLINE_GATEWAY/MANUAL; catch-up steps `0 <= DayOffset <= daysOverdue` not yet logged; AUTO_CHARGE or EMAIL/WHATSAPP/ALL; WhatsApp demoted to email if `Messaging:WhatsAppEnabled=false`; grace → `CANCEL` (typed `SubscriptionCanceled` + `RecordChurn`) or `SUSPEND` (typed `SubscriptionSuspended`). `LastCompletedDayOffset` is written (old dead `CurrentDunningStepIndex` is now synced). Payload includes `plan_name`, `amount`, `currency`, `days_overdue`. Communications hydrator substitutes those plus magic portal link and update-payment URL.

**Still not Chargebee:** no campaign version snapshot (edit still `ClearSteps` + new GUIDs; catch-up keys on `DayOffset` so re-keying is less catastrophic than the old `step.Id` equality, but in-flight copy can change); no time-of-day / merchant TZ; no SMS; no interactive WhatsApp; default seeded campaign is message-heavy; AUTO_CHARGE still charges **list `product.Price`**, not an open invoice balance.

**CS tools.** Pause/resume dunning shipped (admin + ops UI). No force-run, no skip-step, no per-sub timeline API.

#### Analytics and RevRec (BE-044–BE-046)

`CommerceQueryService.Stats.cs`: MRR = sum of `product.Price` for `ACTIVE|PAST_DUE`, yearly `/12`. Includes past-due as MRR (Chargebee would argue). Ignores coupons, comps, quantity, currency mix. Churn = canceled-in-30 / reconstructed active-30-days-ago. Revenue KPIs from `TransactionLogs` CONFIRMED (not Billing ledger). Cash-flow trend 6 months. Payment-method breakdown is **`RecordedByName`**, not a gateway enum.

Billing `GET /admin/billing/summary` and `/net-profit` exist. Historical ABS-on-signed-lines risk was called out in the old gap doc; treat summary as **Partial** until reversals are proven.

`DeferredRevenueSchedule` + `Recognize()` exist. **No producer.** Job **not registered**. `doc_off`.

#### Quotes / CPQ (BE-047)

Custom checkout = line-item payment request. Ops Quotes UI + portal QuoteView exist as code. Nav hidden. No approvals, no ramps, no Salesforce, no convert-quote-to-subscription (a custom checkout does **not** create a recurring `Subscription` on pay — it completes a session / order-ish path). Verdict **Later** only as “un-hide and finish B2B payment links,” not “build Chargebee CPQ.”

#### Entitlements (BE-048–BE-049)

`One.TenantAppEntitlement` (`AppId` + `IsActive`) gates **Lazuar modules** for a workspace (e.g. grant `PAYMENTS` on Aura provision). `AppEntitlementGrantedIntegrationEvent` seeds starter credits. This is **Ours** as platform IAM.

It does **not** answer “does subscriber X have feature `sso`.” Fulfillment is outbound webhooks + `internal:` apps. Founders will still build their own entitlements table unless we add BE-048.

#### Multi-entity, currency, tax (BE-050–BE-053)

Workspaces (`one.Organizations`) are multi-tenant isolation, **not** Chargebee Multi Business Entity (one customer billed by two legal entities with two tax IDs). **Never** unless a real holding-company tenant appears.

`Product.Currency` exists; ops UI writes `MYR` only. Ledger lines have original + base currency. No FX gain/loss. No price-per-currency catalog.

Tax: we will **not** build Stripe Tax / Avalara (**Never** — wrong geography and a sink). We **will** keep LHDN + SST liability accounts (**Ours**, Partial until B2C consolidation and TIN checkout are productized again). ADR 023 hid TIN collection. Consolidation job exists and README claims the B2C_RECEIPT filter was fixed; still a finance-path feature, not a subscription-tax engine.

#### Portal (BE-054–BE-056)

Shipped: magic-token portal lists subs + orders; cancel plan posts to `/{tenantSlug}/portal/cancel`; update-payment flow (`/update-payment/{subId}`, arrears + checkout). Admin Stripe portal-link.

Missing vs Stripe Customer Portal: invoice list/download (hidden), in-portal PM update for non-Stripe, plan change, pause, upcoming invoice preview. `requestMagicLink` / `getBillingLink` were historically specified; current `public-routes.tsp` shows portal GET + cancel only (magic-link/billing-link **removed from the public contract** — do not claim them).

#### Sandbox, import (BE-057–BE-059)

Billplz sandbox host + `App:BillplzEnvironment`. Stripe/CHIP test keys are BYOK (tenant’s test mode). No Time Machine. No CSV import of subscribers with period anchors, trials, vaulted PMs. Manual enroll exists (reminder-only). Export exists (10k cap CSV). Switching **from** Chargebee/Stripe Billing **to** Lazuar is currently a professional-services fantasy.

#### Developer / fulfillment (BE-060–BE-063)

Outbound catalog is frozen and implemented: `subscription.activated|resumed|past_due|canceled|suspended` plus order/payment_link types that SaaS URLs are told to ignore. HMAC `X-Lazuar-Signature`. This is **Both** (every engine has webhooks) and **Ours** in the specific “unlock my app without `subscription.updated`.”

Manual enroll, record-payment, export, admin cancel: **shipped** (the old “UI calls missing routes” gap is closed in `SubscriberEndpoints.cs`).

#### Traps (BE-064–BE-065)

Do not become MoR. Do not implement Zuora amendment versioning or Zuora Revenue. Those are company-shape mistakes, not missing checkboxes.

---

### Honesty delta vs `docs/001-gaps` (so we do not re-litigate closed bugs)

The July 2026 dunning/commerce gap memos are **stale in several P0s**. Current tree:

| Old P0 | Current source |
|--------|----------------|
| Vaulted failure never sets PAST_DUE | `GatewayPaymentFailedIntegrationEventHandler` + webhook `PAYMENT_FAILED` publish |
| ChargeAttempt unique-on-date blocks retries | Unique `(SubscriptionId, TargetBillingDate, AttemptNumber)` |
| `{{plan_name}}` not filled | Dispatch payload + Communications replace |
| Portal cancel missing | `PublicPortalEndpoints` + command |
| Admin cancel / record-payment / export missing | `SubscriberEndpoints` |
| Coupon confirm only on zero-amount | Confirm on paid open-checkout + expiry job releases |
| PAST_DUE event named `subscription.suspended` | `subscription.past_due` |
| `CurrentDunningStepIndex` dead | Synced from `LastCompletedDayOffset` |
| Exact-day match only | Catch-up `DayOffset <= daysOverdue` |
| No typed cancel/suspend from dunning | Publishes `SubscriptionCanceled` / `SubscriptionSuspended` |

**Still open (do not mark shipped):** invoice object, trials, proration/plan change, usage, RevRec producer, campaign versioning, decline-code retries, WhatsApp as a production Meta utility channel, ADR 023 hidden quotes/invoices.

---

## What SaaS founders will demand

This section is demand, not a backlog. Three ICPs buy “billing.” They do not want the same twelve families equally.

### ICP A — MY/SEA seller of a simple recurring thing

Examples: community, course membership, indie SaaS at RM 49–199/mo, agency retainer billed monthly, “pay this Billplz link every month.”

**They will demand, in order:**

1. **A link that charges and renews** without them writing Stripe Subscription code. (We have buy links + hourly renew for vaulted Stripe/CHIP.)
2. **FPX / Billplz** for customers who will not put a card on file. (We have Billplz checkout. We do **not** have Billplz auto-renew. They need reminder dunning + a pay link. That is a product they will love **if we say it out loud**.)
3. **WhatsApp + email when the charge fails**, in BM/EN, with a button-shaped URL. (Partial. Email path is real. WhatsApp is gated and not interactive-template-complete.)
4. **Cancel and “I paid already”** for the owner. (Admin cancel + record-payment: shipped.)
5. **A receipt that does not look illegal.** (PDF receipts shipped. Tax invoice download hidden.)
6. **LHDN when the accountant shows up.** (Backend Partial; UI killed. This is the 2026–2027 buying criterion for any MY company past RM 500k.)

**They will not demand:** Orb SQL meters, Chargebee CPQ, Zuora amendments, multi-entity, ASC 606 schedules, seat proration.

**Churn risk if we pretend to be Chargebee:** they will drown in empty catalog objects. Keep the checkout-link mental model.

### ICP B — Global/SEA SaaS founder who already opened Stripe Billing

Examples: a Next.js SaaS with a Pro plan, annual discount, 14-day trial, `LAUNCH30`, customer portal.

**They will demand, and will walk if missing:**

1. **Trials** with a clear `trialing` → `active` or `past_due` story, with and without a card.
2. **Cancel at period end** (they will call our immediate cancel a bug).
3. **Invoices** they can show a customer and an accountant. Numbered. Line items. Open/paid.
4. **Customer portal** that updates the card **and** lists invoices, not only “Cancel Plan.”
5. **Upgrade/downgrade** with a preview amount. Even a brutal v1 (“immediate, prorate both ways”) beats “create a new checkout.”
6. **Coupons that last three months**, not only first invoice.
7. **A test clock** or at least a “run billing now for this sub” button. They will not wait for the hourly job.
8. **Import** if they are leaving Stripe/Chargebee. Without it we only win greenfield.

**They will tolerate later:** usage, CPQ, RevRec, entitlements (they will hack entitlements in their app from our webhooks — we already document that).

**They will reject:** unexplained MYR-only, unexplained missing invoice object (“so where is the invoice?”), dunning that emails `{{plan_name}}` (this one is fixed), dual IDs where `subscription_id` in gateway metadata is sometimes a checkout session.

### ICP C — Usage / AI / infra (Orb/Lago shoppers)

**They will demand:** event ingest, metrics, credits, upcoming invoice, alerting, simulation.

**We should not build this to win them.** We should say: compose. Use Lago/Orb/Stripe Meters for rating; use Lazuar for **MY checkout + LHDN + WhatsApp recovery** if that sentence is ever true. BE-026 is Later **only** after a paying tenant has events. Scoring usage as table-stakes for Lazuar is how we become a bad Orb.

### ICP D — Sales-led B2B (Chargebee CPQ / Zuora shoppers)

**They will demand:** quotes, approvals, ramps, Net 30 invoices, account hierarchy, RevRec, Salesforce.

**ADR 023 already hid the baby version of this.** Un-hide only after B2 (real invoices). Do not build Salesforce CPQ. A finished **custom checkout + TIN + e-invoice + credit note** is the MY version of CPQ. That is a wedge. Full Chargebee CPQ is a **Never** for a solo-scale CaaS.

### What every ICP will demand (the actual table-stakes)

Regardless of ICP, 2026 founders who have touched any engine will assume:

| Demand | Why they assume it | Lazuar today |
|--------|--------------------|--------------|
| A named subscription state machine they can switch on | Stripe docs | 5 statuses, no trial/pause/expiry |
| Failed payment → customer is told → retry → cancel or restore | Stripe Smart Retries + every SaaS they use | Loop exists for vaulted Stripe/CHIP; Billplz is reminder-shaped |
| Self-serve card update + cancel | Stripe Customer Portal | Cancel yes; card update = Stripe portal or update-payment checkout |
| An invoice PDF with a number | Every accountant | Receipt yes; invoice object no; tax invoice UI hidden |
| Webhooks that mean “unlock / lock” | Every engine | Frozen catalog shipped |
| Sandbox that is not production Billplz | Stripe test mode | Partial; easy to get wrong |

### What SaaS founders will *not* forgive (honesty bugs)

These are worse than missing features:

1. **Calling custom checkouts “quotes” and ledger reversals “credit notes” in the UI** while the objects are not invoices/CNs. ADR 023 hid this — keep it hidden until B2.
2. **MRR that counts PAST_DUE list price** and ignores coupons. Founders will screenshot our dashboard against Stripe.
3. **PWYW in the product form that does not change checkout.**
4. **Currency field in the API, MYR hardcoded in ops.**
5. **WhatsApp dunning in marketing while `Messaging:WhatsAppEnabled` is false and there is no Meta template.**
6. **RevRec language in Billing README** while the job is unregistered.
7. **`subscription_id` metadata that is a checkout session id** on first payment. Integrators will store the wrong GUID. (Known historical footgun; treat as a B0 honesty fix if still present on the first-payment path.)

### What we should refuse even if founders ask

| Ask | Why refuse |
|-----|------------|
| “Be our MoR like Paddle” | Different company; Aura already uses Paddle for System A |
| “Multi-entity like Chargebee Enterprise” | Holding-company software; not CaaS MVP |
| “Zuora-style amendments / Expired versions” | Reporting poison; we are not a close platform |
| “Avalara for 50 countries” | We are the LHDN company |
| “SQL meters / 200k events/s” | Orb exists |
| “Salesforce CPQ connector” | Implementation death |
| “Reuse tenant WhatsApp credits as customer AI credits” | Plane collapse; BE-028 Never |
| “Membership billing for Aura salon visits” | Aura `CM-010` Never; different product |

### Sequencing implication (demand → wave)

```
B0  Close loops + honesty (Billplz reminder narrative, metadata IDs, WA gate honesty)
B1  Words: trial, cancel-at-period-end, pause, future, test-run-now
B2  Invoice object + portal invoices + credit notes that are real
B3  Plan change + proration + quantity + import
B4  Usage only with a tenant
B5  Entitlements + un-hide quotes against real invoices
B6  RevRec producer + LHDN UI resurface + Xero
```

Do not start B3 without B2. Recurly’s entire change model **emits invoices and credit notes**. Prorating into `product.Price` on the subscription row will create a second generation of gap memos.

---

## Tracker IDs

New family **`BE`** — Billing Engine capabilities for **Lazuar Pay as a product**. Do not reuse Aura `SA-*` (Paddle Plan desk) or `PY-*` (guest Billplz). Promote into [`00-checklist-tracker.md`](./00-checklist-tracker.md) with these IDs; do not invent a second taxonomy.

**Job class:** `table-stakes` · `differentiator` · `later-nice` · `hygiene` · `trap`

**Priority** is 0 = first inside the wave.

| ID | Feature | Lz now | V | W | P | Class | Why / evidence |
|----|---------|--------|---|---|--:|-------|----------------|
| BE-001 | Plan / product as a commercial offer (not only a buy link) | partial | Partial | B1 | 2 | table-stakes | `Product.cs` is a link; engines have plans |
| BE-002 | Add-ons / extra items on one subscription | none | Later | B3 | 2 | later-nice | Wait for plan change |
| BE-003 | Setup fee / one-time charge on subscribe | none | Later | B2 | 2 | later-nice | Needs invoice lines |
| BE-004 | Tiered / volume / per-unit catalog prices | stub | Later | B4 | 3 | later-nice | `PricingModel` unused |
| BE-005 | PWYW honored at checkout | stub | Partial | B1 | 1 | hygiene | Form lies today |
| BE-006 | Coupons percent/fixed + caps + product scope | partial | Partial | B1 | 2 | table-stakes | Domain strong; duration missing |
| BE-007 | Coupon repeating / forever | none | Later | B3 | 2 | later-nice | Stripe/Chargebee default ask |
| BE-008 | Trial period on product + `TRIALING` state | none | Later | B1 | 0 | table-stakes | ICP B walks without this |
| BE-009 | Trial without card + end behavior | none | Later | B1 | 1 | table-stakes | Stripe `pause` / convert |
| BE-010 | Future / scheduled start | none | Later | B1 | 2 | later-nice | Maxio `awaiting_signup_date` |
| BE-011 | First-class trial status in API + webhooks | stub | Later | B1 | 0 | table-stakes | Contract change |
| BE-012 | Active status | shipped | Both | B0 | 3 | table-stakes | Exists |
| BE-013 | Past due status + webhook | shipped | Both | B0 | 0 | table-stakes | Failure bridge shipped |
| BE-014 | Pause subscription (not dunning pause) | none | Later | B1 | 1 | table-stakes | Recurly/Chargebee pause |
| BE-015 | Immediate cancel | shipped | Both | B0 | 2 | table-stakes | Admin + portal |
| BE-016 | Cancel-at-period-end / non_renewing | none | Later | B1 | 0 | table-stakes | Highest ICP B complaint |
| BE-017 | Expired terminal state | none | Later | B1 | 3 | later-nice | Recurly Expired |
| BE-018 | Unpaid (retries exhausted) | none | Later | B2 | 2 | later-nice | Stripe/Maxio |
| BE-019 | PENDING / incomplete first payment | partial | Partial | B0 | 2 | hygiene | Linger risk |
| BE-020 | Reactivate canceled (optional) | none | Later | B1 | 3 | later-nice | Recurly before expiry |
| BE-021 | Proration policy | none | Later | B3 | 0 | table-stakes | Requires BE-029 |
| BE-022 | Upgrade / downgrade | none | Later | B3 | 0 | table-stakes | Requires BE-021 |
| BE-023 | Scheduled change / ramp | none | Later | B3 | 2 | later-nice | Chargebee ramps |
| BE-024 | Seats / quantity on subscription | none | Later | B3 | 1 | table-stakes | Checkout qty is lost |
| BE-025 | Per-sub price override | none | Later | B3 | 2 | later-nice | Enterprise deals |
| BE-026 | Usage events + meters | none | Later | B4 | 0 | later-nice | Pain-gated |
| BE-027 | Hybrid seat + usage | none | Later | B4 | 1 | later-nice | After BE-026 |
| BE-028 | Customer prepaid usage credits | n/a | Never | — | — | trap | Do not reuse tenant wallet |
| BE-029 | Invoice aggregate (number, lines, status, due) | none | Later | B2 | 0 | table-stakes | The word “billing” |
| BE-030 | Allocable credit notes | partial | Partial | B2 | 1 | table-stakes | LHDN CN ≠ Chargebee CN |
| BE-031 | One-off invoice (finish custom checkout) | partial | Partial | B2 | 1 | table-stakes | Hidden quotes |
| BE-032 | Hosted invoice pay page | partial | Partial | B2 | 2 | table-stakes | Update-payment is the slice |
| BE-033 | Net terms / manual collection as a mode | partial | Partial | B2 | 2 | differentiator | Reminder-only + Billplz |
| BE-034 | Receipt / proforma PDF | shipped | Both | B0 | 3 | table-stakes | Keep |
| BE-035 | Double-entry ledger as finance truth | partial | Ours | B6 | 1 | differentiator | Do not abandon |
| BE-036 | PM vault (Stripe/CHIP) | partial | Partial | B0 | 1 | table-stakes | Billplz cannot |
| BE-037 | Off-session renew | partial | Partial | B0 | 0 | table-stakes | Closed loop soak |
| BE-038 | Configurable dunning campaigns | shipped | Ours | B0 | 1 | differentiator | Builder + WhatsApp option |
| BE-039 | Decline-code / smart retry policy | none | Later | B2 | 2 | later-nice | After invoice |
| BE-040 | Catch-up + 4 attempts | shipped | Both | B0 | 2 | table-stakes | Index fixed |
| BE-041 | WhatsApp dunning (honest) | partial | Ours | B0 | 1 | differentiator | Gate + templates |
| BE-042 | Update-payment recovery | shipped | Both | B0 | 1 | table-stakes | Exists |
| BE-043 | Pause dunning | shipped | Both | B0 | 3 | table-stakes | CS tool |
| BE-044 | Honest MRR (exclude past_due? net of coupons) | partial | Partial | B2 | 1 | hygiene | Stats.sql today |
| BE-045 | Expansion / contraction MRR | none | Later | B6 | 2 | later-nice | Needs plan change |
| BE-046 | Deferred revenue schedules live | doc_off | Later | B6 | 0 | later-nice | Parked on purpose |
| BE-047 | Quotes UI resurfaced against invoices | killed | Later | B5 | 1 | later-nice | ADR 023 |
| BE-048 | Feature entitlements API | none | Later | B5 | 0 | later-nice | Webhooks suffice until then |
| BE-049 | Workspace app entitlements | shipped | Ours | — | — | hygiene | One module |
| BE-050 | Multi-business-entity | none | Never | — | — | trap | Chargebee Enterprise |
| BE-051 | Multi-currency price points | stub | Later | B3 | 3 | later-nice | Stop hardcoding MYR first |
| BE-052 | Global tax engine | none | Never | — | — | trap | Avalara envy |
| BE-053 | LHDN / SST path productized | partial | Ours | B6 | 0 | differentiator | Real moat |
| BE-054 | Portal list + cancel | shipped | Both | B0 | 2 | table-stakes | Exists |
| BE-055 | Portal invoices + PM update | partial | Partial | B2 | 0 | table-stakes | Stripe portal-link only |
| BE-056 | Portal self-serve plan change | none | Later | B3 | 2 | later-nice | After BE-022 |
| BE-057 | Gateway sandbox honesty | partial | Partial | B0 | 0 | hygiene | Billplz env |
| BE-058 | Test clock / run-engine-now | none | Later | B1 | 1 | hygiene | QA of B1 |
| BE-059 | Subscription import toolkit | none | Later | B3 | 3 | later-nice | Switching cost |
| BE-060 | Frozen outbound subscription webhooks | shipped | Both | B0 | 1 | table-stakes | Keep no `updated` |
| BE-061 | Fulfillment / unlock targets | shipped | Ours | B0 | 2 | differentiator | CaaS point |
| BE-062 | Manual enroll + record payment | shipped | Both | B0 | 2 | table-stakes | Exists |
| BE-063 | Subscriber CSV export | shipped | Both | B0 | 3 | hygiene | Exists |
| BE-064 | MoR / GMV take-rate | n/a | Never | — | — | trap | T4 money-plane |
| BE-065 | Zuora amendments / Zuora Revenue clone | n/a | Never | — | — | trap | Company-shape |
| BE-066 | Campaign version snapshot (immutability) | none | Later | B2 | 3 | hygiene | Edit-safe dunning |
| BE-067 | Force-retry / skip-step / dunning timeline API | none | Later | B2 | 3 | later-nice | CS depth |
| BE-068 | Fix first-payment metadata ID collision | partial | Partial | B0 | 0 | hygiene | session vs subscription |
| BE-069 | Document Billplz as reminder-only renewals | partial | Partial | B0 | 0 | hygiene | Ops copy exists; product claim must match |
| BE-070 | Merchant timezone + dunning time-of-day | none | Later | B2 | 3 | later-nice | UTC day skew |

### Suggested first promotion set (if a tracker is opened this week)

Promote **only** rows that are either honesty (B0) or words ICP B will ask in a sales call (B1). Do not promote usage/CPQ/RevRec into Wave B0.

**B0 (this quarter, if anything):** BE-013 (already shipped — keep as soak), BE-037, BE-041, BE-057, BE-068, BE-069, BE-005 (stop lying about PWYW), BE-044 (label MRR honestly or fix).

**B1 (next product epic):** BE-008, BE-016, BE-014, BE-058.

**B2 (the real “we have billing” epic):** BE-029, BE-030, BE-055.

**Never:** BE-028, BE-050, BE-052, BE-064, BE-065.

### Mapping to Aura tracker (do not collide)

Aura is a **Hub customer**, not a column in this file. If an Aura tracker still uses these IDs, do not reuse them for salon jobs:

| Aura ID | Plane | Relation to this file |
|---------|-------|------------------------|
| `SA-001`–`SA-006` | Aura System A / Paddle | Unrelated. Salon pays Aura. |
| `SA-007` Replace Paddle with Hub | Trap | Still Never. This file’s `BE-*` are **Lazuar’s product for other SaaS**, not Aura Pro billing. |
| `PY-*` | Aura guest → salon | Unrelated checkout of a haircut. |
| `CM-010` Membership billing like Mindbody | Trap | Do not implement BE-* inside Aura Beauty visits. |

Promote into `plans/007-feats/00-checklist-tracker.md` using **`BE-001`…`BE-070`**. If a later analysis finds a missing engine job, append `BE-071+`. Do not start a `CB-` / `SUB-` / `BILL-` family.

---

*Sources: Chargebee Features and Billing 2.0 documentation (subscriptions, entitlements, usage, invoices, CPQ, MBE, Time Machine) as of mid-August 2026; Recurly subscription dashboard, change-subscription, pause, expire, usage-based billing docs; Maxio “Subscription States” and dunning help center; Zuora Billing / CPQ / Revenue docs and 2026.Q2–Q3 release notes; Lago product site and GitHub positioning; Orb core-concepts and enterprise billing docs; Stripe Billing subscription overview and status table; Lazuar Pay repo modules Commerce, Billing, Payments, One, Communications, Lhdn, ops, portal, and TypeSpec as of 16 August 2026. No production soak was run for this file.*
