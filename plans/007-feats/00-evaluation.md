# 00 — Parent evaluation: Lazuar Pay vs the market

**Date:** 16 August 2026  
**Codebase:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**This file is the parent judgment.** The twenty reports in `01`–`20` are the uncondensed evidence. Do not treat this file as a substitute for those reports.

---

## 1. What we actually are

Lazuar Pay is a **headless checkout, subscription, and compliance engine**. Merchants keep their own gateway keys (BYOK). Money settles to *their* Billplz / Stripe / CHIP / Razorpay accounts. We sell software and metered infrastructure (credits), not acquiring and not Merchant-of-Record tax remittance.

The shipping identity is ADR 021 (Compliance CaaS) plus ADR 023 (Pure CaaS MVP: hide LHDN/B2B UI). The README still markets four pillars — multi-gateway orchestration, double-entry ledger, WhatsApp dunning, LHDN e-invoice. Only the first is honestly sellable today. The ledger exists but is not yet audit-grade. Dunning has a campaign UI. WhatsApp is a console stub (`ConsoleMessagingService`). LHDN is a serious backend (UBL 2.1 templates, MyInvois submit/poll/TIN, B2C consolidation job) with **no merchant nav**.

Surfaces that exist:

| App | Job |
|-----|-----|
| `lazuar-ops` | Merchant console: checkout links, subscribers, transactions, coupons, dunning campaigns, templates, API keys, outbound webhooks, delivery logs, BYOK gateways, Resend |
| `lazuar-portal` | Hosted checkout, success, magic-link buyer portal (list + cancel), update-payment |
| `lazuar-admin` | Platform control plane |
| `lazuar-developers` | Scalar OpenAPI hub |
| `lazuar-api` | .NET 10 modular monolith: Payments, Commerce, Billing, Lhdn, Communications, Messaging, CRM, One, Ops |
| `examples/hub-cashier-next` | Integrator sample: M2M checkout + signed webhook unlock |

Aura (salon) is a **Hub customer** via Connect/provision, not a rival. Do not mix Aura guest-booking features into this tracker.

---

## 2. The job we are hired for

Malaysian and SEA operators (creators, indie SaaS, agencies, and later salons via Aura) hire a stack to:

1. Take money on **FPX / cards / e-wallets** without building gateway glue.
2. Run **subscriptions** that actually renew, and recover failed payments.
3. Stay **legally invoiced** (LHDN MyInvois, SST) without a clerk in Excel.
4. Unlock access in *their* app via **API keys + signed webhooks**.

No single incumbent owns all four. That is the only reason this product should exist.

---

## 3. Who we compete with

Treat competitors as four kinds of opponent. Mixing the kinds produces a bad roadmap (building a POS because HitPay has one, or becoming MoR because Paddle is).

### 3.1 Direct product rivals (same job, different packaging)

These are the names a merchant will put next to us on a spreadsheet.

| Rival | Geography | Why they win deals | Why we can still exist |
|-------|-----------|--------------------|------------------------|
| **HitPay** | SG + MY | No-code payment links, invoices, recurring, FPX + DuitNow QR + TnG/Grab/Shopee/Boost, next-day MYR, zero monthly fee, CSV bulk | They are an **acquirer + SMB suite**. FPX recurring is still mostly “send a link each cycle,” not e-mandate. No LHDN-at-POS, no BYOK multi-gateway ledger, weak usage/proration. |
| **Xendit** | SEA (MY via Payex) | Real recurring rails (cards, FPX e-mandate, e-wallet mandates), usage billing, webhooks, SEA one-API | They are a **licensed gateway**. We should **wrap** them later, not clone xenPlatform. They do not file MyInvois. |
| **Billplz payment forms / bills** | MY | “Just send a bill.” Cheap FPX. Every NGO, tuition centre, and WhatsApp seller already has an account. | Dumb pipe. No subscription state machine, no dunning engine, no LHDN, no card vault, no integrator product. We already use them as a **rail**. |
| **CHIP Collect pages** | MY | Modern hosted page, API, tokenization we already adapt | Same as Billplz: rail, not OS. |
| **Stripe Payment Links + Billing** | Global + MY (limited) | Default for developers. Customer portal, Smart Retries, coupons, tax, docs that define the category. | Stripe FPX is one-time and **not supported on Stripe Billing**. No DuitNow QR / TnG. Card + Billing fees are expensive in MY. No MyInvois. |
| **Paddle / Polar / Lemon Squeezy** | Global SaaS | One contract, they remit VAT/GST, overlay checkout, entitlements | MoR take-rate 5%+ and they become the seller of record. That **breaks LHDN** (the Malaysian seller must issue the e-invoice) and is hostile to FPX settlement into the merchant’s own bank. ADR 019/021 already refused this. |

### 3.2 Local rails and substitutes (Malaysia)

Not “products like us,” but they steal the same budget and habit.

**Acquiring / payment forms**

- **ToyyibPay** — cheap, Shariah-positioned, SMEs and masjid/NGO. Rival for “send a link,” not for SaaS billing.
- **SenangPay** — SME hosted payments, WooCommerce.
- **Fiuu** (ex Razer Merchant Services / MOLPay) — broad methods, enterprise e-commerce.
- **iPay88**, **GHL ePayments** — retail/enterprise acquiring.
- **Curlec (Razorpay Malaysia)** — the adult answer for **FPX e-mandate**. We already have a Razorpay adapter; e-mandate as a first-class subscription rail is a later wave.
- **Revenue Monster**, **Boost / TnG / GrabPay merchant**, **PayNet DuitNow** — wallets and scheme rails.

**Informal stack (the real local incumbent)**

WhatsApp catalogue + Instagram checkout + a Billplz/ToyyibPay link + Excel + the **MyInvois portal** at month-end. We do not beat this with more settings pages. We beat it by making the Billplz click *also* create a subscription, a receipt, and (later) a legal invoice.

**Compliance-only rivals**

- LHDN **MyInvois** portal (manual)
- **StoreHub**, **EasyStore** (POS/e-comm + intermediary e-invoice)
- **AutoCount**, **SQL Account**, **UBS**, **FastAccount**
- **Xero** + MY e-invoice intermediary
- Dedicated MyInvois APIs / PEPPOL access points

These own “file the tax.” They do not own the Buy button. ADR 021’s wedge is **compliance at the point of sale**. Until we un-hide LHDN, we are not in this fight.

### 3.3 Global category gravity

Even merchants who will never buy Chargebee still judge us against Stripe’s feature list.

| Category | Leaders | Our relationship |
|----------|---------|------------------|
| Payment OS | Stripe, Adyen, Checkout.com | **Wrap** (already Stripe). Do not acquire. |
| MoR | Paddle, Polar, FastSpring, Lemon Squeezy | **Refuse** becoming them. Steal UX: overlay checkout, customer portal, tax ID fields. |
| Subscription billing | Chargebee, Recurly, Maxio, Lago, Stripe Billing | **Subset**. Copy the taxonomy (states, proration, dunning, portal). Do not copy CPQ, Salesforce, RevRec year one. |
| Creator checkout | Gumroad, Payhip, ThriveCart, SamCart | Steal conversion patterns (bumps, abandoned). Refuse site builders. |
| Global tax | Stripe Tax, Avalara, Anrok | Wrong XML. They do not do MyInvois UBL 2.1. |

### 3.4 Who we lose deals to (honest)

| Buyer | They pick | Because |
|-------|-----------|---------|
| Tuition / NGO / WhatsApp seller | Billplz or ToyyibPay | Faster, cheaper, already logged in |
| Gym / studio / tuition with no engineer | **HitPay** | Dashboard + QR + wallets + invoices + no monthly fee |
| SEA multi-country SaaS | **Xendit** | One API, e-mandate, wallet mandates |
| Indie global SaaS | **Stripe** or **Polar** | Docs, portal, tax, GitHub-native entitlements |
| Series B finance team | **Chargebee** + Stripe | Proration, quotes, RevRec, Salesforce |
| Malaysian SME accountant | **Xero / AutoCount** + a gateway | They buy compliance from the GL, not from checkout |

We win when the buyer is: **a Malaysian (or SEA) software-shaped business that must take FPX, must run subscriptions, must file MyInvois, and must unlock a third-party app** — and does not want to give 5% and legal seller status to Paddle.

That ICP is narrow. Keep it narrow.

---

## 4. How we compare (compressed)

Full cells live in [00-checklist-tracker.md](./00-checklist-tracker.md). This is the shape.

| Layer | Them | Us today | Verdict |
|-------|------|----------|---------|
| Take money (one-time, MY) | HitPay/Billplz/Xendit: FPX + QR + wallets on one page | Hosted portal checkout → redirect to **one** BYOK gateway | We convert worse. Buyer sees our form, then Billplz/Stripe. No first-class DuitNow QR / Apple Pay / wallet buttons. |
| Recurring | Xendit/Curlec: e-mandate + tokens. HitPay: link-per-cycle. Stripe Billing: cards only in MY | Products: one-time / monthly / yearly, FIXED or PWYW. Subs: PENDING/ACTIVE/PAST_DUE/SUSPENDED/CANCELED. Billplz cannot vault (ops UI already warns this). `TRIALING` is mentioned once, no trial product field. No proration, no usage, no plan change. | Enough for Aura-class retainers. Not enough for Chargebee-class SaaS. |
| Recovery | Stripe Smart Retries; Xendit configurable retry; Chargebee dunning | Campaign builder + `DunningEngineJob` + off-session charge event + update-payment route. WhatsApp is **log-only**. Email depends on tenant Resend. Failed-renewal → PAST_DUE is better than the 2026-08-03 gap doc claimed; remaining holes are snapshot, hard/soft decline, pre-dunning catch-up. | Sell “dunning campaigns.” Do **not** sell “WhatsApp dunning.” |
| Compliance | StoreHub/Xero/AutoCount file MyInvois. Gateways mostly do not. | Lhdn module is the deepest code in the repo (XSD, Scriban UBL, strategies for 01–04 and 11–14, TIN, poll, consolidation). UI lobotomized. Unsigned V1.0; V1.1 XAdES not on. | Moat is **inventory**, not **product**. Wave 2 is turning inventory into a sale. |
| Developer | Stripe/Xendit/Polar: keys, SDKs, event catalog, test clocks | One-owned `sk_` keys + scopes; Standard Webhooks–style HMAC; ops pages for keys/webhooks/logs; cashier sample; TypeSpec. Commerce M2M is thin vs admin JWT. Developers hub is still Scalar-heavy. | Closest thing we have to a wedge besides LHDN. Finish this before more rails. |
| Money truth | Stripe Sigma / Chargebee RevRec | Double-entry + credit wallet + PDF receipts. Deferred revenue job parked. | Keep the ledger. Do not advertise “CFO OS.” |
| Commercial model | HitPay: MDR, $0 SaaS. Stripe: MDR + Billing %. Paddle: 5% MoR. Chargebee: expensive SaaS. | Flat SaaS + prepaid credits (ADR 019). No public pricing page in this repo. | Correct model for BYOK. We must not race HitPay on MDR — we do not take MDR. |

---

## 5. What to implement (later — tracker, not a sprint)

Sequence is **honesty → closed loops → sellable CaaS → un-hide the moat → billing depth → more rails**. Copying HitPay’s POS or Paddle’s MoR in the middle of that sequence is how the 15-app era died.

### Wave 0 — Stop lying about money

Close loops we already draw in the UI.

- Dunning run snapshots so mid-flight campaign edits do not skip or spam.
- Successful recovery (magic link *or* off-session) always exits dunning, advances period, attributes metrics.
- Inbound gateway webhooks: received ≠ fulfilled; business-key idempotency; refunds/disputes visible.
- Outbound webhooks: no silent drop; redrive from delivery logs.
- Ledger + credit wallet: no double post on LHDN/top-up/refund paths we already ship.
- Marketing copy and README: WhatsApp and LHDN marked roadmap.

Evidence: `docs/001-gaps/00-what-we-need-to-do-next.md`, reports 01, 12, 13, 14.

### Wave 1 — Sellable CaaS (compete with HitPay/Stripe Payment Links without becoming them)

- Checkout conversion: fewer fields, BM/EN, mobile, show **which rail** the buyer will use, Apple/Google Pay when the active gateway supports it.
- Honest Billplz path: reminder-only renewals (link each cycle) as a first-class mode, not a surprise.
- Developer product: scoped keys, event catalog in docs (not only Scalar), cashier sample as the golden path, Commerce M2M for “create checkout / read subscription.”
- Buyer portal: update payment method, invoices/receipts download, cancel-at-period-end (not only immediate).
- Refund from ops (full, then partial) with ledger reverse.

This is the first wave a stranger can pay us for.

### Wave 2 — Turn LHDN on (the actual moat)

- Un-hide invoicing + legal profile (`[MVP-HIDE]`).
- Checkout TIN / company for B2B products.
- Submit → poll → VALID/INVALID → QR on receipt → buyer download.
- B2C monthly consolidation visible and explainable.
- Credit/debit/refund notes tied to original UUID.
- V1.1 signing when we have a real `.p12`.
- SST codes on lines. Export zero-rate for foreign buyers only after B2B works.

Until Wave 2 ships, “Compliance CaaS” is an ADR, not a product.

### Wave 3 — Billing completeness (Chargebee-shaped, 10% of Chargebee)

- Trials with `TRIALING`.
- Plan change + simple proration (or explicit “change at next renewal only”).
- Quantity / seats.
- Coupons already exist — apply to renewals, not only first checkout.
- MRR/ARR from the **ledger**, not only `SUM(product.price)`.
- Pause that is a product action, not only dunning pause.

### Wave 4 — Rails and recovery channels

- Xendit adapter (SEA + e-mandate + wallets) as BYOK.
- Curlec/Razorpay e-mandate as the real FPX recurring rail.
- DuitNow QR / TnG / Grab when a wrapped gateway exposes them.
- Real WhatsApp (Meta Cloud) **or** permanently demote it.
- Xero sync (ADR 021 “keep”).
- Fiuu only if a tenant demands methods we cannot reach via Xendit/CHIP.

### Never (see report 19)

Website builder, link-in-bio, community DRM, marketplace/Connect-for-platforms, becoming the acquirer, POS hardware, email marketing blasts, full ERP, crypto settlement as a near-term claim, GSTN/Coretax before MyInvois is a sold feature.

---

## 6. Wrap vs rebuild (the most expensive decision)

| Competitor capability | Do this |
|-----------------------|---------|
| FPX, cards, wallets, QR | **Wrap** the gateway that already has them |
| Smart card retries | Use Stripe’s; do not train an ML model |
| Global VAT/GST remittance | Tell global-only sellers to use Paddle/Polar. We do not become MoR. |
| MyInvois UBL + QR | **Rebuild / keep ours.** Nobody we wrap will do this at POS. |
| Subscription state machine | **Ours.** Gateways are bad at this (Billplz especially). |
| Hosted checkout chrome | **Ours**, thin. Redirect to gateway hosted page is fine for FPX. |
| Dunning policy + WhatsApp | **Ours** (policy). Channel providers are Resend / Meta. |
| Dashboard MRR | **Ours**, small. Do not build ChartMogul. |
| Invoicing PDF | **Ours**, because it must become an LHDN document. |

---

## 7. How to use the tracker

1. Read this file for direction.
2. Use [00-checklist-tracker.md](./00-checklist-tracker.md) as the living matrix. Flip a cell when code changes. Do not flip a cell because a README claims it.
3. When implementing, open the matching `01`–`20` report. Those files stay long on purpose.
4. New competitor feature → add a row, do not add a column unless they are a lasting name on the buyer’s shortlist.
5. `R` in the Wave column means refuse. A competitor `Y` in that row is not a request.

Status vocabulary (same as the tracker):

| Mark | Meaning |
|------|---------|
| **Y** | Yes — shipped and honest to sell (or they clearly have it) |
| **P** | Partial / limited / dishonest if marketed as full |
| **B** | Backend or hidden UI only (us) |
| **N** | No |
| **R** | We refuse to build this |
| **W** | Wrap their rail; do not rebuild |
| **—** | Not applicable to that product |

Waves: **0** honesty, **1** sellable CaaS, **2** LHDN UI, **3** billing depth, **4** rails/channels, **R** refuse.

---

## 8. Standing constraints

- BYOK. Tenant keys in KMS. We never hold merchant settlement.
- Single API replica until workers are multi-instance safe (`TODO.md`).
- TypeSpec describes what ships. Phantom ops buttons get endpoints or get deleted.
- Aura provision (`INTEGRATOR_PROVISION_SECRET`) is an integrator path, not a second product.
- Do not delete Paddle from Aura, or point Billplz at Aura `/webhooks/gateway/*`.
- Do not scale a feature factory against HitPay’s SMB suite. They will always have more buttons.

---

## 9. Bottom line

The codebase is a **credible CaaS skeleton with a hidden compliance engine**. The market is not short of payment links. It is short of a Malaysian-native layer that sits **above** Billplz/Stripe/Xendit, runs subscriptions honestly, recovers failed payments, files MyInvois from the same ledger, and lets a developer unlock their own app.

HitPay and Billplz will beat us on “send a link this afternoon.” Stripe and Polar will beat us on global developer fashion. Chargebee will beat us on enterprise billing. Xero will beat us on the GL.

We beat them only on the **intersection**. Build the intersection. Track everything else so we know what we are refusing.
