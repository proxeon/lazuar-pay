# 00 — Feature × competitor checklist

**Product:** Lazuar Pay (this repo)  
**Date:** 16 August 2026  
**Living file.** Flip cells when code changes. A row is not a commitment to ship.

Parent judgment: [00-evaluation.md](./00-evaluation.md). Evidence: uncondensed `01`–`20`.

---

## How to read the matrix

**Rows** = capabilities. **Columns** = us + the names a buyer actually shortlists.

| Column | Who |
|--------|-----|
| **Lazuar** | This codebase, honest today (not README ambition) |
| **Billplz** | MY bills / payment forms / cheap FPX (also our rail) |
| **CHIP** | MY Chip Collect hosted page + API (also our rail) |
| **HitPay** | SG/MY no-code links, invoices, recurring, wallets |
| **Xendit** | SEA gateway + subscriptions + e-mandate |
| **Stripe** | Checkout + Payment Links + Billing + Tax (MY-limited) |
| **Paddle** | Merchant of Record |
| **Chargebee** | Subscription billing engine |
| **Polar** | Developer MoR |

Local names **not** given a column (see reports 02, 05, 10): ToyyibPay, SenangPay, Fiuu, iPay88, GHL, Curlec, Midtrans, PayMongo, Airwallex, StoreHub, AutoCount, Xero, MyInvois portal. They appear in notes when a row is really about them.

### Marks

| Mark | Meaning |
|------|---------|
| **Y** | Yes — they have it, or we can honestly sell it |
| **P** | Partial / limited / only on some rails |
| **B** | Backend or `[MVP-HIDE]` only (Lazuar) |
| **N** | No |
| **R** | We refuse (Wave column **R**) |
| **W** | Wrap as a rail — do not rebuild |
| **—** | Not that product’s job |

### Waves

| Wave | Meaning |
|------|---------|
| **0** | Honesty / closed money loops |
| **1** | Sellable CaaS |
| **2** | Un-hide LHDN / invoices |
| **3** | Billing depth |
| **4** | More rails / WhatsApp / Xero |
| **R** | Refuse |
| **—** | Already shipped or not a build item |

---

## A. Positioning and commercial model

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-001 | BYOK (merchant keeps gateway keys) | — | Y | — | — | N | N | Y | N | Y | N |
| LP-002 | Merchant of Record (they are the seller) | R | R | N | N | N | N | N | Y | N | Y |
| LP-003 | Licensed acquirer / holds settlement | R | R | Y | Y | Y | Y | Y | Y | N | Y |
| LP-004 | SaaS fee (not take-rate on GMV) | 1 | P | N | N | N | N | P | N | Y | N |
| LP-005 | Prepaid utility credits (LHDN / WhatsApp) | 1 | P | N | N | N | N | N | N | N | N |
| LP-006 | Public self-serve signup + pricing page | 1 | N | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-007 | KYC onboarding (for *their* acquiring) | R | R | Y | Y | Y | Y | Y | Y | — | Y |

---

## B. Hosted checkout and payment links

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-010 | Shareable payment / checkout link | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-011 | Hosted checkout (name, email, amount) | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-012 | Promo / coupon at checkout | — | Y | N | P | P | P | Y | Y | Y | Y |
| LP-013 | Pay-what-you-want | — | Y | N | N | N | N | P | N | N | N |
| LP-014 | Quantity on checkout | 1 | P | N | N | Y | P | Y | P | Y | P |
| LP-015 | Order bump / one-click upsell | 3 | N | N | N | P | N | N | P | N | N |
| LP-016 | Abandoned-checkout reminder | 3 | N | N | N | P | P | Y | P | P | P |
| LP-017 | Custom domain on checkout | 3 | N | N | N | Y | P | Y | Y | Y | Y |
| LP-018 | Embed / overlay checkout (no full redirect) | 3 | N | N | P | Y | Y | Y | Y | Y | Y |
| LP-019 | Guest checkout (no buyer account) | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-020 | BM / EN localization | 1 | N | P | P | P | P | Y | Y | Y | P |
| LP-021 | Mobile-first / wallet QR on our page | 1 | N | P | P | Y | Y | P | N | N | N |
| LP-022 | Company + TIN fields on checkout | 2 | B | N | N | P | P | Y | Y | Y | Y |
| LP-023 | Address collection (configurable) | — | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-024 | Success page + fulfillment hook | 0 | P | P | P | Y | Y | Y | Y | Y | Y |
| LP-025 | Branding (logo, colors) on checkout | 1 | P | P | P | Y | Y | Y | Y | Y | Y |

---

## C. Payment rails (wrap, do not acquire)

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-030 | Cards Visa/Mastercard | — | W | P | Y | Y | Y | Y | Y | W | Y |
| LP-031 | FPX one-time (retail) | — | W | Y | Y | Y | Y | Y | N | W | N |
| LP-032 | FPX e-mandate (true auto-debit) | 4 | N | N | N | N | Y | N | N | W | N |
| LP-033 | DuitNow QR | 4 | N | P | P | Y | Y | N | N | W | N |
| LP-034 | Touch ’n Go eWallet | 4 | N | P | P | Y | Y | N | N | W | N |
| LP-035 | GrabPay | 4 | N | P | P | Y | Y | P | N | W | N |
| LP-036 | ShopeePay / Boost | 4 | N | N | P | Y | Y | N | N | W | N |
| LP-037 | Apple Pay / Google Pay | 1 | N | N | P | P | P | Y | P | W | P |
| LP-038 | PayPal | 4 | N | N | N | P | P | N | P | W | P |
| LP-039 | BNPL (Atome / Grab PayLater) | R | R | N | N | Y | Y | N | N | W | N |
| LP-040 | Multi-gateway per tenant (BYOK pick) | — | Y | N | N | N | N | N | N | Y | N |
| LP-041 | Stripe adapter | — | Y | — | — | — | — | — | — | W | — |
| LP-042 | Billplz adapter | — | Y | — | — | — | — | — | — | W | — |
| LP-043 | CHIP Collect adapter | — | Y | — | — | — | — | — | — | W | — |
| LP-044 | Razorpay / Curlec adapter | 4 | P | — | — | — | — | — | — | W | — |
| LP-045 | Xendit adapter | 4 | N | — | — | — | — | — | — | W | — |
| LP-046 | 3-D Secure / SCA | 1 | W | P | P | Y | Y | Y | Y | W | Y |
| LP-047 | Saved card / tokenization / off-session | 0 | P | N | P | P | Y | Y | Y | Y | Y |

Billplz cannot vault; ops product form already warns. Off-session today is Stripe/CHIP-shaped. FPX e-mandate is Curlec/Xendit, not us.

---

## D. Subscriptions

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-050 | One-time + monthly + yearly products | — | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-051 | Subscription records + statuses | — | Y | N | N | Y | Y | Y | Y | Y | Y |
| LP-052 | Automatic renewal (merchant-initiated) | 0 | P | N | P | P | Y | Y | Y | Y | Y |
| LP-053 | Reminder-only / “send link each cycle” | 1 | P | Y | P | Y | P | N | N | P | N |
| LP-054 | Free trial (`TRIALING`) | 3 | N | N | N | P | P | Y | Y | Y | Y |
| LP-055 | Cancel immediately | — | Y | — | — | Y | Y | Y | Y | Y | Y |
| LP-056 | Cancel at period end | 1 | N | — | — | P | P | Y | Y | Y | Y |
| LP-057 | Pause / resume | 3 | P | N | N | Y | P | P | Y | Y | P |
| LP-058 | Plan change | 3 | N | N | N | P | P | Y | Y | Y | Y |
| LP-059 | Proration | 3 | N | N | N | N | P | Y | Y | Y | P |
| LP-060 | Quantity / seats | 3 | N | N | N | P | P | Y | P | Y | P |
| LP-061 | Usage / metered billing | 4 | N | N | N | N | Y | Y | P | Y | Y |
| LP-062 | Setup fee / add-ons | 3 | N | N | N | P | P | Y | P | Y | P |
| LP-063 | Multiple prices per product | 3 | N | N | N | Y | Y | Y | Y | Y | Y |
| LP-064 | Import existing subscribers | 4 | N | N | N | Y | P | P | P | Y | P |
| LP-065 | Offline / manual payment sub | 1 | P | Y | N | Y | P | P | P | Y | N |

Statuses we persist: `PENDING`, `ACTIVE`, `PAST_DUE`, `SUSPENDED`, `CANCELED`. `TRIALING` is referenced in anonymize code only.

---

## E. Dunning and recovery

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-070 | Campaign builder (schedule + actions) | — | Y | N | N | P | P | P | P | Y | P |
| LP-071 | Auto-enter PAST_DUE on failed renewal | 0 | P | N | N | P | Y | Y | Y | Y | Y |
| LP-072 | Off-session retry (AUTO_CHARGE) | 0 | Y | N | P | P | Y | Y | Y | Y | Y |
| LP-073 | Email recovery sequence | 0 | P | P | P | P | P | Y | Y | Y | Y |
| LP-074 | WhatsApp recovery sequence | 4 | N | N | N | P | P | N | N | N | N |
| LP-075 | Magic update-payment link | — | Y | N | N | P | P | Y | Y | Y | Y |
| LP-076 | Hard vs soft decline handling | 3 | N | N | N | N | P | Y | P | Y | P |
| LP-077 | Recovered-revenue metrics | 0 | P | N | N | P | P | Y | Y | Y | P |
| LP-078 | Terminal action (suspend / cancel) | 0 | Y | N | N | P | Y | Y | Y | Y | Y |
| LP-079 | Campaign snapshot (don’t mutate in-flight) | 0 | Y | — | — | N | N | Y | P | Y | N |
| LP-080 | Pause dunning per subscriber | — | Y | N | N | P | P | P | P | Y | N |

WhatsApp transport is `ConsoleMessagingService` (log only). `Messaging:WhatsAppEnabled` defaults false. Do not mark LP-074 **Y**.

---

## F. Money movement after the charge

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-090 | Inbound webhook verify + idempotency | 0 | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-091 | Refund full | 1 | P | P | Y | Y | Y | Y | Y | Y | Y |
| LP-092 | Refund partial | 1 | P | N | P | Y | Y | Y | Y | Y | Y |
| LP-093 | Refund UI in merchant console | 1 | N | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-094 | Disputes / chargebacks as first-class | 3 | P | N | N | P | P | Y | Y | P | Y |
| LP-095 | Settlement / payout reports | R | R | Y | Y | Y | Y | Y | Y | — | Y |
| LP-096 | Multi-currency + FX | 4 | P | N | P | P | Y | Y | Y | Y | Y |
| LP-097 | Reconciliation export (CSV) | 1 | N | Y | Y | Y | Y | Y | Y | Y | Y |

Adapters implement `IssueRefundAsync` (Billplz stubbed/limited). Stripe dispute event is parsed. No ops refund button.

---

## G. Invoicing, quotes, receipts

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-100 | Commercial receipt / PDF | — | P | P | P | Y | Y | Y | Y | Y | Y |
| LP-101 | Sequential document numbers | 2 | P | P | P | Y | Y | Y | Y | Y | Y |
| LP-102 | Quotes / proforma / custom checkout session | 2 | B | N | N | Y | Y | Y | P | Y | N |
| LP-103 | Tax invoice (commercial) | 2 | B | N | N | Y | Y | Y | Y | Y | Y |
| LP-104 | Credit / debit / refund notes | 2 | B | N | N | P | P | Y | Y | Y | P |
| LP-105 | Payment terms / due date / AR reminders | 3 | N | Y | N | Y | Y | Y | P | Y | N |
| LP-106 | Buyer download of documents | 2 | B | P | P | Y | P | Y | Y | Y | Y |
| LP-107 | PDF branding | 2 | P | N | N | Y | P | Y | Y | Y | P |

Ops invoicing routes (Quotes, Tax Invoices, Credit Notes) exist and are **unrouted** (ADR 023). Portal “Download Tax Invoice” is `[MVP-HIDE]`.

---

## H. LHDN / SST / compliance (the moat)

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-110 | MyInvois submit (UBL 2.1) | 2 | B | N | N | N | N | N | N | N | N |
| LP-111 | Status poll VALID / INVALID | 2 | B | N | N | N | N | N | N | N | N |
| LP-112 | TIN / taxpayer validation | 2 | B | N | N | N | N | N | N | N | N |
| LP-113 | LHDN QR on validated invoice | 2 | B | N | N | N | N | N | N | N | N |
| LP-114 | B2C monthly consolidation | 2 | B | N | N | N | N | N | N | N | N |
| LP-115 | Self-billed documents (11–14) | 4 | B | N | N | N | N | N | N | N | N |
| LP-116 | Cancel / reject within IRBM rules | 2 | B | N | N | N | N | N | N | N | N |
| LP-117 | XAdES V1.1 signing | 2 | N | N | N | N | N | N | N | N | N |
| LP-118 | SST line codes | 2 | P | N | N | P | N | P | — | P | — |
| LP-119 | Export zero-rate (foreign buyer) | 4 | N | N | N | N | N | P | Y | P | Y |
| LP-120 | Stripe Tax / Avalara-class global tax | R | R | N | N | N | N | Y | Y | Y | Y |
| LP-121 | Xero / QuickBooks sync | 4 | N | N | N | Y | P | Y | P | Y | N |
| LP-122 | Merchant legal profile (TIN, BRN, address) | 2 | B | N | N | P | P | Y | Y | Y | Y |
| LP-123 | PDPA buyer-data deletion / anonymize | 1 | P | P | P | P | P | Y | Y | P | P |

StoreHub / AutoCount / Xero / MyInvois portal would score **Y** on LP-110–114 and **N** on checkout/dunning. They are the compliance-only column we did not add.

---

## I. Developer product

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-130 | Dashboard API keys (live/test, revoke) | — | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-131 | Scoped keys (least privilege) | 1 | P | N | N | P | Y | Y | Y | Y | Y |
| LP-132 | Outbound webhooks (tenant endpoints) | 0 | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-133 | Signed deliveries + retry + redrive | 0 | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-134 | Delivery logs UI | — | Y | N | N | P | Y | Y | Y | Y | P |
| LP-135 | Versioned event catalog in docs | 1 | P | P | P | P | Y | Y | Y | Y | Y |
| LP-136 | M2M create-checkout (integrator) | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-137 | M2M subscription admin API | 1 | N | N | N | P | Y | Y | Y | Y | Y |
| LP-138 | Official Payments/Commerce SDK | 4 | N | N | N | P | Y | Y | Y | Y | Y |
| LP-139 | LHDN SDK (npm + NuGet) | — | Y | N | N | N | N | N | N | N | N |
| LP-140 | Sample app (cashier) | — | Y | P | P | P | Y | Y | Y | Y | Y |
| LP-141 | Test clocks / time travel | 4 | N | N | N | N | N | Y | N | P | N |
| LP-142 | Idempotency-Key on POST | 1 | P | N | N | P | Y | Y | Y | Y | P |
| LP-143 | Connect / provision (Aura hop) | — | Y | N | N | P | Y | Y | N | N | N |
| LP-144 | Integration guides (not Scalar dump) | 1 | P | P | P | P | Y | Y | Y | Y | Y |

Keys live in `one.ApiCredentials`. Scopes today: `lhdn.documents:*`, `payments.checkouts:*`, `payments.config:read`, `webhooks.endpoints:manage`. Webhook headers: `X-Lazuar-Signature`, `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`.

---

## J. Communications

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-150 | BYO email (Resend) | — | Y | N | N | N | N | — | — | P | — |
| LP-151 | Receipt / failed-pay / magic-link email | 0 | Y | P | P | Y | P | Y | Y | Y | Y |
| LP-152 | Editable notification templates | — | Y | N | N | P | N | P | P | Y | P |
| LP-153 | Variable resolution actually works | 0 | P | — | — | Y | P | Y | Y | Y | Y |
| LP-154 | Suppression (bounce / complaint) | 1 | P | N | N | P | P | Y | Y | P | P |
| LP-155 | WhatsApp via Meta Cloud | 4 | N | N | N | P | P | N | N | N | N |
| LP-156 | SMS | R | R | N | N | P | P | N | N | P | N |
| LP-157 | Marketing campaigns / blasts | R | R | N | N | P | N | N | P | P | N |

---

## K. Merchant dashboard, analytics, CRM

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-160 | Dashboard: net cash, actives, past due | — | Y | P | P | Y | Y | Y | Y | Y | Y |
| LP-161 | Honest MRR / ARR (ledger-based) | 3 | P | N | N | P | P | Y | Y | Y | Y |
| LP-162 | Churn / ARPU (directional) | — | P | N | N | P | P | Y | Y | Y | Y |
| LP-163 | Transaction log | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-164 | Subscriber list + detail | — | Y | N | N | Y | Y | Y | Y | Y | Y |
| LP-165 | Cohort / geo analytics | 4 | N | N | N | N | P | Y | P | Y | P |
| LP-166 | Staff roles beyond admin | 3 | P | P | P | Y | Y | Y | Y | Y | P |
| LP-167 | Audit log | 3 | N | N | N | P | Y | Y | P | Y | P |
| LP-168 | CRM (profiles, notes, segments) | R | P | N | N | P | N | P | P | P | N |
| LP-169 | Multi-workspace | — | Y | P | P | Y | Y | Y | Y | Y | Y |

CRM is a thin `ClientProfile` for checkout identity + PDPA anonymize. Do not grow it into HubSpot.

---

## L. Buyer portal

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-170 | Magic-link buyer portal | — | Y | N | N | P | N | Y | Y | Y | Y |
| LP-171 | See subscriptions + status | — | Y | N | N | Y | P | Y | Y | Y | Y |
| LP-172 | Cancel from portal | — | Y | N | N | P | P | Y | Y | Y | Y |
| LP-173 | Update payment method | 1 | P | N | N | P | P | Y | Y | Y | Y |
| LP-174 | Change plan from portal | 3 | N | N | N | N | N | Y | Y | Y | P |
| LP-175 | Invoice / receipt history | 2 | B | N | N | Y | P | Y | Y | Y | Y |

Update-payment exists as `/update-payment/[subId]` (dunning), not as a first-class portal card.

---

## M. Trust, legal, onboarding

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-180 | Public terms / privacy / refund pages | — | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-181 | Status page / SLA | 4 | N | P | P | P | Y | Y | Y | Y | P |
| LP-182 | Sandbox + test keys | 1 | P | Y | Y | Y | Y | Y | Y | Y | Y |
| LP-183 | Time-to-first-checkout < 15 min | 1 | P | Y | Y | Y | P | Y | Y | P | Y |
| LP-184 | Self-serve workspace create | 1 | P | Y | Y | Y | Y | Y | Y | Y | Y |

---

## N. Refuse list (competitors may have these; we will not)

| ID | Feature | Wave | Lazuar | Billplz | CHIP | HitPay | Xendit | Stripe | Paddle | Chargebee | Polar |
|----|---------|------|--------|---------|------|--------|--------|--------|--------|-----------|-------|
| LP-200 | Website / online store builder | R | R | N | N | Y | N | P | N | N | N |
| LP-201 | Link-in-bio / funnel builder | R | R | N | N | N | N | N | N | N | N |
| LP-202 | POS / tap-to-pay / hardware | R | R | N | N | Y | N | Y | N | N | N |
| LP-203 | Marketplace / multi-vendor split | R | R | N | N | P | Y | Y | N | N | N |
| LP-204 | Community DRM / Telegram bouncer | R | R | N | N | N | N | N | N | N | P |
| LP-205 | Course / membership CMS | R | R | N | N | N | N | N | N | N | P |
| LP-206 | Full accounting / GL replacement | R | R | N | N | N | N | N | N | P | N |
| LP-207 | Crypto / USDC settlement | R | R | N | N | N | N | P | N | N | P |
| LP-208 | Escrow / e-sign at checkout | 4 | N | N | N | N | N | N | N | P | N |
| LP-209 | India GSTN / Indonesia Coretax | 4 | N | N | N | N | N | N | N | P | N |
| LP-210 | Affiliates / mass payouts | 4 | N | N | N | P | Y | P | Y | P | P |

LP-208 and LP-210 are **delay**, not refuse. They stay off the MVP board. LP-209 waits until MyInvois is a sold feature.

---

## Priority backlog (tracker → later implementation)

Work top-down. Do not start Wave 2 chrome before Wave 0 loops, or Wave 4 adapters before Wave 1 DX.

### Wave 0 — close loops

| ID | Do this |
|----|---------|
| LP-071, LP-072, LP-078 | Failed vaulted renewal enters a dunning run; success exits it |
| LP-073, LP-153 | Email steps send with resolved variables and real links |
| LP-079 | Snapshot campaign at run start |
| LP-090 | Inbound webhooks: received vs fulfilled, business-key dedupe |
| LP-132, LP-133 | Outbound: no silent drop, redrive from logs |
| LP-024 | Success page only after payment truth |
| LP-047 | Honest vault story (Stripe/CHIP vs Billplz reminder-only) |

### Wave 1 — sellable CaaS

| ID | Do this |
|----|---------|
| LP-020, LP-021, LP-025 | Checkout conversion (BM/EN, mobile, branding) |
| LP-053, LP-065 | First-class reminder-only / offline renewals |
| LP-056 | Cancel at period end |
| LP-091–093 | Refunds from ops |
| LP-131, LP-135, LP-137, LP-144 | Keys, catalog, M2M subs, guides |
| LP-006, LP-183, LP-184 | Pricing + self-serve time-to-first-link |
| LP-173 | Portal: update payment |

### Wave 2 — compliance product

| ID | Do this |
|----|---------|
| LP-022, LP-122 | TIN + legal profile |
| LP-102–106 | Un-hide quotes / invoices / notes / downloads |
| LP-110–116 | MyInvois loop in the UI |
| LP-117 | V1.1 signing when we have a cert |
| LP-118 | SST codes |

### Wave 3 — billing depth

| ID | Do this |
|----|---------|
| LP-054 | Trials |
| LP-058, LP-059 | Plan change + proration (or explicit next-renewal-only) |
| LP-060, LP-063 | Seats / multi-price |
| LP-161 | Ledger MRR |
| LP-174 | Portal plan change |

### Wave 4 — rails and channels

| ID | Do this |
|----|---------|
| LP-032, LP-045 | Xendit + e-mandate |
| LP-033–037 | QR / wallets / Apple Pay via wrapped gateway |
| LP-074, LP-155 | Real WhatsApp or delete the claim |
| LP-121 | Xero |
| LP-044 | Finish Curlec/Razorpay as e-mandate rail |

---

## How to update

1. Change **Lazuar** cells only with a file path in the matching `01`–`20` report or a PR.
2. Re-score a competitor column when their public docs change (date the edit).
3. New feature → next free ID in that section (`LP-08x`, `LP-14x`, …). Do not reuse IDs.
4. New shortlist name (e.g. Xendit becomes table-stakes in every MY deal) → add a column and fill every row.
5. Implementation programs live elsewhere. This file stays a tracker.
