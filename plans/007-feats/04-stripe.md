# 04 — Stripe vs Lazuar Pay

**Program:** `plans/007-feats` — competitor-feature research for **Lazuar Pay** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`), public host `hub.lazuar.com`.  
**Date:** 16 August 2026.  
**Status:** Full uncondensed analysis. **No product code from this file.** Tracker only.  
**Subject:** Stripe as **upstream rail (BYOK)** and as **direct competitor** (Payment Links, Checkout, Billing, Tax, Customer Portal, Radar, Connect, and the rest of the 2026 product surface).  
**ID authority:** [`20-sequencing-and-tracker-schema.md`](./20-sequencing-and-tracker-schema.md). This file **does not invent a second taxonomy**. Stripe wrap/rebuild/refuse decisions promote into existing `LP-PAY-*`, `LP-COM-*`, `LP-DUN-*`, `LP-TAX-*`, `LP-DEV-*`, `LP-UX-*`, `LP-OPS-*`, `LP-TRU-*`, `LP-XX-*` rows.  
**Standing locks (do not contradict):** ADR 019 BYOK not MoR. ADR 021 Compliance CaaS. ADR 023 Pure CaaS UI lobotomy. Buyer GMV on tenant Stripe/Billplz/CHIP is **not** Lazuar’s SaaS fee. Aura salon is a **customer** of Hub, not a competitor. Wrap rails — do not rebuild acquiring.

**Honesty rule:** a missing Stripe capability in Lazuar is not automatically a rebuild ticket. Most of Stripe is a processor. Lazuar is a sovereign checkout / billing / compliance engine that already **calls** Stripe. Gaps that belong on Stripe’s side of the PCI / network / fraud / tax-engine fence must be **wrapped**. Gaps that are Malaysian compliance, multi-gateway orchestration, LHDN, or WhatsApp recovery may be **rebuilt**. Gaps that are US/EU banking products, Atlas, Treasury, Issuing, Identity, marketplace Connect, or Merchant of Record must be **refused**.

Sibling contrast: [`05-malaysia-gateways.md`](./05-malaysia-gateways.md) owns Billplz / CHIP / Toyyib / Fiuu / iPay88. This file owns Stripe the company. Do not duplicate 05’s MDR tables. Do not treat Stripe FPX as the cheap Malaysian rail — that is Billplz/CHIP.

---

## Method

### What this file is answering

Three questions, in this order:

1. **What is Stripe selling in August 2026**, as documented on `stripe.com` / `docs.stripe.com`, including Sessions 2026 (29 April 2026, 288 announcements) and the public Malaysia pricing page?
2. **What does Lazuar Pay already do with Stripe**, in code, not in README claims?
3. **For each Stripe capability, must Lazuar match it, wrap it, or ignore it?** And which official `LP-*` row does that decision live on?

This is not an Aura salon-floor comparison. This file is about **Lazuar Pay itself** versus Stripe the company. Aura implications appear only where a wrap/rebuild decision leaks into Hub-as-cashier for a first-party integrator. They do not reopen Paddle, marketplace take-rate, or Hub-as-MoR.

### Sources (primary, fetched 16 August 2026)

| Source | URL / path | What it locked |
|--------|------------|----------------|
| Stripe global availability | https://stripe.com/global | Malaysia is a full self-serve Payments country. Indonesia and India are **Preview** (sales contact). Treasury fiat is not a MY product. Atlas is worldwide US incorporation, not SSM. |
| Stripe Malaysia pricing | https://stripe.com/en-my/pricing | Domestic cards **3% + RM1.00**. International +1%. FX +2%. FPX **3% + RM1.00**. GrabPay **3%**. Alipay **2.9% + RM1.00**. Radar Lite included. Billing **0.7% of Billing volume**. Invoicing **0.4%**. Tax, Sigma, Data Pipeline, Revenue Recognition, Radar for Fraud Teams, Atlas, Workflows, Terminal hardware, Connect, Customer Portal custom domain all listed for MY accounts. |
| FPX docs | https://docs.stripe.com/payments/fpx | MY merchants only. MYR only. No recurring. No SetupIntents. No Checkout subscription/setup mode. Refunds yes (async, up to 60 days, can take up to 6 weeks). Disputes no. Connect yes. Payout **5 business days**. BRN required. |
| GrabPay docs | https://docs.stripe.com/payments/grabpay | MY + SG merchants. MYR/SGD. No recurring. Checkout subscription/setup mode unsupported. Express Checkout Element unsupported. Refunds yes (up to 90 days). Disputes no. |
| Payment method support matrix | https://docs.stripe.com/payments/payment-methods/payment-method-support | Country / currency / product / API support for every APM. FPX, GrabPay, Link, cards, Apple Pay, Google Pay called out below. |
| Stripe Tax countries | https://docs.stripe.com/tax/supported-countries | Malaysia: **digital products only**, tax type **Service Tax**, **business location ❌**, **customer location ✓**. Remote seller, no MY permanent establishment. Threshold MYR 500,000 / 12 months. |
| Malaysia Tax detail | https://docs.stripe.com/tax/supported-countries/asia-pacific/collect-tax?tax-jurisdiction-asia-pacific=malaysia | SST-on-digital-services only; mystods.customs.gov.my. No LHDN MyInvois. No SST-02 return filing. No 8% SST on goods. |
| Billing + Smart Retries | https://docs.stripe.com/billing · https://docs.stripe.com/billing/revenue-recovery/smart-retries | Subscriptions, invoices, quotes, Metronome usage, Customer Portal, Smart Retries (recommended default 8 tries / 2 weeks), hard-decline list, local-PM retries (ACH/SEPA/BECS/Bacs — **not FPX/GrabPay**). |
| Checkout | https://docs.stripe.com/payments/checkout | Hosted full page, embedded form (preview), Elements on Checkout Sessions. Adaptive Pricing, Tax, Link, Managed Payments, upsells, trials. Modes: `payment`, `subscription`, `setup`. |
| Payment Links | https://docs.stripe.com/payment-links | No-code hosted page. Adaptive Pricing. Tax. Recurring. QR. Buy button. Custom domain US$10/mo. Post-payment invoice 0.4% (US$2 cap). |
| Connect | https://docs.stripe.com/connect | SaaS platforms + marketplaces. Accounts v2. Embedded components. Separate charges and transfers **includes Malaysia**. MY Connect pricing on `/en-my/connect/pricing`. |
| Radar | https://docs.stripe.com/radar | Network ML, rules, lists, reviews, trial/bot/PAYG abuse, Radar for Platforms, Signals, Smart Disputes. MY: Radar Lite included; paid Radar from **RM0.23**/screened txn. |
| Identity | https://docs.stripe.com/identity | Government ID + selfie + SSN. **Supported business locations do not include MY.** |
| Treasury | https://docs.stripe.com/treasury | Fiat financial accounts: US public preview, UK public preview, AU private preview. **Not MY.** |
| Atlas | https://stripe.com/atlas · `/en-my/atlas` | Delaware C-corp / LLC from anywhere, **US$500**. Not SSM / MyCoID. |
| Terminal | `/en-my/pricing` Terminal section + Sessions 2026 | MY hardware SKUs and Tap to Pay listed on MY pricing. |
| CLI | https://docs.stripe.com/stripe-cli | `login`, `listen`, `trigger`, `logs tail`, `sandbox create/claim`, `agent setup`, resource CRUD, fixtures. |
| SDKs | https://docs.stripe.com/sdks | Official: Ruby, Python, Go, Java, Node, PHP, **.NET**. Web: Stripe.js + React. Mobile: iOS, Android, React Native. Terraform. OpenAPI + Postman. |
| Sessions 2026 | https://stripe.com/blog/everything-we-announced-at-sessions-2026 | Agentic Commerce Suite, Checkout studio, embedded Checkout form, Managed Payments GA for digital, Metronome as a Stripe product, Stripe Database preview, Workflows GA, custom objects preview, Tax “full support for businesses in Malaysia … soon” (**not GA on fetch day**), Treasury AU/CA later 2026. |
| Third-party MY 2026 (secondary) | HitPay “Stripe alternatives Malaysia” (11 May 2026); Airwallex MY (24 Apr 2026) | Consistent claim: Stripe MY = cards + FPX + GrabPay; **no DuitNow QR, no TnG, no Boost, no ShopeePay, no local BNPL** on the standard MY offering. Used only as corroboration; official Stripe PM table does not list those methods for MY. |

### Lazuar Pay sources (repo, 16 August 2026)

| Artifact | Absolute path |
|----------|---------------|
| Stripe adapter | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` |
| Adapter port | `…/Payments/Application/Ports/IPaymentGatewayAdapter.cs` |
| Cashier | `…/Payments/Application/Services/CheckoutSessionCashier.cs` |
| M2M checkout | `…/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` |
| Inbound webhooks | `…/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` |
| Off-session inbox | `…/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs` |
| Refund inbox | `…/Payments/Infrastructure/EventHandlers/GatewayRefundRequestedIntegrationEventHandler.cs` |
| Portal query | `…/Payments/Application/Queries/GenerateCustomerPortalQueryHandler.cs` |
| Config write | `…/Payments/Infrastructure/Commands/UpdatePaymentConfigCommandHandler.cs` |
| Config aggregate | `…/Payments/Domain/Aggregates/TenantPaymentConfiguration.cs` |
| Ops vault UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` |
| Platform vault UI | `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` |
| Commerce product / sub / coupon / checkout / dunning | `apps/lazuar-api/Modules/Commerce/Domain/Aggregates/*` |
| Initiate checkout | `…/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` |
| Billing engine | `…/Commerce/Infrastructure/Workers/BillingEngineJob.cs` |
| Billing ledger | `apps/lazuar-api/Modules/Billing/` |
| LHDN | `apps/lazuar-api/Modules/Lhdn/README.md` |
| Payments README | `apps/lazuar-api/Modules/Payments/README.md` |
| Product README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` |
| M2M quickstart | `docs/payments-integration-quickstart.md` |
| Gap dossiers | `docs/001-gaps/01-dunning-engine.md`, `05-billing-module.md`, `06-payments-module.md`, `07-commerce-module.md`, `18-outbound-customer-webhooks.md` |
| Stripe.net pin | `apps/lazuar-api/Directory.Packages.props` → **Stripe.net 48.0.1** |
| Tracker schema | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/20-sequencing-and-tracker-schema.md` |

### How status words are used below

| Word | Meaning in this file |
|------|----------------------|
| **Shipped** | Code path exists and is wired (ops UI or API). Not a production-soak claim. Matches schema `Ours=shipped` only when demoable for the named audience. |
| **Partial** | Path exists but is capability-blind, email-ambiguous, fee-lossy, or broken on a documented edge. |
| **Wrap** | Call Stripe (or leave the merchant in Stripe Dashboard). Do not reimplement. |
| **Rebuild** | Lazuar-native, because the job is multi-gateway, MY-legal, or product-owned. |
| **Refuse** | Out of company shape (`LP-XX-*`). Do not staff. Do not put on a wave as “match Stripe.” |
| **Ignore** | Not a MY/SEA CaaS job in 2026. Revisit only if a named customer pulls it. |
| **Later** | Real job, wrong wave. Usually Wave 3 billing completeness or Wave 4 extras. |

### What this file does not do

- It does not claim production guest-pay soak for any integrator (Aura included).
- It does not recommend deleting Billplz, CHIP, or Razorpay.
- It does not recommend becoming a Merchant of Record (Stripe Managed Payments / Paddle-shaped). Lazuar’s contract is **BYOK, 0% GMV take** (`LP-XX-001`).
- It does not recommend Stripe Connect as Lazuar’s tenant model. Tenants already paste their own `sk_live_` / `sk_test_`. That **is** the platform model. Connect would invert it (`LP-XX-007`).
- It does not mint `SR-*` / `SX-*` IDs. Those would be a second taxonomy. Decisions land on `LP-*`.

---

## Stripe as rail vs Stripe as rival

### The split that every later table hangs on

Stripe is one legal entity and one Dashboard. It is two competitive objects.

| Object | What the merchant experiences | What Lazuar is |
|--------|-------------------------------|----------------|
| **Stripe-as-rail** | “I have a Stripe account. Charge this card / FPX / GrabPay. Settle MYR to my bank in ~7 calendar days (cards) or ~5 business days (FPX).” | A **BYOK adapter**. Tenant secret lives in `payments.TenantPaymentConfigurations.ApiKey` (AES). Lazuar never holds PAN. Money never sits on Lazuar. Tracker: `LP-PAY-001`. |
| **Stripe-as-rival** | “I don’t need Lazuar. I’ll make a Payment Link, turn on Billing, Tax, Customer Portal, Radar, Invoicing, Sigma.” | A **horizontal SaaS** that overlaps Commerce + Billing + portal + dunning + quotes. This is the product we lose deals to when the founder is already on Stripe and selling globally. Tracker: `LP-COM-*`, `LP-DUN-*`, `LP-TAX-*`, `LP-UX-*`. |

Lazuar’s own README states the rail contract in one sentence:

> We do not act as a Merchant of Record (MoR) and we do not take 8% transaction fees. You plug in your own Stripe, Billplz, or CHIP API keys. Money flows instantly to your merchant accounts.

That sentence is the constitution. Anything that would make Lazuar the merchant, the acquirer, the tax filer of record for non-MY VAT, or the card issuer is a **shape error**.

### Stripe-as-rail: what “good wrap” means

A wrap is correct when:

1. The tenant already has (or can open) a Stripe MY account.
2. The capability is **network / PCI / issuer / bank** work (3DS, tokens, Authorization Boost, Radar scores, card account updater, FPX bank redirect, Apple Pay domain verify).
3. Lazuar’s job is to **orchestrate**: create a hosted session, stamp metadata, verify `Stripe-Signature`, extract fee/FX, publish a universal event, optionally confirm an off-session PaymentIntent, optionally open Billing Portal, optionally refund by PaymentIntent id.

The current wrap surface is exactly five methods on `IPaymentGatewayAdapter`:

| Method | Stripe API used today | Depth |
|--------|----------------------|-------|
| `GenerateCheckoutAsync` | `Stripe.Checkout.SessionService.CreateAsync`, **mode = `payment` only** | Hosted Checkout. One ad-hoc `PriceData` line. Optional `setup_future_usage=off_session` on `PaymentIntentData`. Metadata copied onto both Session and PaymentIntent. `LP-PAY-001`. |
| `ParseWebhookAsync` | `EventUtility.ConstructEvent` + optional `PaymentIntentService.GetAsync` expand `latest_charge.balance_transaction` | Maps `checkout.session.completed`, `payment_intent.succeeded`, `charge.dispute.created`. Everything else is acknowledged and dropped. `LP-PAY-005`, `LP-PAY-006`. |
| `ChargeOffSessionAsync` | `PaymentIntentService.CreateAsync` with `OffSession=true`, `Confirm=true` | Recurring / dunning. Metadata: `type=commerce_subscription`, `subscription_id`, `tenant_id`, `receipt`, optional `dunning_campaign_id`. `LP-PAY-008`. |
| `IssueRefundAsync` | `RefundService.CreateAsync` by PaymentIntent + amount (minor units) | Full or partial. No refund webhook mapping. `LP-PAY-009`. |
| `GenerateCustomerPortalAsync` | `CustomerService.ListAsync` by email (limit 1) → `BillingPortal.SessionService.CreateAsync` | Stripe-only. Email collision is undefined. Does not use `VaultedCustomerId`. `LP-COM-010`. |

SDK: **Stripe.net 48.0.1**, one `StripeClient(apiKey)` per call. No Connect account header. No idempotency key on PaymentIntent create. No `automatic_tax`. No `payment_method_types` allow-list (Dashboard dynamic PMs apply). No Elements / publishable key. No SetupIntent. No Billing Subscription / Invoice / Price / Coupon objects.

Ops vault (`PaymentSettingsPage.tsx` / admin twin):

- Gateways offered: **CHIP, BILLPLZ, STRIPE, RAZORPAY**.
- Stripe fields: **Secret Key** (`sk_live_…` / `sk_test_…`) + **Webhook Signing Secret** (`whsec_…`).
- No publishable key, no Connect client id, no Tax registration, no Radar key, no restricted key scopes.
- Soft-disable (`IsActive`) keeps ciphertext (`LP-PAY-012`).
- `UpdatePaymentConfigCommandHandler` stores Stripe `secret_key` into the same `ApiKey` column other gateways use.
- `CheckoutSessionCashier.EnsureKeyModeMatchesGateway` refuses `sk_test_` vs live K1 (and the reverse) with `KEY_MODE_MISMATCH` 409.

That is a **competent rail wrap for hosted one-shot + vaulted off-session + refund + portal URL**. It is not Stripe Billing. It is not Stripe Checkout subscription mode. It is not Elements.

Allowed gateways on M2M create (`CreateIntegrationCheckoutCommandHandler.AllowedGateways`): `STRIPE`, `BILLPLZ`, `CHIP`, `RAZORPAY`. Preferred gateway → first active tenant config → BILLPLZ last resort (legacy only).

### Stripe-as-rival: where founders actually compare

A Malaysian / SEA founder comparing “just use Stripe” vs “use Lazuar” is comparing these rival surfaces, not PaymentIntents:

| Stripe rival product | Job-to-be-done | Lazuar analogue | Official row |
|----------------------|----------------|-----------------|--------------|
| Payment Links | Share a URL, get paid, no site | Commerce public buy link + custom checkout + M2M `POST /integrations/payments/checkouts` | `LP-COM-001`, `LP-COM-005`, `LP-DEV-007` |
| Checkout | Hosted, high-conversion pay page | `lazuar-portal` + redirect to **gateway-hosted** page (Stripe Checkout / Billplz / CHIP) | `LP-UX-001`–`005`, `LP-PAY-001` |
| Billing | Subscriptions, invoices, trials, coupons, portal, Smart Retries | Commerce `Product` + `Subscription` + `Coupon` + `DunningCampaign` + `BillingEngineJob` | `LP-COM-002`–`012`, `LP-DUN-*` |
| Invoicing + Quotes | Send a numbered invoice / estimate | Ops quotes = custom `CheckoutSession` line items. B2B quote UI is **MVP-hidden** / portal `notFound()`. | `LP-COM-005`, `LP-TAX-007` |
| Customer Portal | Self-serve PM / cancel / invoice | Stripe portal wrap **if** Stripe is configured; no CHIP/Billplz portal; Commerce cancel-at-period-end missing | `LP-COM-009`, `LP-COM-010`, `LP-UX-007` |
| Tax | Auto SST/VAT/GST | Ledger `LIABILITY_TAX_PAYABLE` + **LHDN MyInvois**. Stripe Tax does **not** do MyInvois and does **not** support MY-located businesses. | `LP-TAX-001`–`010` |
| Radar | Fraud | None first-party. Relies on Stripe Radar Lite on the tenant account + 3DS. | no row — wrap by omission |
| Connect | Platform takes a cut / onboard sellers | **Refused.** BYOK is the opposite of Connect. | `LP-XX-001`, `LP-XX-007` |
| Sigma / Data Pipeline / Rev Rec | Analytics / warehouse / ASC 606 | Double-entry ledger + financial summary. `DeferredRevenueSchedule` exists, **job parked**. | `LP-TRU-001`, `LP-TRU-005`, reserved `LP-TRU-007` |
| Terminal | In-person | Out of CaaS shape. Desk money is never Hub. | `LP-XX` (no Terminal row — refuse) |
| Atlas / Treasury / Issuing / Identity | US corp, bank, cards, KYC | Refuse. | `LP-XX` / ignore |

### Competitive posture (one paragraph)

Stripe wins **global cards, FPX-on-Stripe (expensive), Radar, Smart Retries, Dashboard DX, SDKs, and “I already have a Stripe account.”** Lazuar wins **Malaysian legal survival (LHDN), multi-rail (Billplz FPX cheaper + CHIP + Razorpay), WhatsApp-shaped recovery (when the channel is real), a ledger that treats every gateway as the same money, and 0% GMV.** The worst strategy is to rebuild Checkout / Elements / Radar / Connect. The second-worst is to pretend Stripe Billing is the subscription source of truth while Commerce already owns `NextBillingDate` — that produces **two clocks**. The correct strategy is: **Commerce owns lifecycle; Stripe (or CHIP) is a dumb pipe; wrap more of the pipe (decline codes, refund webhooks, PaymentMethod update, tax amount, SetupIntent for trials); never instantiate `Stripe.Subscription`.**

### Money topology (do not flatten)

```
Buyer
  │
  ├─► Lazuar portal / integrator app
  │         │
  │         ▼
  │   Lazuar Payments cashier  (metadata, idempotency, BYOK decrypt)
  │         │
  │         ▼
  │   Stripe Checkout Session (mode=payment)   OR  Billplz bill  OR  CHIP purchase
  │         │
  │         ▼
  │   Stripe / PayNet / CHIP  ── settle ──►  tenant’s merchant bank
  │
  └─► (never) Lazuar balance
```

Lazuar then:

- verifies webhook → `PaymentWebhookLog` (event id + business key)
- publishes `GatewayPaymentCompleted` / `GatewayDisputeCreated` / `GatewayPaymentFailed`
- Commerce fulfills entitlement
- Billing posts `ASSET_CASH` + `EXPENSE_GATEWAY_FEE` + `REVENUE_GROSS` + `LIABILITY_TAX_PAYABLE` = 0
- LHDN may emit B2C receipt or monthly consolidation (backend; UI hidden)

Stripe Dashboard will still show the PaymentIntent. That is fine. **Do not sync Stripe Invoices into Commerce.** Two invoices for one charge is how founders lose the plot. Plane **G** (merchant GMV) stays on the tenant’s Stripe balance. Plane **U** (utility credits) is Lazuar’s prepaid wallet for LHDN/WhatsApp. Plane **S** (Lazuar SaaS fee) is out of this file. Mixing planes is Trap T4 / `LP-XX-012`.

---

## Malaysia / SEA availability (sourced)

### Country status (Stripe the acquirer)

| Country | Payments (open a Stripe account) | Notes as of 16 Aug 2026 |
|---------|----------------------------------|-------------------------|
| **Malaysia** | **Yes — self-serve** (`dashboard.stripe.com/register?country=MY`) | Launched 7 Oct 2019. Full Payments + listed add-ons on `/en-my/pricing`. |
| **Singapore** | Yes | GrabPay + PayNow + cards. Stronger Tax (GST, business location supported). |
| **Thailand** | Yes | PromptPay. |
| **Hong Kong** | Yes | No GST. Tax “all PTCs / no tax.” |
| **Japan** | Yes | |
| **Australia / NZ** | Yes | |
| **Indonesia** | **Preview** — `/en-my/contact/sales` | Not a DIY signup. Lazuar must not promise “Stripe ID.” |
| **India** | **Preview** — sales | Recurring has e-mandate rules; Smart Retries explicitly skip India-issued cards. Razorpay stays the IN adapter (`LP-PAY-004`). |
| **Philippines / Vietnam / Cambodia** | **Not** on `stripe.com/global` as Payments countries | Tax can calculate **into** PH/VN/KH for remote digital sellers; the **seller cannot be PH/VN/KH-based** on Stripe Tax. |
| Côte d’Ivoire, Ghana, Kenya, Nigeria, South Africa | “Extended network” via Paystack | Irrelevant to Lazuar’s SEA ICP. |

SEA implication: Stripe is a **first-class MY + SG + TH** rail. It is **not** a regional cover-all. Indonesia/India preview and PH/VN absence are exactly why Billplz / CHIP / Razorpay / (future Xendit, Midtrans) exist on the factory. README Phase 1 still lists Fiuu, Xendit, Midtrans, Cashfree — **adapters not in tree**. Honesty: four adapters ship (Stripe, Billplz, CHIP, Razorpay). Extra rails are Wave 4 (`LP-PAY-004`, reserved `LP-PAY-014` Fiuu).

### What a Malaysia Stripe account can actually turn on

Sourced from official docs + MY pricing page. “Works” means the product is sold to MY businesses or the PM is documented for `business location = MY`.

#### Payment methods (MY merchant, MYR presentment unless noted)

| Method | Works in MY? | Recurring / `setup_future_usage` | Checkout hosted | Payment Links | Elements | Invoicing | Customer Portal | Lazuar wrap today |
|--------|--------------|----------------------------------|-----------------|---------------|----------|-----------|-----------------|-------------------|
| Visa / Mastercard (domestic) | Yes. **3% + RM1.00** | Yes | Yes | Yes | Yes | Yes | Yes | Yes, via Checkout `mode=payment` (Dashboard decides PMs). `LP-PAY-001`. |
| International cards | Yes. +**1%** | Yes | Yes | Yes | Yes | Yes | Yes | Same |
| Currency conversion | Yes. +**2%** | n/a | Adaptive Pricing included | Included | Included | — | — | Fee extracted from `balance_transaction.exchange_rate` when expand works. `LP-PAY-011`. |
| **FPX** | **Yes. MY merchants only. MYR. BRN required.** **3% + RM1.00** | **No.** No SetupIntent. Checkout **subscription/setup mode unsupported**. Subscriptions/Invoices only via `send_invoice` (customer comes back) | Yes (payment mode) | Yes | Yes (not Express Checkout) | Yes | **No** | **Passive.** Adapter never sets `payment_method_types=['fpx']`. If tenant enables FPX in Dashboard, hosted Checkout may show it. **Cannot vault FPX for `ChargeOffSessionAsync`.** |
| **GrabPay** | **Yes. MY+SG. MYR/SGD.** **3%** | **No** | Payment mode only | Yes | Yes (not Express) | Yes | **No** | Same passive story |
| **Link** | Yes (MY in Link’s business-location list). Most currencies | Yes | Yes | Yes | Yes | Yes | Yes | Passive if Dashboard-on |
| **Apple Pay / Google Pay** | Yes where Apple/Google support the device/region | Yes | Yes | Yes | Yes | Yes | Yes | Passive; needs domain verify in Stripe Dashboard (not Lazuar). Reserved `LP-UX-010`. |
| Alipay | Priced on MY page (**2.9% + RM1.00**). Docs: Alipay business locations are EU/AU/CA/HK/JP/NZ/SG/US — **MY not in the Alipay country table**. Treat as **Dashboard-dependent / possibly invite**. | No | Payment mode | Yes | Yes | Yes | No | Do not market |
| **DuitNow QR** | **No** (not in Stripe PM support table; 2026 MY secondary sources agree) | — | — | — | — | — | — | Use **Billplz / CHIP**, not Stripe. Reserved `LP-PAY-013`. |
| **Touch ’n Go eWallet** | **No** | — | — | — | — | — | — | CHIP / local |
| **Boost / ShopeePay / MAE** | **No** | — | — | — | — | — | — | Local |
| **PayNet DuitNow Transfer / RPP** | **No** as a Stripe PM | — | — | — | — | — | — | Local |
| Malaysian BNPL (Atome, etc.) | **No** on standard MY list | — | — | — | — | — | — | Ignore (`LP-XX-010` adjacent) |
| PayNow (SG) | SG merchants, SGD | send_invoice only | Payment mode | Yes | Yes | Yes | No | Only if tenant is SG Stripe |
| PromptPay (TH) | TH merchants, THB | send_invoice only | Payment mode | Yes | Yes | Yes | No | Only if tenant is TH Stripe |

**FPX is the sentence everyone gets wrong.** Stripe **does** support FPX. Stripe FPX is **expensive** (3% + RM1 vs Billplz/CHIP which typically charge a low flat or ~1% local rate — see file 05) and **cannot auto-renew**. Lazuar ops copy already tells merchants: *Billplz cannot vault; use Stripe or CHIP for recurring.* It should also say: *Stripe FPX cannot vault either. Recurring on Stripe means cards (or Link), not FPX.* That copy is `LP-PAY-001` + `LP-DUN-009` honesty, not a new product.

#### Product availability for a MY-located Stripe account

| Product | MY 2026 | Evidence | Lazuar posture |
|---------|---------|----------|----------------|
| **Payments** (Charges / PaymentIntents / refunds / payouts) | **Yes** | Global list + MY pricing | **Wrap** `LP-PAY-001` |
| **Checkout** (hosted) | **Yes** | Used in adapter today | **Wrap** (already) |
| **Checkout embedded form** | Private preview globally | Sessions 2026 / Checkout docs | Ignore until GA; still a wrap |
| **Payment Links** | **Yes**, included with Payments | MY pricing | **Refuse to clone.** Commerce links + M2M cashier are the product. `LP-COM-001`, `LP-DEV-007`. |
| **Elements / Payment Element** | **Yes** (cards, FPX, GrabPay, Link, …) | PM support matrix | **Refuse** for Lazuar-hosted PCI UI. Portal is SSR; cash register is gateway-hosted. Elements would put Lazuar on the PCI SAQ-A-EP / DSP path. |
| **Payment Intents API** | **Yes** | Adapter uses it for off-session + webhook expand | **Wrap** (deepen: idempotency, decline codes, `requires_action`) `LP-PAY-008`, reserved `LP-DUN-007` |
| **Setup Intents** | **Yes** for cards/Link | Docs | **Wrap later** for free-trial card-on-file. Zero-amount Commerce path currently **skips** Payments entirely. `LP-COM-011`. |
| **Billing** (Subscriptions, Prices, Invoices, Meters, Customer Portal, Smart Retries) | **Sold on MY pricing at 0.7% of Billing volume** | `/en-my/pricing` | **Refuse as SoT.** Commerce already bills. Enabling Stripe Billing **and** `BillingEngineJob` is dual-clock. Portal wrap is OK (`LP-COM-010`). Smart Retries wrap is **not** available unless you put the sub on Stripe. |
| **Metronome usage** | Sold via Stripe; allotment US$100k | MY pricing | **Refuse.** Lazuar has no customer meter. Credits wallet is integer units for **platform** LHDN/WhatsApp, not customer usage. Reserved `LP-COM-014` default trap. |
| **Invoicing** (0.4% per paid invoice) | Sold | MY pricing | **Refuse to clone Stripe Invoices.** Rebuild MY-legal invoices (already: QuestPDF + LHDN). `LP-TAX-*`, `LP-UX-004`. |
| **Quotes** | Part of Billing | Docs | Partial analogue: custom checkout. Do not buy Stripe Quotes. `LP-COM-005`, `LP-TAX-007`. |
| **Tax** | **Customer-in-MY digital SST only. Seller-in-MY ❌.** “Full MY support soon” (Sessions 2026) | Tax country table + Sessions | **Do not wrap as MY SST engine.** Rebuild (ledger + LHDN). Optional wrap only for **export** tenants selling digital into MY from AU/SG/etc. |
| **Revenue Recognition** (0.25% of volume, ASC 606 / IFRS 15) | Sold on MY page | MY pricing | **Ignore.** Parked `RevenueRecognitionJob` is enough honesty. Reserved `LP-TRU-007`. Do not buy Stripe Rev Rec. |
| **Radar Lite** | **Included** with standard Payments | MY pricing | **Wrap by doing nothing** — it runs on the tenant account when they use Checkout. |
| **Radar for Fraud Teams** | **RM0.23+** per screened txn | MY pricing | Tenant Dashboard decision. Lazuar does not expose Radar rules. |
| **Smart Disputes** | 30% of won amount + RM90 received fee | MY pricing | Tenant Dashboard. Adapter already emits `DISPUTE_CREATED`. Do not build evidence packs. `LP-PAY-010` is **our** commerce-GMV dispute ledger, not Smart Disputes. |
| **Connect** | **Priced for MY.** Separate charges & transfers list includes MY. Payout delay type: **business day**. | Connect docs + `/en-my/connect/pricing` | **Refuse as Lazuar’s platform model.** Tenants are BYOK, not connected accounts. `LP-XX-001`, `LP-XX-007`. |
| **Terminal** | Hardware + Tap to Pay priced in MYR | MY pricing | **Refuse** (CaaS). |
| **Identity** | **MY not in supported business locations** | Identity docs | **Refuse / ignore.** MyDigital ID is the MY story if ever. |
| **Atlas** | Buyable from MY (US$500) | `/en-my/atlas` | **Ignore.** Link out at most. |
| **Treasury / Issuing / Capital** | Treasury fiat **not MY**. Capital not listed for MY. Issuing not a first-class MY SKU on the pricing nav | Treasury docs; MY pricing IA | **Refuse** |
| **Sigma** | From **RM50/mo + RM0.09/charge** | MY pricing | **Ignore.** Ledger + agent query is the product. `LP-TRU-004`, `LP-OPS-002`. |
| **Data Pipeline** | **RM0.10**/txn; includes Sigma | MY pricing | **Ignore** |
| **Workflows** | 10k steps free, then RM0.071/step | MY pricing | **Ignore** |
| **Managed Payments (MoR)** | GA for digital (Sessions 2026); tax coverage **excludes** using it as a MY SST replacement | Managed Payments docs | **Refuse.** Opposite of BYOK. `LP-XX-001`. |
| **Authorization Boost / Adaptive Acceptance** | 0.2% included on standard; extra on custom | MY pricing | **Wrap by doing nothing** |
| **Instant Payouts** | 1% (min RM2) | MY pricing | Tenant Dashboard |
| **Custom domain for Checkout / Portal** | **US$10/mo** | MY pricing | Tenant Dashboard if they care. Lazuar custom domain is `LP-UX-006` Wave 4. |
| **CLI / SDKs / webhooks** | Global | docs | **Wrap** inbound (`whsec_`). Do not ship a Stripe CLI. `LP-PAY-005`, `LP-DEV-*`. |

### Local acquiring reality (important)

Stripe MY is **Stripe as acquirer** (or Stripe’s local partner bank), not “your existing Maybank merchant ID.” There is no “bring your CIMB MID and let Stripe route FPX” in the public docs. Settlement:

- Cards: payouts page lists MY as **business-day** delay; balances page commonly cites **7 calendar days** rolling for MY (confirm per account in Dashboard). Instant Payouts optional, 1%.
- FPX: docs say **5 business days**.
- Currency: presentment MYR typical; payout MYR to a Malaysian bank account.

Billplz / CHIP are often **cheaper on FPX** and settle on local PayNet timelines. That is why the factory is multi-gateway (`LP-PAY-002`, `LP-PAY-003`). Stripe is the **global card + Link + Apple Pay + Radar** rail (`LP-PAY-001`), not the **cheap FPX** rail.

### Pricing comparison merchants will actually do

| Path | Indicative take (2026, public) | Recurring | LHDN | Who should use it |
|------|--------------------------------|-----------|------|-------------------|
| Stripe cards MY | 3% + RM1 (+1% intl, +2% FX) | Yes | No | Export / card-heavy / already-on-Stripe |
| Stripe FPX | 3% + RM1 | No | No | Almost never, if Billplz/CHIP exist |
| Stripe GrabPay | 3% | No | No | Nice-to-have wallet, not primary |
| Stripe Billing extra | +0.7% of billing volume | n/a | No | Do not turn on if Lazuar bills |
| Billplz | Typically much lower local (tenant’s Billplz plan; not Stripe) | No vault | No | Default MY hosted + FPX (`LP-PAY-002`) |
| CHIP Collect | Local MY, recurring token | Yes | No | Recurring without Stripe (`LP-PAY-003`) |
| Lazuar | **0% GMV** + prepaid credits for LHDN/WA | Via Stripe or CHIP | **Yes** (backend) | The point |

### SEA “Stripe Tax into Malaysia” trap

A SG/AU/US SaaS selling digital to Malaysians **can** ask Stripe Tax to add Service Tax on digital products once they cross MYR 500k/12 months, **if they have no MY permanent establishment**. A **Petaling Jaya Sdn Bhd** selling to Malaysians **cannot** use Stripe Tax as their SST engine today (`Your business location ❌`). They need SST registration + (for e-invoice) **MyInvois**. That is Lazuar LHDN (`LP-TAX-001`–`010`). Stripe Sessions 2026 promised “full support for businesses in Malaysia … soon.” Until that ships **and** includes **e-invoice XML to LHDN**, it is still not a substitute. Watch item, not a Wave 0 pivot.

---

## Product-by-product dossier

Each subsection: what Stripe ships (2026), what Lazuar ships (code), verdict, official row.

### 1. Payments (PaymentIntents, Charges, Customers, PaymentMethods, Refunds, Payouts)

**Stripe.** The atomic unit is the **PaymentIntent**. Customers and PaymentMethods are the vault. Refunds reference PI or Charge. Payouts are automatic to the connected bank. 135+ presentment currencies (MYR included). 3DS included. Authorization Boost / network tokens / card account updater included on standard pricing. Disputes: RM90 received + RM90 to counter; Smart Disputes 30% of win.

**Lazuar.** Never creates a Customer explicitly. Checkout Session may create one as a side effect (`CustomerEmail` on Session). Webhook stores `GatewayCustomerId` + `GatewayTokenId` onto the Commerce subscription via `StoreVaultedToken`. Off-session creates a **new** PaymentIntent each cycle (`Confirm=true`, `OffSession=true`). Refunds: inbox handler now **rejects amount ≤ 0** (the old “always refund 0” bug in older gap notes is **fixed in current tree**). Refund webhooks (`charge.refunded`, `refund.updated`, `refund.failed`) are **not mapped**. Payouts: not modeled (correct — tenant’s Stripe balance).

**Verdict: WRAP.** Deepen the wrap:

- Pass `IdempotencyKey` on off-session PI create (`subscriptionId + billingDate`). `LP-PAY-008`.
- Map `card_declined` / hard-decline codes into `GatewayPaymentFailed` metadata so dunning can skip stupid retries (Stripe Smart Retries’ hard-decline list is the cheat sheet). `LP-DUN-007` Wave 4.
- Map `payment_intent.payment_failed` and `payment_intent.requires_action` (SCA). Today SCA on off-session is a boolean `false`. `LP-PAY-007` is the publish path; the Stripe adapter still needs to emit `PAYMENT_FAILED` from PI events.
- Map refund events so Billing can post `GatewayRefundCompleted` from the **network**, not only from our request path. `LP-PAY-009`.
- Do **not** build a Lazuar “Payments list that is a Stripe Dashboard clone.” Ledger + Commerce transaction log is enough. `LP-OPS-001`. Support timeline is `LP-OPS-005`.

### 2. Checkout (hosted)

**Stripe.** Three UIs on Checkout Sessions API: hosted full page (recommended), embedded form (private preview / Sessions 2026), Elements. Hosted includes order summary, Tax, Adaptive Pricing, Link, promotions, trials, upsells, Managed Payments. Modes: `payment`, `subscription`, `setup`.

**Lazuar.** `StripeGatewayAdapter.GenerateCheckoutAsync` hard-codes `Mode = "payment"`. One line item from `amount * 100` (`UnitAmountDecimal = amount * 100` — **zero-decimal currencies would be wrong**; MYR is two-decimal so OK). `SuccessUrl` / `CancelUrl` are Commerce or integrator URLs. Fulfillment is **webhook-only** (correct). Portal success page polls server status (`LP-UX-003` seed shipped).

We do **not** use Checkout `subscription` mode. That would create a Stripe Subscription and fight Commerce.

**Verdict: WRAP hosted `payment` mode. REFUSE `subscription` mode. IGNORE embedded form until a named embed customer exists.** Do not rebuild Stripe’s hosted page chrome. `lazuar-portal` is the pre-pay collect-email/coupon/address step; Stripe (or Billplz) is the PAN/FPX step. Rows: `LP-PAY-001`, `LP-UX-001`–`005`.

### 3. Payment Links

**Stripe.** No-code URL. Reusable. QR. Buy button. Adaptive Pricing. Tax. Recurring. Coupons. Optional “customer chooses amount.” 30+ languages. Custom domain $10/mo. After-payment invoice 0.4%.

**Lazuar.** Three link products:

1. **Commerce product slug** — `/{tenantSlug}/products/{slug}` → `POST /checkout`. `LP-COM-001`.
2. **Custom checkout / quote** — admin line items, copy link. B2B `/pay/{session}` is `[MVP-HIDE]` / `notFound()`. `LP-COM-005`, `LP-TAX-007`.
3. **M2M cashier** — `POST /api/v1/integrations/payments/checkouts` with idempotency, fingerprint, stamped metadata. `LP-DEV-007`.

**Verdict: REBUILD is already the strategy (and correct).** Do not wrap Stripe Payment Links API. Do not add “open Stripe Dashboard → Payment Links” as a Lazuar feature. Compete on: multi-gateway, LHDN-ready receipts, integrator metadata, 0% take.

### 4. Elements / Stripe.js

**Stripe.** Payment Element, Express Checkout Element, Address Element, Link Authentication, mobile Payment Element. Appearance API. Dynamic PMs.

**Lazuar.** No publishable key in vault. No Stripe.js. No React Stripe.js. Privacy page correctly says PAN goes to the gateway, never Lazuar.

**Verdict: REFUSE** for the default CaaS path. Elements is how you **become** a checkout UI company and pick up PCI scope. If a future “white-label embed” customer demands in-page cards, the wrap is Stripe-hosted **embedded Checkout** (still Stripe iframe, SAQ-A), not a from-scratch Element integration. That is a later, named-customer wrap — not a Wave 0 rebuild. No new `LP-*` until a named customer exists.

### 5. Billing (subscriptions, invoices, usage, coupons, trials, proration, customer portal)

**Stripe Billing (2026).**

- Pricing models: flat, per-seat, tiered, usage, multi-currency.
- Subscriptions with trials, schedules, pause (pause is **public preview** on the 2026 roadmap), prebilling (GA Q2 roadmap).
- Invoices: draft → open → paid; `send_invoice` vs `charge_automatically`.
- Quotes → accept → subscription/invoice.
- Coupons / promotion codes / credit notes.
- Proration on plan change (plus previewed Billing scripts for custom proration).
- Meters API (100M events/mo included) + **Metronome** for real usage / hybrid / commits / streaming payments.
- Customer Portal (no-code): update PM, cancel, invoices, switch plans — configuration in Dashboard.
- Smart Retries + automations (see § 19).
- Price: **0.7% of Billing volume** on MY standard (excludes one-off invoices).

**Lazuar Commerce + Billing.**

| Stripe Billing concept | Lazuar object | Honesty | Row |
|------------------------|---------------|---------|-----|
| Product + Price | `Commerce.Product` (single `Price`, `Interval` `one_time`/`mo`/`yr`-style, `PricingModel` default `FIXED`, `MinimumPrice` for PWYW) | No tiers, no per-currency prices, no metered price. `PricingModel` largely unused. | `LP-COM-001`–`003`; reserved `LP-COM-013` PWYW |
| Subscription | `Commerce.Subscription` (`PENDING/ACTIVE/PAST_DUE/SUSPENDED/CANCELED`, `NextBillingDate`, `CurrentPeriodEnd`, vault ids, dunning fields, `MetadataJson`) | **No seats that survive renewal, no proration, no pause (only dunning pause), no plan-change.** | `LP-COM-002`, `LP-COM-007`–`012` |
| Invoice | **None** as a cycle document | Billing `InvoiceIssuedIntegrationEvent` books AR+deferred for a B2B path; gateway payments book cash immediately. No open invoice, no `attempt_count`, no `next_payment_attempt`. | `LP-TAX-*` legal; Wave 3 if we ever want AR invoices |
| Customer | CRM `ClientProfile` + optional Stripe customer id on vault | Portal lookup is **email**, not vault id. | `LP-COM-010` |
| Coupon | `Commerce.Coupon` (PERCENTAGE/FIXED, max uses, reserve/confirm/release, product allow-list, min price, expiry) | Real. Not Stripe Coupons. Not promotion codes on Stripe Checkout. | `LP-COM-004` |
| Trial | Zero-amount checkout if coupon = 100%; **no card-on-file** | Gap vs Stripe Checkout trial. | `LP-COM-011` |
| Usage / meters | **None** for customer products. Platform `TenantCreditBalance` is Lazuar’s own prepaid wallet. | Do not confuse the two. | `LP-COM-014` trap; `LP-OPS-004` wallet |
| Proration | **Not modeled** | Rebuild only if plan-change becomes a sold job. | `LP-COM-008`, `LP-COM-012` |
| Customer Portal | Wrap Stripe Billing Portal **if** Stripe configured; ops “Copy Portal Link” | Email `ListAsync Limit=1`. Non-Stripe tenants error. Magic-link list + hard cancel exist. | `LP-COM-010`, `LP-UX-007`, `LP-COM-009` |
| Smart Retries | `DunningCampaign` steps `EMAIL` / `WHATSAPP` / `AUTO_CHARGE` by `DayOffset`; `ChargeAttemptLog` 1/day | Calendar/status driven, not invoice/decline-code driven. Not ML. WhatsApp step does not send. | `LP-DUN-001`–`010`, `LP-MSG-003` |

**Verdict:**

- **REBUILD** (already): catalog, subscription state, coupons, dunning copy, multi-gateway renewals.
- **WRAP**: Stripe Customer + PaymentMethod lifecycle; Billing Portal URL; never Stripe Subscription.
- **REFUSE**: Metronome, Stripe Invoices as SoT, Stripe Coupons, 0.7% Billing attach.
- **LATER REBUILD (not wrap):** trials with SetupIntent (`LP-COM-011`); proration (`LP-COM-008`); seats (`LP-COM-007`); cancel-at-period-end (`LP-COM-009`); gateway-agnostic manage-PM.

Wave 3 in file 20 exists **because** Stripe Billing is the comparison set. Do not “catch up” by turning Stripe Billing on.

### 6. Tax

**Stripe Tax.** Calculates sales tax / VAT / GST / SST from PTC + customer location + (sometimes) business location. Registrations, monitoring, reports, TaxJar US filing (Sessions 2026). Checkout / Payment Links / Billing / Invoicing one-click. **MY: digital Service Tax, remote seller only, seller-in-MY not supported.** Threshold MYR 500k. No MyInvois. No SST-02. No 8% SST on goods.

**Lazuar.**

- CheckoutConfiguration `RequiresTaxId` (enforced on initiate) — UI hidden under ADR 023.
- Webhook `TaxAmount` from Stripe Session `TotalDetails.AmountTax` (0 on PI-only path).
- Ledger `LIABILITY_TAX_PAYABLE`.
- **LHDN module**: UBL 2.1 invoices, credit/debit/refund, self-billed, B2C consolidation job (28th MYT), TIN validate, MyInvois submit/poll, QR. This is the actual MY tax product. Merchant UI is `[MVP-HIDE]`. Seed: `backend-only`.

**Verdict: REBUILD MY e-invoice / SST liability (already). Do not wrap Stripe Tax for MY-incorporated tenants. Optional later wrap for export tenants selling digital into EU/US/SG — only if a customer is already on Stripe Tax and wants Checkout `automatic_tax=true` passed through the adapter.** Passing `automatic_tax` is a 10-line wrap; building a tax engine is a company. Rows: `LP-TAX-001`–`010`. Multi-country tax before LHDN trusted is `LP-XX-009`.

### 7. Invoicing

**Stripe Invoicing.** Numbered invoices, hosted invoice page, PDF, reminders, Smart Retries on one-off invoices, partial payments / payment plans (preview), 40+ PMs, 0.4% per paid invoice.

**Lazuar.**

- QuestPDF receipts / proforma drafts; R2 store; HMAC public download (`LP-UX-004`).
- Ops quotes = custom checkout sessions (MVP-hide on portal pay page).
- LHDN legal invoice ≠ Stripe commercial invoice.
- `InvoiceIssuedHandler` exists; `ManualPaymentRecorded` historically dead.

**Verdict: REBUILD commercial + legal invoices on Lazuar/LHDN. REFUSE Stripe Invoicing as the customer-facing invoice.** A wrap of “create Stripe Invoice” would bypass Commerce metadata and LHDN sequencing.

### 8. Revenue Recognition

**Stripe.** ASC 606 / IFRS 15, 0.25% of volume, 30-day trial, dashboards.

**Lazuar.** `DeferredRevenueSchedule` + `RevenueRecognitionJob` **parked / not registered** (Billing README decision 00.3). Gateway payments recognize **immediately**. Invoice-issued books deferred **without creating a schedule**.

**Verdict: IGNORE Stripe Rev Rec. If finance ever wants amortization, finish the parked job from product periods — do not buy 25 bps.** Reserved `LP-TRU-007`.

### 9. Radar

**Stripe Radar (2026, including Sessions).** Lite included. Fraud Teams paid. Rules, lists, reviews. Trial abuse, bot vs agent, PAYG abuse, multi-account, merchant delinquency, website LLM review, Signals on/off Stripe, custom models, Smart Disputes evidence, Radar for Platforms. Screens **all supported PMs** now (not cards-only).

**Lazuar.** Zero Radar API. Zero risk score on ledger. Disputes: `charge.dispute.created` → `GatewayDisputeCreated` → Billing clawback for utility top-ups. Commerce GMV dispute ledger is **absent** (`LP-PAY-010`).

**Verdict: WRAP by remaining hosted-Checkout (Radar Lite runs). REFUSE a Lazuar fraud product. Do not reimplement rules engines. If a tenant wants Radar for Fraud Teams, they toggle it in Stripe and pay RM0.23.** Optional later: persist `outcome.risk_score` from expanded Charge onto the webhook result for ops — still a wrap, not a new family.

### 10. Identity

**Stripe Identity.** 120+ country IDs, selfie match, SSN. **MY not a supported Identity business location.**

**Lazuar.** CRM profiles; LHDN TIN validation (`LP-TAX-005`). One workspace KYC is not Stripe Identity.

**Verdict: IGNORE / REFUSE.** MyDigital ID / Singpass were README Phase 3 poetry. Do not staff.

### 11. Atlas

**Stripe Atlas.** Delaware C-corp/LLC, EIN, 83(b), SAFE (Sessions 2026 + Treasury account for founders). US$500. Available to MY founders as **customers of Atlas**, not as a MY corporate registry.

**Lazuar.** SSM / LHDN TIN on `TenantBillingProfile` (`LP-TAX-008`). No incorporation product.

**Verdict: IGNORE.** A docs link in onboarding is the maximum.

### 12. Connect

**Stripe Connect (2026).** 16,000+ platforms. Accounts v2 unified identity. Hosted / embedded / API onboarding. Direct / destination / separate charges & transfers (**MY listed**). Application fees. IC++ in 45 markets. Networked onboarding. Radar for Platforms. Smart Disputes for connected accounts. Managed Risk API preview. Marketplace wallets / prepaid cards preview. Cross-border marketplace payouts US/UK/EEA/CA. MY Connect **starting 0.25%** if platform sets its own pricing; “Included with Payments” if Stripe sets pricing.

**Lazuar.** Multi-tenant BYOK. Each workspace has its own Stripe/Billplz/CHIP/Razorpay ciphertext. Provision API mints **Lazuar** `sk_test_` / `sk_live_` (K1), not a Stripe connected account. Explicit docs: “This looks like a Stripe secret. Mint a Lazuar Pay key.”

Aura marketing once claimed “Stripe Connect”; that copy is retired. Correct.

**Verdict: REFUSE Connect as Lazuar’s account model.** It would make Lazuar liable for connected-account losses, force a take-rate, and destroy Billplz/CHIP. The only honest Connect story is “a *tenant* who is themselves a marketplace can use Connect **inside their own Stripe account**; Lazuar still just creates Checkout Sessions on their `sk_`.” We do not need to know. We already pass their key. Rows: `LP-XX-001`, `LP-XX-007`.

### 13. Treasury

**Stripe Treasury.** Financial accounts, multi-currency, USDC, cards on balance, instant Stripe-to-Stripe, agentic Treasury. **US / UK / (AU private).** Not MY.

**Lazuar.** No stored value for customer GMV. Platform credits are a **utility wallet**, not a bank (`LP-OPS-004`).

**Verdict: REFUSE.**

### 14. Webhooks

**Stripe.** Snapshot events + thin events. Dashboard endpoints. `Stripe-Signature` windowed HMAC. CLI `listen` / `trigger` / `events resend`. Retry with exponential backoff. 30-day resend.

**Lazuar inbound.**

```
POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}
```

Stripe adapter verifies signature, maps 3 event types, expands PI for fees. Handler: idempotency on `EventId` **and** business key (`PAYMENT_COMPLETED` + PI id) so `checkout.session.completed` + `payment_intent.succeeded` do not double-fulfill (`LP-PAY-005`, `LP-PAY-006`). Soft-disabled gateways still accept webhooks. Unmapped types return success (prevents Stripe retry storms). `PAYMENT_FAILED` **is** published from the handler **if** the adapter emits it — Stripe adapter currently **does not** map `payment_intent.payment_failed`. Off-session failures publish `GatewayPaymentFailed` from the inbox handler instead (`LP-PAY-007`).

**Lazuar outbound.** One (now multi-endpoint after Phase B) HMAC dispatcher; Commerce `order.completed` / `subscription.*` plus M2M `payment.*`. Payloads are thin. Residuals: redrive, rotate, test ping, SSRF, LHDN unify (`LP-DEV-003`–`005`, `LP-DEV-010`). This is the **rival** to Stripe’s “you get every event in the Dashboard.”

**Verdict: WRAP inbound Stripe webhooks (deepen event map). REBUILD outbound (already) — one algorithm for all gateways is the DX win.** Do not expose raw Stripe events to integrators; that couples them to one rail.

### 15. CLI

**Stripe CLI (2026).** `stripe login`, `listen --forward-to`, `trigger`, `logs tail`, `sandbox create` (7-day claimable), `agent setup` (Claude/Codex/Cursor), `fixtures`, resource verbs, `docs`.

**Lazuar.** No CLI. Taskfile / turbo / `examples/hub-cashier-next`. Developers hub is Scalar OpenAPI (`LP-DEV-006`).

**Verdict: IGNORE a Stripe-compatible CLI. Optional later: a thin `lazuar pay listen` that tails **our** outbound webhooks — only if DX pain is measured. Not a match-Stripe ticket.**

### 16. SDKs

**Stripe.** First-party in 7 server languages + JS + iOS/Android/RN + Terraform. OpenAPI. Postman. Semantic versioning + dated API versions.

**Lazuar.** TypeSpec → OpenAPI → `api-types-ts` + `api-types-dotnet`. **LHDN** has Kiota SDKs (`lhdn-sdk-ts`, `lhdn-sdk-dotnet`) — `LP-DEV-009`. **Payments has no published SDK** — integrators copy the quickstart. Stripe.net is an **internal** dependency of the adapter, not a customer SDK.

**Verdict: REBUILD typed contracts (already). Do not publish a “Stripe-shaped” SDK.** A small `lazuar-pay` Node helper for signature verify + checkout create is a DX wrap of **our** API, not of Stripe. That is `LP-DEV-006` craft, not a new family.

### 17. Sigma

**Stripe Sigma.** SQL + NL over Stripe data. MY: RM50/mo + per-charge.

**Lazuar.** Ledger lines, financial health agent query, ops LLM prompt that **must** distinguish Gross vs Net Cash vs tax liability (`LP-TRU-004`, `LP-OPS-002`).

**Verdict: IGNORE.** If a tenant wants Sigma, they open Stripe Dashboard. Lazuar’s job is **net cash after all gateways**, which Sigma cannot see (Billplz/CHIP live elsewhere).

### 18. Data Pipeline

**Stripe.** Warehouse sync (Redshift, Snowflake, Databricks; BigQuery on roadmap). RM0.10/txn. Includes Sigma. Sessions 2026: next-gen + Sheets + Stripe Database (hosted read-only Postgres preview).

**Lazuar.** Postgres schemas (`payments`, `commerce`, `billing`, `lhdn`, …). No customer-facing warehouse product.

**Verdict: IGNORE.**

### 19. Smart Retries / dunning

**Stripe Smart Retries.** ML over device graph, local-time, issuer. Default 8 tries / 2 weeks. Custom 3-step schedule. Hard declines (`lost_card`, `stolen_card`, `authentication_required`, …) schedule but **do not fire** until a new PM exists. Local PM retries: ACH/ACSS/BECS/Bacs/NZ BECS/SEPA **only** — **not FPX, not GrabPay**. India cards excluded. Webhooks: `invoice.payment_failed` with `attempt_count` / `next_payment_attempt`. Terminal states: cancel / unpaid / leave past_due.

**Lazuar dunning.** First-class product claim (“Native WhatsApp Dunning”). Implementation:

- `DunningCampaign` + `DunningStep` (`DayOffset`, `EMAIL`/`WHATSAPP`/`AUTO_CHARGE`).
- `DunningEngineJob` + `BillingEngineJob` (hourly).
- `ChargeAttemptLog` uniqueness.
- Off-session via Payments inbox; failure event published from handler.
- WhatsApp via Messaging credits; templates exist; `IMessagingService` → `ConsoleMessagingService`; `Messaging:WhatsAppEnabled` defaults false. Seed: **absent as a channel** (`LP-MSG-003`).
- Gaps: no decline-code policy (`LP-DUN-007`); campaign snapshot/versioning absent (`LP-DUN-006`); Billplz products cannot `AUTO_CHARGE`; FPX on Stripe cannot either (`LP-DUN-009`).

**Verdict: REBUILD dunning (WhatsApp + multi-gateway + MY copy) — that is a real differentiator. WRAP decline-code intelligence from Stripe’s PI error object. Do not enable Stripe Smart Retries (requires Stripe Billing invoices).** Stealing the **hard-decline list** as a data table in Commerce is allowed (it is a published doc, not an ML model). Wave 0 owns the closed loop (`LP-DUN-001`–`005`, `LP-DUN-008`). Intelligence is Wave 4.

### 20. Terminal

**Stripe.** Readers (WisePad 3 RM279, S710 / S700 RM1,209, WisePOS E RM999), Tap to Pay RM0.45/auth, cellular RM40/reader/mo, in-person **2.8% + RM0.50**. Sessions 2026: T600, standalone mode preview.

**Lazuar.** None. Desk / cash / proof is never Hub.

**Verdict: REFUSE.**

### 21. Link

**Stripe.** Network wallet. MY in business-location list. Agent wallet (Sessions 2026). Conversion analytics tab.

**Lazuar.** Falls out of hosted Checkout if tenant enables Link.

**Verdict: WRAP by omission.** Do not build a Lazuar wallet. Reserved `LP-UX-010` if we need a visibility row.

### 22. Managed Payments (MoR)

**Stripe.** Stripe is merchant of record; tax in 80+ countries; support; fraud. Sessions 2026: GA for digital businesses.

**Lazuar.** Constitutionally **not MoR**.

**Verdict: REFUSE.** `LP-XX-001`. If a tenant wants MoR they use Paddle / Stripe Managed Payments **instead of** Lazuar for that SKU.

### 23. Authorization Boost / Adaptive Pricing / 3DS

Included on MY standard Payments. Adaptive Pricing: customer sees local currency, conversion fee from 2%.

**Verdict: WRAP by omission.** Adapter should not disable them. Do not reimplement FX UX; ledger already stores `FxRate` + `BaseCurrency` when expand works. Multi-currency checkout as a first-class product is reserved `LP-COM-015`.

### 24. Climate / Capital / Financial Connections / Crypto Onramp / Issuing / Workflows / Custom objects / Projects / MCP / Agentic Commerce

**Verdict: IGNORE / REFUSE** for Lazuar Pay 2026. Agentic Commerce is interesting **only** as a future integrator who calls **our** M2M checkout (`LP-DEV-007`); we do not become UCP. MCP on Stripe is Stripe’s DX, not ours. Super-app / ops-AI-as-the-product is `LP-XX-011`.

---

## Feature comparison tables

Legend for **Lazuar status**: `Y` shipped · `P` partial · `N` none · `n/a` not applicable.  
**Decision**: `WRAP` · `REBUILD` · `REFUSE` · `IGNORE` · `LATER`.  
**Row** is the official tracker ID from file 20.

### Table A — Accepting money

| Capability | Stripe 2026 | Lazuar status | Evidence | Decision | Row |
|------------|-------------|---------------|----------|----------|-----|
| Hosted checkout session | Checkout Sessions, 3 UIs | `Y` redirect to Stripe Checkout `mode=payment` | `StripeGatewayAdapter.GenerateCheckoutAsync` | **WRAP** (done) | `LP-PAY-001` |
| Payment Links no-code | Dashboard + API | `Y` Commerce slug + custom session + M2M | Commerce + `CreateIntegrationCheckout` | **REBUILD** (done; do not clone Stripe Links) | `LP-COM-001`, `LP-DEV-007` |
| Elements / in-page card form | Payment Element | `N` | No pk_ vault, no Stripe.js | **REFUSE** | — |
| PaymentIntents one-shot | Full | `P` only as Checkout’s child PI | Adapter never creates on-session PI directly | **WRAP** via Checkout | `LP-PAY-001` |
| Off-session PI | Full | `Y` | `ChargeOffSessionAsync` | **WRAP** (deepen) | `LP-PAY-008` |
| SetupIntent / card-on-file without charge | Full | `N` | Zero-amount path skips Payments | **WRAP later** | `LP-COM-011` |
| Customers API | Full | `P` email on Session; list-by-email for portal | Portal handler | **WRAP** (use vault id) | `LP-COM-010` |
| PaymentMethods attach/detach | Full | `P` store ids from webhook | `StoreVaultedToken` | **WRAP** | `LP-PAY-008` |
| Refunds full/partial | Full | `Y` API path; `N` refund webhooks | Refund handler amount>0; adapter unmapped refunds | **WRAP** (add events) | `LP-PAY-009` |
| Disputes | Full + Smart Disputes | `P` create event; utility clawback only | `charge.dispute.created` | **WRAP** evidence to Dashboard; **REBUILD** GMV ledger | `LP-PAY-010` |
| Payout schedule / Instant Payouts | Full | `n/a` | Not our balance | **IGNORE** | — |
| Cards Visa/MC | Y | `Y` if tenant enables | Dashboard PMs | **WRAP** | `LP-PAY-001` |
| FPX | Y, no recurring, 3%+RM1 | `P` only if Dashboard-on | Docs; adapter silent | **WRAP** display; **do not** use for vault | `LP-PAY-001` + `LP-PAY-002` (cheap FPX) |
| GrabPay | Y, no recurring, 3% | `P` same | Docs | **WRAP** display | `LP-PAY-001` |
| DuitNow QR / TnG / Boost | N | `N` on Stripe; **local via Billplz/CHIP** | PM table | **REBUILD on local rails**, not Stripe | reserved `LP-PAY-013` |
| Apple Pay / Google Pay / Link | Y | `P` Dashboard | — | **WRAP** | reserved `LP-UX-010` |
| 100+ global APMs | Y, country-gated | `N` explicit | — | **IGNORE** except tenant’s Stripe country | — |
| Adaptive Pricing | Y | `N` explicit | — | **WRAP** by omission | reserved `LP-COM-015` |
| Manual capture / holds | Y | `N` | — | **IGNORE** | — |
| Split tender / tips | Terminal / Checkout extras | `N` on Pay | Desk is never Hub | **REFUSE** on Pay | — |
| Idempotent checkout create | Stripe idempotency keys | `Y` M2M fingerprint + key | `CreateIntegrationCheckoutCommandHandler` | **REBUILD** (ours) + **WRAP** (pass through to Stripe) | `LP-DEV-007`, `LP-PAY-006` |
| Test/live key guard | Dashboard | `Y` `KEY_MODE_MISMATCH` | `CheckoutSessionCashier` | **REBUILD** (ours) | `LP-PAY-012` |
| Multi-gateway failover | n/a (Stripe-only) | `P` preferred → first active → BILLPLZ legacy | Cashier resolve | **REBUILD** | `LP-PAY-001`–`004` |
| Capability flags on adapter | n/a | `N` try/catch | Port is capability-blind | **REBUILD** port | reserved `LP-PAY-018` |
| Encrypted BYOK + soft-disable | Restricted keys | `Y` AES + `IsActive` | `TenantPaymentConfiguration` | **REBUILD** (done) | `LP-PAY-012` |
| Inbound verify + persist | Dashboard endpoints | `Y` | `ProcessGatewayWebhookCommandHandler` | **WRAP** | `LP-PAY-005` |
| Business-key idempotency | Event id only (their side) | `Y` | dual session+PI | **REBUILD** (done) | `LP-PAY-006` |
| Fee fidelity | Balance transactions | `P` Stripe yes; Billplz 0 | Adapter expand | **WRAP** Stripe; **REBUILD** Billplz estimate | `LP-PAY-011` |

### Table B — Billing & subscriptions

| Capability | Stripe 2026 | Lazuar status | Evidence | Decision | Row |
|------------|-------------|---------------|----------|----------|-----|
| Product catalog | Products + Prices | `Y` single price + interval + gateway | `Product.cs` | **REBUILD** | `LP-COM-001` |
| Recurring intervals | Flexible | `P` mo/yr-style strings | `Interval` | **REBUILD** | `LP-COM-002` |
| Per-seat / quantity on sub | Y | `N` (checkout qty not on sub) | Gap 07 | **LATER** rebuild | `LP-COM-007` |
| Tiered / volume prices | Y | `N` | — | **IGNORE** | — |
| Usage / meters / Metronome | Y | `N` (platform credits ≠ usage) | Billing wallet | **REFUSE** Metronome | reserved `LP-COM-014` |
| Trials | Y + card-on-file | `P` 100% coupon / zero-amount, no vault | InitiateCheckout | **WRAP** SetupIntent later | `LP-COM-011` |
| Coupons / promo codes | Y | `Y` own coupons | `Coupon.cs` | **REBUILD** | `LP-COM-004` |
| Proration / plan change | Y | `N` | Gap 07 | **LATER** rebuild | `LP-COM-008`, `LP-COM-012` |
| Pause subscription | Preview 2026 | `N` (dunning pause only) | `PauseDunning` | **LATER** | `LP-DUN-008` is not pause-sub |
| Cancel at period end | Y | `N` hard cancel only | Gap 07 | **LATER** rebuild | `LP-COM-009` |
| Invoices as AR objects | Y | `P` events + PDF, no cycle invoice | Billing | **REBUILD** MY invoices; **REFUSE** Stripe Invoices SoT | `LP-TAX-*` |
| Quotes | Y | `P` custom checkout; portal MVP-hide | QuotesPage | **REBUILD** | `LP-COM-005`, `LP-TAX-007` |
| Credit notes | Y | `P` LHDN CN + refund events | LHDN | **REBUILD** (legal) | `LP-TAX-006` |
| Customer Portal | Y | `P` Stripe-only, email lookup | Portal handler + ops button | **WRAP** (fix lookup) + **REBUILD** agnostic manage-PM later | `LP-COM-010` |
| Customer self-cancel | Portal | `P` hard cancel / magic-link | `LP-UX-007` | **REBUILD** | `LP-COM-009`, `LP-UX-007` |
| Smart Retries | ML | `P` day-offset AUTO_CHARGE | DunningEngine | **REBUILD** cadence; **WRAP** decline codes | `LP-DUN-001`–`007` |
| Dunning emails | Y | `Y` templates | Communications | **REBUILD** | `LP-MSG-001` |
| Dunning WhatsApp | N (not a Stripe channel) | `N` as product channel | Console logger | **REBUILD** Wave 4 | `LP-MSG-003`, `LP-DUN-002` |
| Failed payment → past_due | Y | `P` / seed shipped on publish path | Off-session handler | **REBUILD** (keep loop honest) | `LP-PAY-007`, `LP-DUN-001` |
| 0.7% Billing attach | Optional | Must stay **off** | Pricing | **REFUSE** | — |
| Offline mark-paid | n/a | `Y` | Commerce | **REBUILD** | `LP-COM-006` |
| Manual enroll / reminder-only | n/a | `Y` | `IsReminderOnly` | **REBUILD** | `LP-COM-006` |

### Table C — Tax, compliance, money movement extras

| Capability | Stripe 2026 | Lazuar status | Decision | Row |
|------------|-------------|---------------|----------|-----|
| Auto VAT/GST/SST calc | Y (jurisdiction-limited) | `P` tax id collect + ledger liability | **REBUILD** MY; **WRAP** `automatic_tax` only for export | `LP-TAX-003`, `LP-TAX-012` reserved |
| MY seller SST | ❌ business location | LHDN + ledger | **REBUILD** | `LP-TAX-*` |
| MY e-invoice MyInvois | N | `P` backend pipeline, UI hidden | **REBUILD** (moat) | `LP-TAX-001`–`009` |
| US sales tax filing | TaxJar | N | **IGNORE** | — |
| Revenue recognition | Y 0.25% | Parked job | **IGNORE** Stripe; **LATER** own job | reserved `LP-TRU-007` |
| Radar Lite | Included | Inherited on Checkout | **WRAP** | — |
| Radar custom rules | Paid | N | **IGNORE** (tenant Dashboard) | — |
| Identity / KYC | Not MY | TIN validate | **REFUSE** Identity | `LP-TAX-005` |
| Atlas | US corp | N | **IGNORE** | — |
| Connect platform | Y in MY | BYOK opposite | **REFUSE** | `LP-XX-001`, `LP-XX-007` |
| Treasury / Issuing / Capital | Not MY / not ICP | N | **REFUSE** | — |
| Terminal / Tap to Pay | Priced MY | N | **REFUSE** | — |
| Sigma / Data Pipeline / Rev Rec SaaS | Priced MY | Ledger + agent | **IGNORE** | `LP-TRU-001`, `LP-OPS-002` |
| CLI | Best-in-class | N | **IGNORE** | — |
| Multi-language official SDKs | Y | Types + LHDN SDKs | **REBUILD** our DX, not theirs | `LP-DEV-006`, `LP-DEV-009` |
| Webhook verify | `Stripe-Signature` | Y | **WRAP** | `LP-PAY-005` |
| Outbound developer webhooks | Dashboard + retries | `P` One dispatcher | **REBUILD** | `LP-DEV-003`–`005` |
| MoR / Managed Payments | GA digital | Explicitly not | **REFUSE** | `LP-XX-001` |
| Agentic commerce / MPP | Sessions 2026 | N | **IGNORE** (maybe M2M later) | `LP-DEV-007` |
| Xero journal sync | n/a (not Stripe) | N | **LATER** ADR 021 keep | `LP-TRU-006` |
| GSTN / Coretax | n/a | N | **REFUSE** until LHDN trusted | `LP-XX-009` |

### Table D — DX and operations

| Capability | Stripe | Lazuar | Decision | Row |
|------------|--------|--------|----------|-----|
| Dashboard | World-class | `lazuar-ops` AWS-style | **REBUILD** our ops, not a Stripe skin | `LP-OPS-001`–`004` |
| Restricted API keys + reveal last4 | Y | Prefix `sk_live_` / `sk_test_`; one-time reveal | **REBUILD** DX | `LP-DEV-001`, reserved `LP-DEV-012` |
| Test clocks | Billing test clocks | N | **IGNORE** | — |
| `stripe listen` | Y | Public tunnel docs for Billplz | **IGNORE** | — |
| Workbench / request logs | Y | N | **LATER** outbound delivery logs | `LP-DEV-005`, `LP-OPS-005` |
| Sandbox claimable 7-day | Sessions/CLI | `sk_test_` workspaces | **REBUILD** our test mode (exists) | `LP-DEV-001` |
| Docs | docs.stripe.com | Scalar + VitePress + quickstart | **REBUILD** | `LP-DEV-006` |
| Status page / 99.99 | Stripe’s | Ours + theirs | **WRAP** their status for rail incidents | — |
| Payment settings vault | Dashboard | Ops + admin vault | **REBUILD** | `LP-OPS-003` |
| Support payment timeline | Dashboard payment | SQL/logs join | **REBUILD** | `LP-OPS-005` |
| MRR from ledger | Billing metrics + Sigma | Absent first-class | **LATER** rebuild | `LP-TRU-005` |

### Table E — Honest “are we behind?” (the wrap filter)

If a row is “behind Stripe” **and** the fix is “call Stripe,” it is not a product gap. It is an adapter gap.

| Feels like a gap | Actually |
|------------------|----------|
| No Radar UI | Tenant already has Radar Lite |
| No FPX toggle in ops | Stripe Dashboard payment methods; cheap FPX is Billplz (`LP-PAY-002`) |
| No Apple Pay | Domain verify in Stripe (`LP-UX-010`) |
| No Smart Retries ML | Would require Stripe Billing SoT — forbidden. Static decline table is `LP-DUN-007` |
| No Tax | Stripe Tax cannot do MY seller SST/e-invoice (`LP-TAX-*`) |
| No Connect onboarding | We are BYOK on purpose (`LP-XX-007`) |
| No Elements | PCI / shape |
| No Sigma | Cross-gateway net cash is **our** job (`LP-TRU-001`) |
| Weak dunning vs Smart Retries | **Real** product gap — rebuild `LP-DUN-*`, don’t wrap Billing |
| Portal email collision | **Real** wrap bug — use `VaultedCustomerId` (`LP-COM-010`) |
| No refund webhooks | **Real** wrap bug (`LP-PAY-009`) |
| No decline codes | **Real** wrap bug (`LP-DUN-007`) |
| Dual checkout+PI fulfillment | **Fixed** via business key (`LP-PAY-006`) |
| Off-session metadata missing type | **Fixed** in current adapter (`type=commerce_subscription`) |
| Refund amount 0 | **Fixed** in current refund handler |
| WhatsApp dunning | **Real** honesty gap — channel absent (`LP-MSG-003`) |
| LHDN “we have tax” | Backend only (`LP-TAX-009` un-hide is Wave 2) |

---

## What to wrap vs rebuild vs refuse

### WRAP (call Stripe, or leave the merchant on Stripe Dashboard)

Do these. They are adapter / docs / ops-copy work, not new companies.

1. **Keep hosted Checkout `mode=payment`** as the only Stripe UI. Copy metadata to Session **and** PaymentIntent (already done). `LP-PAY-001`.
2. **Off-session PaymentIntents** with idempotency keys and structured decline codes on `GatewayPaymentFailed`. `LP-PAY-008`, `LP-DUN-007`.
3. **SetupIntent** (or Checkout `mode=setup`) when a trial / RM0 coupon needs a card on file. `LP-COM-011`.
4. **Refund + dispute + payment_failed + refund.updated** event map in `ParseWebhookAsync`. `LP-PAY-007`, `LP-PAY-009`, `LP-PAY-010`.
5. **Customer Portal** by `VaultedCustomerId`, not `List(email, limit=1)`. `LP-COM-010`.
6. **Fee / FX / tax extract** on every success path (PI-only already expands `balance_transaction`; keep it; add `payment_method` details). `LP-PAY-011`.
7. **Dashboard-owned PMs**: document that FPX/GrabPay/Link/Apple Pay/Radar Lite/Adaptive Pricing/Authorization Boost are toggles **on the tenant’s Stripe account**, not Lazuar checkboxes. Add one ops paragraph so we stop getting “does Lazuar support FPX?” tickets. `LP-OPS-003`, `LP-PAY-001`.
8. **Pass `automatic_tax`** only if an export tenant asks and understands MY-seller limits.
9. **Never create `Stripe.Subscription` / `Stripe.Invoice` / `Stripe.Coupon`.**
10. **Capability flags** on `IPaymentGatewayAdapter` so Commerce stops hard-coding Stripe for portal. Reserved `LP-PAY-018`.

### REBUILD (Lazuar-native; this is the company)

1. **Multi-gateway cashier** — already the product. `LP-PAY-001`–`004`.
2. **Commerce subscription state machine** — one clock. Close failed-charge → `PAST_DUE` → dunning → recover. `LP-DUN-001`–`005`, `LP-PAY-007`.
3. **Coupons, catalog, custom payment links, M2M checkout** — already ours. `LP-COM-001`–`006`, `LP-DEV-007`.
4. **Double-entry ledger + net cash across Stripe+Billplz+CHIP+Razorpay** — Sigma cannot do this. `LP-TRU-001`–`004`.
5. **LHDN MyInvois + SST liability** — Stripe Tax cannot do this for MY businesses. `LP-TAX-001`–`010`. Wave 2 is un-hide, not greenfield.
6. **WhatsApp / email dunning** that works on CHIP **and** Stripe cards **and** degrades to “update payment link” on Billplz/FPX. `LP-MSG-001`, `LP-MSG-003`, `LP-DUN-009`.
7. **Outbound webhooks** with one HMAC for all rails. `LP-DEV-003`–`005`.
8. **Ops vault** for four gateways (exists). Add capability warnings (Billplz no vault; Stripe FPX no vault). `LP-OPS-003`.
9. **Developer DX** (key list, delivery logs, honesty errors like `KEY_MODE_MISMATCH`) — copy Stripe’s **feel**, not Stripe’s objects. `LP-DEV-001`–`007`.

### REFUSE (company-shape traps)

1. **Stripe Connect as Lazuar’s tenant model** / marketplace take-rate / application fees on GMV. `LP-XX-001`, `LP-XX-007`.
2. **Managed Payments / any MoR.** `LP-XX-001`.
3. **Elements** as default checkout (PCI + we become a UI kit).
4. **Stripe Billing as source of truth** (0.7% tax + dual clock).
5. **Metronome / streaming / agentic usage billing** as a 2026 Lazuar SKU. Reserved `LP-COM-014`.
6. **Terminal / Tap to Pay** on the CaaS engine.
7. **Treasury, Issuing, Capital, Identity, Atlas** as Lazuar features.
8. **Rebuilding Radar.**
9. **Rebuilding Payment Links** pixel-for-pixel (QR codes, buy buttons, 30 languages) instead of shipping reliable multi-rail links.
10. **DuitNow/TnG “on Stripe”** — they are not on Stripe; sell CHIP/Billplz. Reserved `LP-PAY-013` is a **local-rail** row, not a Stripe row.
11. **Website / link-in-bio / funnel builder** to “match Stripe Payment Links + Checkout studio.” `LP-XX-002`.
12. **Cheap FPX via Stripe** as a marketing claim. Use `LP-PAY-002`.

### IGNORE (not a 2026 SEA CaaS job)

Sigma, Data Pipeline, Stripe Database, Workflows, Climate, Financial Connections, Crypto Onramp, custom objects, Checkout studio, Agentic Commerce Suite, Stripe CLI clone, Identity, Atlas (except an optional docs link), Revenue Recognition SaaS.

### Sequencing (maps to file 20 waves)

| Wave | Stripe-relevant work | Why this wave |
|-----:|----------------------|---------------|
| **0** | Decline codes into failed events; refund webhooks; dispute GMV ledger; keep off-session metadata honest; success page already polls | Money loop must finish. `LP-PAY-007`–`010`, `LP-DUN-001`–`005`. |
| **1** | Sellable Stripe rail + ops copy (FPX ≠ recurring; expensive vs Billplz); fee fidelity; capability flags; portal-by-customer-id; M2M cashier already shipped | Stranger can integrate. `LP-PAY-001`, `LP-PAY-011`, `LP-PAY-018`, `LP-COM-010`, `LP-DEV-007`. |
| **2** | Do **not** wrap Stripe Tax. Un-hide LHDN. | Moat Stripe cannot copy. `LP-TAX-*`. |
| **3** | SetupIntent trials; proration; seats; cancel-at-period-end; MRR — **rebuild**, do not attach Stripe Billing | “Looks like Stripe Billing” without becoming Stripe Billing. `LP-COM-007`–`012`, `LP-TRU-005`. |
| **4** | WhatsApp send; extra rails (Fiuu/Xendit); custom domain; campaign snapshot; decline-code intelligence | Keep-list and extras. `LP-MSG-003`, `LP-PAY-004`/`014`, `LP-DUN-006`/`007`. |
| **—** | Everything in REFUSE / IGNORE | Never, unless constitution changes. |

---

## Tracker IDs

Official family list from [`20-sequencing-and-tracker-schema.md`](./20-sequencing-and-tracker-schema.md). Spoken short form may drop `LP-`; tracker cells must use the full id.

This section is the **promotion map** for `00-checklist-tracker.md` when the parent evaluation fills Stripe’s competitor column. It does not mint `SR-*`.

### Stripe-as-rail (wrap) → existing PAY / COM / DUN / DEV / UX / OPS

| ID | Feature (schema name) | Stripe implication | Ours seed (file 20) | Decision from this file |
|----|----------------------|--------------------|---------------------|-------------------------|
| `LP-PAY-001` | BYOK Stripe hosted checkout (cards, Apple/Google Pay via Stripe) | Core wrap. Dashboard owns PM list. Document FPX-on-Stripe is expensive and non-recurring. | shipped | **WRAP** (done). Deepen ops copy. |
| `LP-PAY-002` | BYOK Billplz hosted bill — FPX / MYR | Complementary cheap FPX. Not a Stripe feature. | partial | Keep. Do not replace with Stripe FPX. |
| `LP-PAY-003` | BYOK CHIP Collect hosted + recurring token | Complementary vault without Stripe. | partial | Keep. Recurring ICP that will not open Stripe. |
| `LP-PAY-004` | BYOK Razorpay | Stripe India is Preview. Keep adapter. | partial | Wave 4. Do not market IN. |
| `LP-PAY-005` | Inbound webhook verify, persist, structured log | Wrap `Stripe-Signature`. Map more event types. | shipped | **WRAP**. Add `payment_intent.payment_failed`, refund events. |
| `LP-PAY-006` | Business-key idempotency | Dual `checkout.session.completed` + `payment_intent.succeeded`. | shipped | **REBUILD** (done). Do not regress. |
| `LP-PAY-007` | Payment-failed published into Commerce | Adapter must emit `PAYMENT_FAILED` for Stripe PI failures, not only inbox off-session. | shipped | **WRAP** adapter map. Keep Commerce consumer. |
| `LP-PAY-008` | Off-session / vaulted renewal with metadata | Deepen: idempotency key, SCA `requires_action`, decline codes. | partial | **WRAP**. |
| `LP-PAY-009` | Full/partial refunds + ledger + tax reverse | Map `refund.updated` / `refund.failed`. Amount > 0 already. | partial | **WRAP**. |
| `LP-PAY-010` | Disputes / chargebacks first-class on **commerce GMV** | Stripe Smart Disputes stay in Dashboard. We need **our** GMV ledger, not utility-only clawback. | absent | **REBUILD** ledger; **WRAP** evidence. |
| `LP-PAY-011` | Gateway fee fidelity | Stripe expand works; Billplz is 0. Keep Stripe expand on all success paths. | partial | **WRAP** Stripe; Billplz is file 05. |
| `LP-PAY-012` | Encrypted BYOK secrets + soft-disable | Stripe `secret_key` in `ApiKey` column. `KEY_MODE_MISMATCH`. | shipped | **REBUILD** (done). |
| reserved `LP-PAY-013` | DuitNow QR as first-class rail | **Impossible on Stripe.** Local adapter later. | — | Not a Stripe wrap. |
| reserved `LP-PAY-014` | Fiuu adapter | Not Stripe. | — | Wave 4. |
| reserved `LP-PAY-016` | Two-phase raw intake vs fulfill | Stripe retries are noisy; raw store would help support. | — | Optional Wave 0 residual. |
| reserved `LP-PAY-018` | Capability matrix (portal/off-session/refund flags) | Stops hard-coding Stripe for portal. | — | **REBUILD** port. Promote if `01` agrees. |
| `LP-COM-010` | Customer portal self-serve: update PM, invoices, cancel | Fix lookup to `VaultedCustomerId`. Stripe portal is wrap-only for Stripe tenants. Agnostic PM update is rebuild. | partial | **WRAP** + **LATER REBUILD**. |
| `LP-COM-011` | Trials that vault a card and convert | SetupIntent / Checkout `setup` wrap. | absent | **WRAP later** (Wave 3). |
| `LP-DUN-007` | Decline-code-aware retry rules (static first, not ML) | Steal Stripe’s published hard-decline list. Do not buy Smart Retries. | absent | **WRAP** codes + **REBUILD** rules. Wave 4. |
| `LP-DUN-009` | Payment-method-aware campaigns (FPX vs card) | Stripe FPX and Billplz cannot AUTO_CHARGE. | partial | **REBUILD**. |
| `LP-DEV-007` | Payments M2M cashier | Integrators must not call Stripe directly if they chose Hub. | shipped | **REBUILD** (done). |
| `LP-UX-003` | Honest success: poll server status | Redirect `?success` is not paid. Stripe hosted success is a redirect. | shipped | **REBUILD** (done). Confirm custom-link path. |
| reserved `LP-UX-010` | Apple/Google Pay visibility on Stripe path | Dashboard + domain verify. | — | **WRAP** by docs. Promote if `09` wants a row. |
| `LP-OPS-003` | Payment gateway BYOK settings | Stripe secret + `whsec`. Add FPX/recurring warnings. | shipped | **REBUILD** copy. |
| `LP-OPS-005` | Support “what did this payment do?” timeline | Not a Stripe Dashboard clone. Join our events. | absent | **REBUILD**. |

### Stripe-as-rival (match / refuse) → COM / TAX / TRU / MSG / XX

| ID | Feature | Stripe analogue | Ours seed | Decision from this file |
|----|---------|-----------------|-----------|-------------------------|
| `LP-COM-001` | Product checkout links | Payment Links | shipped | **REBUILD**. Do not clone Stripe Links chrome. |
| `LP-COM-002` | Recurring subscriptions | Stripe Billing subscriptions | shipped | **REBUILD**. Never `Mode=subscription`. |
| `LP-COM-003` | One-time products / orders | Checkout payment mode | shipped | **REBUILD**. |
| `LP-COM-004` | Coupons | Stripe Coupons / promo codes | shipped | **REBUILD** our coupons. Do not create Stripe Coupons. |
| `LP-COM-005` | Custom payment links / B2B quotes | Stripe Quotes + Payment Links | partial | **REBUILD**. Wave 2 with tax un-hide. |
| `LP-COM-006` | Manual / offline mark-paid | n/a (Stripe is always online) | shipped | **REBUILD**. Differentiator vs Stripe-only shops. |
| `LP-COM-007` | Quantity / seats that survive renewal | Per-seat Prices | partial | **LATER** rebuild Wave 3. |
| `LP-COM-008` | Proration on plan change | Stripe proration | absent | **LATER** rebuild Wave 3. **Refuse** Stripe Billing attach. |
| `LP-COM-009` | Cancel at period end | Portal / `cancel_at_period_end` | absent | **LATER** rebuild Wave 3. |
| `LP-COM-012` | Plan upgrade / downgrade | Subscription item update | absent | **LATER** rebuild Wave 3. |
| reserved `LP-COM-014` | Usage-based | Metronome / Meters | — | Default **trap**. |
| reserved `LP-COM-015` | Multi-currency checkout | Adaptive Pricing | — | **WRAP** by omission until sold. |
| `LP-DUN-001`–`005`, `008` | Closed recovery loop | Smart Retries + invoice states | shipped / partial | **REBUILD**. Wave 0. |
| `LP-DUN-002` | Campaign builder | Billing automations | partial | **REBUILD**. WHATSAPP step is not a channel. |
| `LP-DUN-006` | Campaign run snapshot | Stripe freezes invoice retry policy | absent | **REBUILD** Wave 4. |
| `LP-DUN-010` | Funnel analytics by step | Billing recovery analytics | absent | **REBUILD** Wave 4. |
| `LP-TAX-001`–`010` | LHDN / SST / TIN / consolidation | Stripe Tax (insufficient for MY seller) | backend-only | **REBUILD**. **Refuse** Stripe Tax as MY engine. |
| `LP-TAX-009` | Un-hide invoicing nav | Stripe Invoicing Dashboard | absent | Wave 2 un-hide. Not a Stripe wrap. |
| reserved `LP-TAX-013` | GSTN/Coretax | Stripe Tax other countries | — | `LP-XX-009` until LHDN trusted. |
| `LP-TRU-001` | Double-entry on happy path | Stripe balance + Sigma | shipped | **REBUILD**. Cross-rail is the point. |
| `LP-TRU-005` | MRR / ARR from ledger | Stripe Billing metrics | absent | **LATER** rebuild Wave 3. Not Sigma. |
| reserved `LP-TRU-007` | Deferred revenue | Stripe Rev Rec 0.25% | parked | **IGNORE** Stripe product. Own job later. |
| `LP-MSG-001` | Email lifecycle + dunning | Stripe receipt / dunning email | shipped | **REBUILD** (Resend BYOK). |
| `LP-MSG-003` | WhatsApp Meta Cloud send | **No Stripe equivalent** | absent | **REBUILD** Wave 4. Differentiator. |
| `LP-DEV-001`–`006` | Keys, scopes, outbound, docs | Stripe Dashboard Developers | shipped / partial | **REBUILD** feel, not objects. |
| `LP-DEV-009` | LHDN SDKs | n/a | shipped | **REBUILD**. Moat. |
| `LP-UX-001`–`002` | MY / mobile checkout | Checkout conversion / Adaptive Pricing | partial | **REBUILD** craft on **our** pre-pay page. |
| `LP-UX-004` | Receipt PDF | Stripe receipts / post-pay invoice 0.4% | shipped | **REBUILD**. |
| `LP-UX-006` | Custom domain | Stripe custom domain $10/mo | absent | **LATER** Wave 4. Do not resell Stripe’s $10 domain. |
| `LP-XX-001` | Merchant of Record / GMV take-rate | Managed Payments, Connect fees | refuse | **REFUSE**. |
| `LP-XX-002` | Website / link-in-bio / funnel builder | Checkout studio + Payment Links marketing | refuse | **REFUSE**. |
| `LP-XX-007` | Marketplace / discover / take-rate | Connect marketplaces | refuse | **REFUSE**. |
| `LP-XX-009` | Multi-country tax before LHDN | Stripe Tax 80+ | refuse-until | **REFUSE** now. |
| `LP-XX-010` | Affiliate mass-payouts / BNPL / Web3 | Capital / Issuing / stablecoins | refuse | **REFUSE**. |
| `LP-XX-011` | Super-app 15 modules / ops AI as product | Stripe Console / Projects / MCP | refuse | **REFUSE**. |

### IDs this file does **not** mint

Do not add `SR-001`, `SX-001`, `STK-*`, or a “Stripe product” family. If `01` or the parent evaluation needs a child of `LP-PAY-001` (for example “ops copy: FPX is not recurring”), keep the parent and add `LP-PAY-019` only if the jobs can ship on different waves. Prefer a `Notes` cell on `LP-PAY-001`.

### Stripe column fill (for `00-checklist-tracker.md`)

When the tracker gains a **Str** (Stripe) competitor column — file 20 currently puts Stripe under **Glo** (global billing/checkout) rather than a dedicated column — fill cells as:

| Typical row | Stripe cell |
|-------------|-------------|
| `LP-PAY-001` | **Y** (they are the rail and the hosted page) |
| `LP-PAY-002` | **P** (they have FPX; rate/recurring lose to Billplz) |
| `LP-PAY-003` | **N** (CHIP is not Stripe) |
| `LP-PAY-005`–`009` | **Y** |
| `LP-PAY-010` | **Y** (Dashboard + Smart Disputes) |
| `LP-COM-001`–`004` | **Y** |
| `LP-COM-006` offline | **N** / **P** (Dashboard mark-paid is not the job) |
| `LP-COM-007`–`012` | **Y** |
| `LP-DUN-*` email/auto | **Y** (Smart Retries) |
| `LP-DUN` WhatsApp | **N** |
| `LP-TAX-*` MyInvois | **N** |
| `LP-TAX` remote digital SST | **Y** for non-MY sellers only |
| `LP-DEV-*` | **Y** (best-in-class) |
| `LP-MSG-003` | **N** |
| `LP-XX-001` MoR | **Y** (Managed Payments) — our verdict **Never** |
| `LP-XX-007` marketplace | **Y** (Connect) — our verdict **Never** |

Gap score **excludes** `refuse` and `n/a` (file 20). Do not let Stripe “win” 40 rows we Never wanted.

---

## Implications

### For Lazuar Pay (the product)

1. **Stop describing Stripe as “global” and Billplz as “Malaysia” as if Stripe had no FPX.** Stripe has FPX and it is **bad value** (3%+RM1, no mandate). The accurate line is: *Stripe = cards/Link/Apple Pay/Radar + expensive FPX; Billplz/CHIP = cheap local + (CHIP) recurring; Lazuar = one cashier + ledger + LHDN + dunning.* That sentence belongs on `LP-PAY-001` / `LP-OPS-003` and in the developers hub (`LP-DEV-006`).

2. **The adapter is the productization of Stripe-as-rail.** Treat `StripeGatewayAdapter.cs` as a compliance surface: metadata contract, idempotency, fee fidelity, event map. Most “we don’t match Stripe” slides die if those four are tight. Waves 0–1.

3. **Do not instantiate Stripe Billing.** The moment `Mode = "subscription"` or a `SubscriptionService.CreateAsync` lands, Commerce’s `NextBillingDate` is a lie and MYR 0.7% starts leaking. Dual clocks are how CaaS companies drown. Wave 3 exists to look like Billing **without** attaching it.

4. **Portal is a wrap bug, not a missing product.** Email `Limit=1` is the entire Stripe-portal gap. Fix the lookup (`LP-COM-010`); do not build a billing portal for Billplz.

5. **Dunning is the only place we should be proud to be “behind Stripe.”** Smart Retries are better ML. We can still win on **WhatsApp + CHIP + “update payment” for FPX**. That combination does not exist in Stripe Billing. Honesty: WhatsApp is **absent** as a channel today. Do not market the console logger.

6. **LHDN is the only tax conversation that matters for MY-incorporated tenants.** Stripe Tax’s MY row is a remote-seller digital SST calculator. Sessions 2026 “full MY support soon” is a watch item, not a reason to pause MyInvois. Wave 2 is un-hide.

7. **Connect is a mirror, not a roadmap.** Stripe Connect is what Lazuar would look like if we took 0.25%+ and onboarded sellers onto **our** Stripe platform. We chose the opposite (BYOK, 0%). Retired “Stripe Connect” copy must stay retired. `LP-XX-007`.

8. **Four adapters is the honest SEA story; Stripe is one of them.** Indonesia preview and PH/VN absence on Stripe are permanent-enough. Do not wait for Stripe to cover ASEAN. File 05 + Wave 4 rails.

9. **Do not become Stripe Dashboard.** `LP-OPS-002` KPIs and `LP-TRU-005` MRR are **our** cross-rail truth. Sigma cannot see Billplz.

10. **Sessions 2026 is a distraction engine.** Agentic Commerce, Checkout studio, Stripe Database, Metronome streaming, Treasury cards — none of that is Wave 0–2. File them under IGNORE unless a named integrator wants M2M (`LP-DEV-007`) as the agent’s cashier.

### For Aura (Hub customer, not competitor)

- Guest pay should keep **Billplz as default MY rail**. Offer Stripe when the salon is card-heavy or already has `sk_live_`.
- Do not send salon guests down Stripe FPX if Billplz is configured.
- Off-session (passes, memberships) requires Stripe **cards** or CHIP, not Billplz, not Stripe FPX.
- Paddle remains Aura System A. Stripe Billing is not a Paddle replacement either (`LP-XX-001`).
- This file does not reopen Aura’s archived guest-pay soak tracker. When Aura consumes Hub, it consumes `LP-PAY-001` / `LP-DEV-007`.

### For sales / positioning

**Say:** “Bring your Stripe key. We add Billplz/CHIP, a real ledger, LHDN e-invoice, and (when live) WhatsApp recovery. We don’t take a cut.”  
**Don’t say:** “We’re Stripe for Southeast Asia.” That invites a feature-for-feature loss on Radar, Sigma, Elements, and Dashboard.  
**Don’t say:** “We support FPX via Stripe” without the rate and the no-recurring caveat.  
**Don’t say:** “Native WhatsApp dunning” until `LP-MSG-003` is a real Meta Cloud send.  
**Don’t say:** “We do tax” in a demo that cannot un-hide LHDN (`LP-TAX-009`).

### Watch items (re-open this file if they ship)

| Watch | Why it would change a cell |
|-------|----------------------------|
| Stripe Tax **business location MY** + MyInvois | Could turn `LP-TAX-*` from “refuse Stripe Tax” to “wrap filing, still rebuild UBL” |
| Stripe FPX mandates / recurring | Would change `LP-PAY-001` recurring advice and `LP-DUN-009` |
| DuitNow QR on Stripe | Would change reserved `LP-PAY-013` from local-only to optional wrap |
| Stripe Payments DIY in ID/PH/VN | Would change multi-gateway priority vs Xendit/Midtrans |
| Checkout embedded form GA | Possible SAQ-A embed wrap if a customer demands in-page pay |
| Managed Payments + MY SST | Still Refuse for us (`LP-XX-001`); tenants might leave for MoR |
| Stripe Billing pause / prebilling GA | Still Refuse as SoT; might inform Wave 3 `LP-COM-009` UX |

### Final sentence

Stripe is the best **card rail** Lazuar can wrap and the worst **company** Lazuar could try to clone; the work is to deepen five adapter methods (`LP-PAY-001`, `005`–`011`, `LP-COM-010`) and to keep Commerce, the ledger, LHDN, and WhatsApp dunning (`LP-COM-*`, `LP-TRU-*`, `LP-TAX-*`, `LP-MSG-003`) as the products Stripe will not build for Malaysian BYOK founders.
