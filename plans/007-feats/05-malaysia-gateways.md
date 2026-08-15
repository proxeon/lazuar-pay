# 05 — Malaysia payment gateways vs Lazuar

**Program:** `plans/007-feats` — competitor-feature research for **Lazuar Pay** (Hub cashier / CaaS), not a ship ticket.  
**Date:** 2026-08-16  
**Status:** Full uncondensed analysis. **No product code from this file.**  
**Workspace inspected:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Audience:** product + payments engineering deciding which Malaysian acquiring / payment-form products are **rails** (BYOK adapters) versus **rival checkout / link products** we must not clone.

**Standing constraints (do not contradict):**

- Guest money (System B / Lazuar Pay / merchant Billplz-CHIP-etc.) is **not** SaaS money (System A / Paddle).
- Lazuar is **BYOK, not Merchant of Record**. We do not take an 8% GMV cut. Money is supposed to land in the merchant’s own acquiring account.
- Production guest fulfillment for Aura is **not claimed** until a sandbox three-book soak. `HUB_PAYMENTS_DEFAULT_NEW_ORGS_TO_HUB` stays false.
- Do not become a marketplace, a locked acquirer (Fresha Payments / Toast / Square), or a second Billplz Catalog.
- Do not mix LHDN (tax document) with gateway settlement. A paid Billplz bill is not a MyInvois invoice.
- Do not promise card-on-file / no-show auto-charge on FPX-only rails.
- README marketing still lists Fiuu as a “Local Asian Gateway (BYOK)”. **That is a claim, not an adapter.** Shipping truth is Stripe + Billplz + CHIP + Razorpay only.
- Do not become a website builder, POS, or ERP to “match competitors.”
- Aura (salon) is a **customer** of Hub, not a competitor.

This file answers who Malaysian merchants compare us to when they say “I already have Billplz / CHIP / Toyyib / senangPay / Fiuu / iPay88 / GHL / Curlec / Revenue Monster.” Adjacent chapters in this program: [`04-stripe.md`](./04-stripe.md), [`06-sea-fintech-platforms.md`](./06-sea-fintech-platforms.md), [`09-checkout-and-payment-links.md`](./09-checkout-and-payment-links.md), [`13-payments-refunds-rails.md`](./13-payments-refunds-rails.md).

---

## Method

### What was read in our repo

| Artifact | Absolute path | Why it matters |
|----------|---------------|----------------|
| Payments module README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/README.md` | Declared job: cashier / gateway orchestrator, not ledger, not fulfillment. Mentions “Stripe, Billplz, FPX, Curlec” — Curlec is named, not implemented as a distinct type. |
| Adapter port | `…/Modules/Payments/Application/Ports/IPaymentGatewayAdapter.cs` | One interface: checkout, parse webhook, refund, customer portal, off-session charge. Capability-blind. |
| Factory | `…/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs` | String match on `GatewayType`. Unknown type throws. |
| Billplz adapter | `…/Gateways/BillplzGatewayAdapter.cs` | v3 bills, HMAC x_signature, query-string metadata, refund always `false`, off-session throws. |
| Billplz public base | `…/Gateways/BillplzPublicBase.cs` | Production hosts `api.lazuar.com`, `pay.lazuar.com`, `hub.lazuar.com`. `App:BillplzEnvironment` override. Insecure callback refused unless flag. |
| CHIP adapter | `…/Gateways/ChipCollectGatewayAdapter.cs` | `gate.chip-in.asia/api/v1` purchases, RSA webhooks, refunds, off-session token charge. |
| Razorpay adapter | `…/Gateways/RazorpayGatewayAdapter.cs` | Official `Razorpay.Api` SDK. Payment Links + registration links. Dummy off-session contact. |
| Stripe adapter | `…/Gateways/StripeGatewayAdapter.cs` | Present; out of this MY-PG set except as DX contrast. |
| Webhook allow-list | `…/Infrastructure/Endpoints.cs` | `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP` only. Other `{gatewayType}` → 400. |
| M2M cashier | `…/Infrastructure/IntegrationEndpoints.cs` | `POST/GET /integrations/payments/checkouts`, `GET /me`. |
| Platform / Ops config | `…/Infrastructure/PlatformEndpoints.cs` | `GET/PUT /payment-config`. |
| CHIP auto-provision | `…/Commands/UpdatePaymentConfigCommandHandler.cs` | On CHIP key save: fetch RSA public key, register webhook events. |
| Payments quickstart | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/payments-integration-quickstart.md` | Integrator surface. Billplz sandbox-vs-live gap documented. |
| ADR 009 | `docs/architecture-decision-log/009-stateless-webhook-metadata-transmission.md` | Billplz strips metadata; query-string reconstruction. |
| ADR 019 | `docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | CaaS + BYOK + LHDN + WhatsApp dunning as the intended moat. |
| ADR 020 | `docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` | Wishlist names Fiuu + SenangPay + ChipCollect as Phase-1 MY targets. Watermarked: not shipping truth. |
| Gap 06 / 20 | `docs/001-gaps/06-payments-module.md`, `docs/001-gaps/20-architecture-intent-vs-implementation.md` | Adapter matrix; Fiuu/SenangPay **not** implemented. |
| Portal checkout | `apps/lazuar-portal/src/modules/checkout/…`, `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | Merchant-facing CaaS form. Custom pay-by-session route is `[MVP-HIDE]` / `notFound()`. |
| LHDN module | `apps/lazuar-api/Modules/Lhdn/README.md` | First-class MyInvois UBL 2.1. Separate product line from Payments. |
| Tracker schema | `plans/007-feats/20-sequencing-and-tracker-schema.md` | ID space for this program. |
| Checklist | `plans/007-feats/00-checklist-tracker.md` | Living matrix (promote IDs here, do not treat this file as the matrix). |

### What was read on official / primary product sites (16 August 2026)

| Product | Pages fetched or searched |
|---------|---------------------------|
| **Billplz** | [main.billplz.com](https://main.billplz.com/), [main.billplz.com/pricing](https://main.billplz.com/pricing), [main.billplz.com/collection](https://main.billplz.com/collection), [support.billplz.com/api](https://support.billplz.com/api) |
| **CHIP** | [chip-in.asia](https://www.chip-in.asia/), [chip-in.asia/pricing](https://www.chip-in.asia/pricing), [chip-in.asia/collect/payments](https://www.chip-in.asia/collect/payments), [docs.chip-in.asia](https://docs.chip-in.asia/chip-collect/overview/what-we-offer), subscriptions + webhook blog |
| **ToyyibPay** | [toyyibpay.com](https://www.toyyibpay.com/), [toyyibpay.com/pricing-plans](https://www.toyyibpay.com/pricing-plans/), [toyyibpay.com/apireference](https://toyyibpay.com/apireference/) |
| **senangPay** | [senangpay.com](https://senangpay.com/), [senangpay.com/pricing](https://senangpay.com/pricing/), [api-guide.senangpay.my](https://api-guide.senangpay.my/) |
| **Fiuu** | [fiuu.com](https://fiuu.com/), payment-links, recurring, FAQ; Airwallex review of 17 Apr 2026 used only where Fiuu itself does not publish numbers |
| **iPay88 / ADAPTIS** | [ipay88.com](https://www.ipay88.com/) (now ADAPTIS e-Commerce), [my.nttdatapay.com](https://my.nttdatapay.com/), [my.nttdatapay.com/en/payment-services/adaptis-e-commerce](https://my.nttdatapay.com/en/payment-services/adaptis-e-commerce) |
| **GHL / eGHL** | NTT DATA acquisition PR (27 May 2024); ADAPTIS launch coverage 2025; merchant portal still at `secure2pay.ghl.com`; `www.eghl.my` DNS dead on fetch day |
| **Curlec / Razorpay MY** | [curlec.com/pricing](https://curlec.com/pricing/), [curlec.com/docs/api](https://curlec.com/docs/api/), Airwallex Curlec review 13 Apr 2026 (cross-checked against official table) |
| **Revenue Monster** | [revenuemonster.my](https://revenuemonster.my/), [revenuemonster.my/pricing](https://revenuemonster.my/pricing), [revenuemonster.my/online-payment](https://revenuemonster.my/online-payment), [revenuemonster.my/digital-invoice](https://revenuemonster.my/digital-invoice) |

Third-party comparison tables (DHL Feb 2026, EasyStore, SiteGiant, ecommerce-pro.my, Airwallex) were used **only** when the official site refuses to publish MDR, and are labelled as **unofficial / treat as range, not contract**.

### How to read a “typical MDR” cell

Malaysian acquiring quotes are not a single number. A honest cell has four layers:

1. **Scheme cost** (PayNet FPX interchange + acquirer cut; Visa/Mastercard interchange + scheme + acquirer).
2. **Gateway markup** (the number on the pricing page).
3. **Plan tax** (annual / setup / SST on fees — CHIP explicitly adds 8% SST on Atome).
4. **Hidden operational tax** (minimum settlement, payout fee, refund fee, failed-auth fee, Shopify 0.3% overlay, annual membership).

Where a vendor publishes a table, we quote the table and the URL. Where they do not, we say **quote-only** and give the market range other merchants report, never as if it were official.

### How to read “rail vs rival”

| Label | Meaning for Lazuar |
|-------|--------------------|
| **Rail (adapter)** | Merchant already has (or will get) a MID at that processor. Lazuar should hold the keys, create a hosted session, verify the processor webhook, and emit one `payment.*` shape. Merchant never has to embed that processor in Aura or in their own app. |
| **Rival checkout** | The processor’s own dashboard / Catalog / payment-form / invoice-link is what a non-technical Malaysian merchant will use **instead of** Lazuar portal, Commerce buy links, or Hub M2M. Competing with that UX is a product decision, not an adapter ticket. |
| **Both** | Almost every name on this list. The same company sells a hosted bill page to WhatsApp sellers **and** a REST API to platforms. |
| **Never-as-rail** | Integrating it would add liability, sales-cycle length, or company-shape we refuse, without unlocking a payment method Billplz/CHIP/Curlec cannot already present on their hosted page. |
| **Never-as-product** | Cloning their Catalog / Super App / terminal fleet / loyalty / LivePay. Out of CaaS. |

---

## Rails vs products

### The two products merchants confuse, and that we must not confuse

Malaysian “payment gateway” marketing collapses four different jobs into one logo:

1. **Acquiring / scheme access** — a BNM-listed merchant acquirer or a PayNet FPX Third-Party Acquirer that can actually move FPX / DuitNow / card money.
2. **Hosted payment form** — a bill URL / purchase URL / payment link the guest opens. This is what salon owners mean by “I sent her a Billplz.”
3. **Developer API + webhooks** — collections, bills, purchases, x_signature, RSA, HMAC. This is what Lazuar’s adapters speak.
4. **Merchant operating system** — invoices, catalogues, POS terminals, loyalty, LivePay, e-invoice buttons, split payouts, expense cards. This is what Revenue Monster, Fiuu, ADAPTIS, and Billplz Catalog sell **on top of** (1)+(2).

Lazuar Pay is deliberately **(3) plus a thin (2)**. We are a **cashier façade**:

```
Integrator app / Aura / Commerce portal
        │  POST /integrations/payments/checkouts   (or Commerce initiate)
        ▼
Lazuar Hub Payments  (vault K2, IntegrationCheckoutSession, idempotency)
        │  adapter.GenerateCheckoutAsync
        ▼
Processor hosted page  (Billplz bill / CHIP checkout_url / Razorpay short_url / Stripe Checkout)
        │  guest pays
        ▼
POST /webhooks/payments/{gatewayType}/{tenantId}
        │  verify → PaymentWebhookLog → outbox
        ▼
One envelope: payment.completed | payment.failed
        │  X-Lazuar-Signature
        ▼
Integrator fulfillment
```

We are **not** (1). We do not hold guest GMV. We are **not** (4). Portal checkout is a CaaS identity + amount form that **redirects** onto (2). Custom quote pay-by-session (`/pay/{sessionId}`) is currently **hidden / `notFound()`**.

That topology decides the competitive set:

| If the merchant says… | They are comparing us to… | Correct Lazuar move |
|-----------------------|---------------------------|---------------------|
| “I just need a link for WhatsApp” | Billplz Catalog Link, CHIP Payment Link, Toyyib bill, senangPay form, Fiuu Payment Link, Curlec Payment Link/Page, RM e-Invoice link | **Rival checkout.** Either (a) we are a better *branded* link with fulfillment + LHDN + one webhook, or (b) we lose the deal and they stay on the PG dashboard. Do not build a fourth Catalog. |
| “My developer will hit an API” | Billplz v3/v4, CHIP Collect, Toyyib createBill, senangPay Open API, Fiuu HPP, iPay88, Curlec/Razorpay, RM Open API | **Rail.** Adapter quality, signature verify, sandbox, refund, metadata. This is our actual job. |
| “I need FPX + TnG + Grab + cards on one page” | Every PG’s hosted page, not Aura checkboxes | **Honesty.** Rails live *inside* the processor page. Lazuar must not invent `ONLINE_GATEWAY // For Stripe/FPX` as if we acquired FPX. |
| “I need to auto-charge next month” | Curlec e-Mandate, CHIP token, Stripe/Razorpay token, senangPay tokenisation (Advance+), Fiuu recurring, Billplz Agreements (v5 beta) / Auto-Deduct (card) | **Rail, capability-gated.** Billplz FPX cannot do this. Our adapter already throws `NotSupportedException` for Billplz off-session. Product copy must say so. |
| “I need LHDN e-invoice when they pay” | Almost **none** of these PGs do MyInvois as a first-class developer product. RM and senangPay market “e-invoice” that is usually a **payment request PDF/link**, not UBL 2.1 to MyInvois. | **Ours, adjacent.** Lazuar LHDN module is the hook. Do not wait for Billplz to grow a tax engine. |
| “I already have iPay88 / Fiuu from 2019” | Switching cost, not features | **Optional rail** only if the merchant refuses to open Billplz/CHIP. Enterprise sales motion, not a default adapter. |

### What “hosted vs API” actually means in Malaysia

Every serious MY PG is **hosted-page-first**. Even the ones with “seamless” / “merchant hosted” / “inpage” options still expect 3-D Secure / FPX bank page redirects. There is no Malaysian equivalent of Stripe Elements that keeps the guest entirely on the merchant origin for FPX.

So the UX comparison that matters is not “iframe vs redirect.” It is:

1. **Who owns the first screen?** Lazuar portal (name, email, coupon, PWYW) vs processor bill page (amount, method picker, bank list).
2. **How many hops?** Lazuar form → Billplz bill → Maybank2u is **three** surfaces. A WhatsApp Billplz Catalog Link is **one**.
3. **What happens after pay?** Processor receipt vs Lazuar `/checkout/{slug}/success` vs integrator `success_url`. Fulfillment is webhook-only; browser `?payment=success` is UX.
4. **Can the merchant send the processor URL without us?** Yes, always. That is why these companies are rival checkouts even when we wrap them as rails.

### Corporate map (do not treat logos as independent companies)

```
BNM-listed / PayNet TPA (can actually acquire)
├── CHIP IN Sdn. Bhd.          — Registered Merchant Acquirer (Non-bank) + FPX TPA
├── Billplz Sdn. Bhd.          — PayNet System Integrator; collection + payment order
├── Razorpay Curlec            — BNM-regulated; PayNet member (ex-Curlec, acquired 2022)
├── Fiuu (Razer Fintech)       — FSA 2013; ex-MOLPay (2005) → RMS → Fiuu (2024)
├── NTT DATA Payment Services  — iPay88 (owned since 2015) + GHL Systems (majority 2024)
│                                 unified consumer brand: ADAPTIS (2025)
│                                 merchant portals still split: ipay88.com, secure2pay.ghl.com
├── senangPay-DOKU             — annual-fee SME PG; now on DOKU stack; sandbox.doku.com
├── ToyyibPay                  — FPX-first, Shariah-marketed, bill/category API
└── Revenue Monster            — BNM-licensed PG + terminals + loyalty + LivePay

Lazuar Pay                   — NOT an acquirer. BYOK cashier + LHDN + Commerce.
```

**iPay88 and GHL are no longer two independent acquiring decisions.** NTT DATA Japan agreed to acquire a majority of GHL Systems Berhad on 27 May 2024 and launched ADAPTIS as the unified suite in 2025. iPay88.com now renders ADAPTIS e-Commerce copy. eGHL’s old `www.eghl.my` did not resolve on 16 Aug 2026; the merchant portal still says “eGHL — Welcome to ADAPTIS Merchant Portal” at `secure2pay.ghl.com`. Treat them as **one enterprise family, two leftover integration contracts**.

### What Lazuar already is, in one paragraph

Hub Payments is a **stateless-ish BYOK orchestrator** with four live adapters (`STRIPE`, `BILLPLZ`, `CHIP`, `RAZORPAY`), inbound `POST /webhooks/payments/{gatewayType}/{tenantId}`, M2M `POST /integrations/payments/checkouts`, Commerce-initiated checkouts, and a portal that collects identity then **redirects** to the processor. LHDN is a **separate module** that can emit `invoice.valid` / `invoice.invalid` after MyInvois polling. Commerce can emit `payment_link.paid` for custom links. The portal custom-quote pay route is lobotomized. We do **not** implement Fiuu, SenangPay, ToyyibPay, iPay88, GHL, or Revenue Monster. We do **not** implement Curlec as a distinct gateway type — we implement Razorpay’s India-shaped Payment Link SDK, which happens to share `https://api.razorpay.com/v1` with Curlec docs.

---

## Dossier per gateway

Each dossier uses the same spine: identity → pricing → hosted vs API → recurring → webhooks → settlement/KYC → invoices/links → LHDN → DX → UX vs Lazuar portal → rail vs rival.

---

### 1. Billplz

**Legal / brand.** Billplz Sdn. Bhd. Public marketing: [main.billplz.com](https://main.billplz.com/). Dashboard: `dashboard.billplz.com`. API: `https://www.billplz.com/api/` and sandbox `https://www.billplz-sandbox.com/api/`. Docs: [support.billplz.com/api](https://support.billplz.com/api). Claims (homepage, fetched 16 Aug 2026): 60,000+ Malaysian organizations; 19.5M+ transactions in 2025; MYR 5.4B+ processed in 2025; 99.9% historical uptime. Logo wall is institutional (PDRM MyBayar Saman, PTPTN, Lembaga Zakat Selangor, Perodua, Boost, Wahed). PCI DSS + ISO 27018 + PayNet System Integrator badges.

**What it is in the Malaysian mental model.** The default “just send a bill” product. Schools, associations, zakat, traffic summons, tuition, and WhatsApp sellers. When an informal salon says they “take deposit on Billplz,” they mean a Collection + a Bill URL in WhatsApp, not an API.

**Product surface (official).**

| Surface | What it is |
|---------|------------|
| **Collection** | Folder of bills. Payment methods configured per collection. Our adapter’s `merchantId` **is** `collection_id`. |
| **Bill** | One payment request. Has `url`, `callback_url`, `redirect_url`, `reference_1/2`, amount in sen. |
| **Bill Page** | Hosted, mobile-optimized, method picker, automatic receipts. This is the guest UX. |
| **Catalog Link** | Branded form the *payer* fills (amount can be open). Rival of Lazuar portal for no-website sellers. `catalog.billplz.com/link`. |
| **Catalog Store** | Mini commerce. `catalog.billplz.com/commerce`. |
| **Catalog Billing** | Recurring-ish billing product on Catalog, still charged at Billplz processing rates. |
| **Payment Order (v5)** | Disburse to any MY bank account via DuitNow Transfer. API-only. Real-time. |
| **Split payments** | Collection-level split to other verified Billplz accounts. |
| **Direct payment gateway** | Skip Bill Page: set `reference_1_label=Bank Code`, `reference_1=<bank code>`, append `?auto_submit=true`. |
| **Agreements (v5 beta)** | Card tokenisation + on-demand auto-deduct. `POST /api/v5/agreements`, `authorize_consent_url`. Statuses: draft / pending_authorisation / authorised / failed / revoked. Type `billplz_card`. |
| **Plugins** | Shopify (+0.3% overlay on both Basic and Standard), Woo, etc. GitHub `billplz`. |

**Pricing (official, [main.billplz.com/pricing](https://main.billplz.com/pricing), 16 Aug 2026).**

| | **Basic (MYR 0 / year)** | **Standard (MYR 999 / year)** | **Enterprise** |
|--|--------------------------|-------------------------------|----------------|
| FPX B2C | **RM 1.25** flat | **RM 0.75** flat | Custom |
| FPX B2B | RM 3.00 | RM 2.00 | Custom |
| FPX payout | Next business day | Next business day | Real-time option |
| Card MYR | **1.8%** | **1.5%** | Custom / FPX CCA |
| Card non-MYR | 3.8% optional | 3.5% optional | Custom |
| Card payout | T+2 | T+2 | Custom |
| **Auto-Deduct MYR** | **2.3% + RM 1.25** | **2.0% + RM 0.75** | Custom |
| Auto-Deduct non-MYR | 4.2% + 1.25 optional | 4.0% + 0.75 optional | Custom |
| Wallets (DuitNow QR, TnG, Boost, GrabPay) | **1.5%** | 1.5% | Custom |
| Wallet payout | Next day | Next day | Custom |
| Atome | **6%**, 3-month, payout Wed & Fri | 6% | Custom |
| Payment Order (DuitNow Transfer) | RM 1.25, real-time | RM 0.75, real-time | Custom |
| Catalog Link / Store / Billing | Free subscription; processing = Billplz rates | Same | Custom |
| Shopify | Billplz + **0.3%** | Billplz + 0.3% | Custom |

No setup fee on Basic. No contract. Enterprise adds dedicated AM, H2H reconciliation, branded bank statement name, Receivables/Payables/Reconciliation BPO, FPX Credit Card Account.

**Implication of the fee shape.** Flat FPX is why Billplz owns low-ticket Malaysia. A RM 50 salon deposit costs **RM 1.25** (2.5%) on Basic or **RM 0.75** (1.5%) on Standard — cheaper than Curlec Basic’s `max(1.5%, RM 1)` (RM 1.00 on RM 50) and far cheaper than Stripe MY `3% + RM 1`. Card 1.8% is **better** than CHIP local credit (2.0%) and Curlec Basic (2.40%). Auto-Deduct is a different, more expensive product than a one-shot bill.

**Hosted vs API.**

- Default guest path is **hosted Bill Page**. Our adapter creates `POST /api/v3/bills` and returns `url`.
- Official docs (16 Aug 2026): **“V3 is no longer in active development. No new features will be introduced in this version. For new integrations, use V4.”** Lazuar is on **v3**. Existing integrations are promised indefinitely, but we are already on the frozen line.
- API accepts `application/json` and `application/x-www-form-urlencoded`. Auth is HTTP Basic, secret key as username, empty password. Our adapter does exactly that.
- Sandbox is a **separate account and separate host**. Our `BillplzPublicBase` maps production vs sandbox by `App:BillplzEnvironment` or by Hub host ∈ {`api`,`pay`,`hub`}.`lazuar.com`. Quickstart still documents an older “if ApiBaseUrl contains lazuar.com” story; the code is now host-exact. Staging on a `*.lazuar.com` host that is **not** in that set will hit **sandbox** unless the env var is set. A `sk_live_` K1 does **not** flip Billplz live.
- Rate limit on GET: 100 / 5 min (or 10 if abused), headers `RateLimit-*`.
- Metadata: only `reference_1` / `reference_2` (labels 20 chars). **No JSON metadata.** ADR 009 exists because of this. Callbacks historically dropped references; we stamp `?type=&reference_1=&checkout_id=` on `callback_url` and also write refs onto the bill.
- `callback_url` is locked at bill creation. Changing Hub public base does **not** rewrite old bills (`CALLBACK_BASE_NOT_PUBLIC` if you try to stamp localhost).
- Redirect (`redirect_url`) and callback are **not ordered**. Must be idempotent on bill id. Our `PaymentWebhookLog` is `(Provider, EventId)` with EventId = bill id — so a paid callback and a later unpaid-looking retry need care. We map `paid=true` or `state=paid` → `PAYMENT_COMPLETED`, else `PAYMENT_FAILED`.
- **No refund API** in the adapter (`IssueRefundAsync` → `false`). Official posture for SMEs is dashboard refund / Payment Order. Do not promise in-product money refunds on Billplz.
- Delete bill only if still `due`. Useful for unpaid-deposit timers; we do not call delete today.

**Recurring / tokenisation.**

- **FPX is not a vault.** You cannot auto-charge a Maybank2u payment next month. This is the single most important product sentence we can print for salon no-shows.
- **Auto-Deduct** on the pricing page is a **card** product (2.3%+1.25 / 2%+0.75), T+2.
- **Agreements API (v5 beta)** is the programmatic form: create agreement → customer hits `authorize_consent_url` → tokenised card → merchant auto-deducts bills. Our adapter does **not** call v5. `ChargeOffSessionAsync` throws `NotSupportedException("Billplz does not support vaulted token off-session charges.")`. That statement is still true **of the adapter and of FPX**. It is **no longer true of Billplz-the-company** if the merchant is on Agreements / Auto-Deduct.
- Catalog Billing is a no-code recurring layer, still not off-session FPX.

**Webhooks quality.**

| Property | Billplz |
|----------|---------|
| Transport | `application/x-www-form-urlencoded` POST to per-bill `callback_url` |
| Signature | `x_signature` HMAC-SHA256 over sorted `key+value` joined by `\|`, hex lower. Extra fields `paid_at`, `transaction_id`, `transaction_status` may or may not be in the signed set — our adapter tries **with** extras, then **without**. |
| Event taxonomy | Not an event bus. One callback per bill completion (paid or not). No `refund.created`, no `dispute.created`. |
| Metadata in body | Unreliable. Reconstruct from query + `reference_*`. |
| Fees in payload | **None.** Our fee columns were removed; webhook handler passes 0,0,0. Net = gross. |
| Idempotency key | Bill id. Retries are the same bill. |
| Delivery | Rank API `GET /v4/webhook_rank` (0.0 highest). No Standard Webhooks envelope. No event id header. |
| Localhost | Refused. Our `BillplzPublicBase` matches. |

Quality verdict: **good enough for “did this bill pay?”** if you implement x_signature exactly and treat redirect as UX. **Poor** for subscriptions, refunds, disputes, fee truth, and arbitrary metadata. This is why Hub exists.

**Settlement, payout, KYC.**

- FPX: next business day (Enterprise: real-time). Cards: T+2. Wallets: next day. Atome: Wed & Fri.
- Payment Order disbursement is real-time DuitNow, RM 0.75–1.25.
- KYC is dashboard onboarding (SSM, bank, directors). Institutional logos imply a real compliance desk. Airwallex’s secondary table claimed “~14 days” onboarding versus Curlec’s 1–2 days — **unofficial**. Treat Billplz KYC as “days to a couple of weeks,” not instant Stripe.
- Sandbox account is separate; do not reuse live secret on sandbox host.

**Invoicing / payment links.**

- A Bill **is** a payment link (`https://www.billplz.com/bills/{id}`).
- Catalog Link is the no-code form (payer enters details). This is the rival of Lazuar `/{tenant}/checkout/{product}`.
- Bulk Excel billing is a school/association feature we should not clone.
- Email/SMS deliver flag exists on create-bill (`deliver`); SMS is plan-charged. We do not set it; we rely on integrator/Aura messaging.

**e-Invoice / LHDN.**

- **No official MyInvois / LHDN API on the pricing, collection, or API index fetched 16 Aug 2026.** Receipts are Billplz receipts, not UBL 2.1.
- If a merchant needs e-invoice, they use MyInvois portal, an accountant tool, or **Lazuar LHDN**. Billplz is not a tax engine.
- Do not stamp a Billplz bill id as `internal_id` and pretend it is validated.

**Developer DX.**

- Docs are now a proper support site (Introduction, sandbox, v3/v4/v5, checksum for v5, bank codes, errors). Better than the old single-page API.
- v3 frozen is a **migration debt** for us.
- PHP/curl examples; Basic auth is trivial. No official first-party .NET SDK of note — we roll our own.
- Plugins are how non-developers integrate. Platforms (us) should not tell merchants to install a Woo plugin.
- Signature field-order is the classic foot-gun. Our dual-compute is the right defensive posture.
- DX score versus CHIP/Curlec: **B for bills, D for subscriptions/refunds/metadata.**

**Merchant-facing checkout UX vs Lazuar portal.**

| Step | Billplz Catalog / Bill Page | Lazuar portal (`/{tenant}/checkout/{slug}`) |
|------|-----------------------------|-----------------------------------------------|
| Discover | WhatsApp / IG bio / Excel blast | Product URL on creator site or Hub |
| Identity | Bill pre-filled by API, or Catalog form | Name, email, optional phone/address. Guest mode. Coupon. PWYW. Quantity. Tax-id UI **MVP-HIDE**. |
| Amount | Fixed on Bill; open on some Catalog | Product price × qty − coupon; PWYW |
| Methods | FPX bank list, wallets, cards, Atome — **on Billplz** | **None.** Submit → `window.location.href = result.url` |
| Brand | Billplz chrome + collection logo | Lazuar CaaS chrome (`CheckoutLayout`, `OrderSummaryCard`) then **abandons** to Billplz |
| Success | Billplz receipt, or `redirect_url` | `/checkout/{slug}/success` — must not treat as paid |
| Recurring manage | Catalog Billing / dashboard | Stripe portal only; Billplz throws |
| Send without a website | **Native, 30 seconds** | Requires a product in Commerce + active gateway |

Billplz **wins informal send-a-link**. Lazuar **wins** (when it works) identity capture, coupon/PWYW, one webhook for the integrator, and the *promise* of LHDN + dunning. Lazuar **loses** if the merchant never wanted a product catalog and only wanted “RM 50 deposit please.”

**Adapter in our repo (preview; full matrix later).** Live. Default Commerce gateway in several paths historically (some hardcodes were fixed). Off-session unsupported. Refunds always false. Fees always 0. v3 only. Metadata via query. Production host allow-list.

**Rail vs rival.**

- **Primary rail. Keep. Deepen.** This is the Malaysian default for guest money. Aura’s entire System B story is “Hub + Billplz.”
- **Rival checkout** for informal sellers (Catalog Link) and for institutions (bulk bills). Do **not** build Catalog Store.
- **Never promise** Billplz card-on-file in salon copy until we implement Agreements **and** the merchant’s collection has Auto-Deduct enabled.
- **Never** use Billplz for System A (Aura Pro). Paddle stays.

---

### 2. CHIP / Chip Collect

**Legal / brand.** CHIP IN Sdn. Bhd. 202201010914 (1456611-H). Registered address Lot 1.02, Glo Damansara. **Registered Merchant Acquirer (Non-bank)** listed by BNM; **PayNet FPX Third-Party Acquirer**. PCI-DSS, AWS, Visa approved service provider badges on collect page. Site: [chip-in.asia](https://www.chip-in.asia/). Portal: `portal.chip-in.asia`. Onboarding: `onboarding.chip-in.asia`. API: `https://gate.chip-in.asia/api/v1/`. Docs: [docs.chip-in.asia](https://docs.chip-in.asia/). Status: `status.chip-in.asia`, `fpxstatus.chip-in.asia`. GitHub: `CHIPAsia`.

**Product family (do not collapse).**

| Product | Job |
|---------|-----|
| **CHIP Collect** | Accept: purchases API, payment links, plugins, CHIP mini (phone QR), POS terminal rates |
| **CHIP Send** | Payouts: RM 1.00 / transfer + RM 1.00 one-time bank verification, real-time |
| **CHIP Expense** | Team spend / scan-to-pay; transaction fee waived |
| **CHIP Advance** | Shariah sales-based financing; from 6% + 0.5% stamp; up to RM 500k; 48h |

Lazuar only speaks **Collect**. Send/Expense/Advance are rival OS, not rails.

**What it is in the Malaysian mental model.** The “modern Billplz” for developers and SMEs who want cards + FPX + DuitNow QR + wallets + BNPL + (now) stablecoins, **no annual fee**, next-day FPX, and a real JSON API with metadata. Also the only local PG aggressively shipping **phone-as-terminal** (CHIP mini) and **cross-border DuitNow QR** (SG / TH / ID → settle MYR).

**Pricing (official, [chip-in.asia/pricing](https://www.chip-in.asia/pricing), 16 Aug 2026).** No setup, no monthly, no annual, no contract.

| Method | Rate | Settlement |
|--------|------|------------|
| FPX B2C | **RM 1.00** / paid txn | Next day |
| FPX B2B | **RM 2.00** / paid txn | Next day |
| Local credit card | **2.0%** | 2 business days |
| Local debit card | **1.0%** | 2 business days |
| Foreign cards | **3.0%** | 2 business days |
| DuitNow QR (Online) incl. cross-border SG/TH/ID | **1.0%** (min RM 0.15) | Next day |
| E-wallets (TnG, GrabPay, ShopeePay) online | **1.4%** | 2 business days |
| Atome | **5.3%** + **8% SST on the fee** | Thursday of following week |
| SPayLater | **1.4%** | 2 business days |
| Stablecoins (BTC, ETH, PYUSD, USDC, USDT; several chains) | **1.5%**; refund 1.5%; excl. gas | 1 business day |
| CHIP mini DuitNow QR | same 1.0% min 0.15 | Next day |
| POS terminal local credit / debit / foreign | 1.35% / 1.00% / 4.40% | 2 business days |
| POS e-wallets | TnG 0.95%, GrabPay 1.05%, Boost 1.00%, ShopeePay 1.15%, Maybank QR 0.95%, Alipay 1.00%, UnionPay QR 1.80% | 2 business days |
| CHIP Send | RM 1.00 / transfer; RM 1.00 verify | Real-time |
| CHIP Advance | from 6% + 0.5% stamp | n/a |
| Refund extra | **FPX only**: RM 1.00 B2C / RM 2.00 B2B. Other methods no extra refund fee | |

Google Pay live; Apple Pay “coming soon” on pricing page. Cards on CHIP mini “coming soon.”

**Versus Billplz on a RM 50 deposit.** CHIP FPX = RM 1.00. Billplz Basic = RM 1.25. Billplz Standard = RM 0.75. CHIP wins on Basic-plan merchants; Billplz Standard wins on volume if they pay RM 999/year. CHIP local credit 2.0% **loses** to Billplz 1.8%/1.5%. CHIP debit 1.0% is a real differentiator if the guest uses debit.

**Hosted vs API.**

- Create `POST /api/v1/purchases/` with Bearer token + `brand_id`. Response has `checkout_url` + `id`.
- Our adapter sends `client.email/full_name`, `purchase.products[]`, `purchase.metadata`, `success_redirect` / `failure_redirect` / `cancel_redirect`. Amount via `ToMinorUnitsRounded` (banker’s rounding) — **different from Billplz/Razorpay truncating**.
- `force_recurring` + `skip_capture` when `setupFutureUsage`. Zero-amount + skip_capture = card vault without capture (docs: “Secure Customer Card Information”).
- Official capabilities: success callback, receipts, redirects, `skip_capture` pre-auth, free-form `reference`, `payment_method_whitelist` (FPX, local/foreign cards, e-wallets, DuitNow QR, BNPL, on-prem POS).
- Test mode is first-class (portal toggle). Test cards `4444 3333 2222 1111` (non-3DS), `5555 5555 5555 4444` (3DS), CVC 123.
- Plugins: Woo (incl. Subscriptions, One Page Checkout), Gravity, GiveWP, Fluent, Bookly, Charitable, GoHighLevel, WHMCS, PrestaShop, OpenCart, EDD, Perfex, TourMaster, etc. Sales platforms: EasyStore, OnPay, Orderla, ShoppeGo, Convertly, WhatsMenu, Wasep.me.
- **S2S / whitelist** exists; we do not use it. We always take `checkout_url`.

**Recurring / tokenisation.**

- CHIP **does not run the subscription clock**. Docs FAQ: “Does CHIP handle the automatic renewal of subscriptions? **No.** What CHIP offers is the ability to save and charge a customer's saved card. The automatic renewal logic must be implemented on the merchant's side.”
- That is **exactly** Lazuar Commerce `BillingEngineJob` / `DunningEngineJob` → `ExecuteOffSessionChargeIntegrationEvent` → `ChipCollectGatewayAdapter.ChargeOffSessionAsync`.
- Off-session path in our adapter: GET old purchase (token id) → copy brand + client → POST new purchase with commerce metadata → `POST /purchases/{newId}/charge/` with `{ recurring_token }`. Success if `status` is `paid` or `pending_charge`.
- Token is tied to `brand_id`; referenced by customer email; list/delete token APIs exist. Webhook sets `GatewayTokenId` when `is_recurring_token` is true — **token id = purchase id**.
- Pre-auth: `skip_capture` + price > 0. Our webhook **explicitly refuses** to treat `purchase.preauthorized` as paid (comment in adapter). Correct.
- Card-only for vault. Do not advertise CHIP recurring on FPX.

**Webhooks quality.**

| Property | CHIP |
|----------|------|
| Transport | JSON POST |
| Signature | `X-Signature` base64 RSA PKCS#1 v1.5 SHA256 of **raw body**. Public key from `GET /public_key/`. Our config save **auto-fetches PEM into `WebhookSecret`**. |
| Events we register | `purchase.paid`, `purchase.payment_failure`, `payment.refunded`, `purchase.preauthorized` |
| Events we map | `purchase.paid` → `PAYMENT_COMPLETED`; `purchase.payment_failure` → `PAYMENT_FAILED`; **everything else verified-and-ignored** including `payment.refunded` and `purchase.preauthorized` |
| Metadata | `purchase.metadata` JSON — **first-class**. No query-string hack. |
| Fees | `payment.fee_amount` / `net_amount` in sen. We convert /100. **Best fee fidelity after Stripe.** |
| Event id | `id` on envelope; fallback `Guid.NewGuid()` if missing — **weaker than Razorpay’s fail-closed**. A missing id would mint a new idempotency key and could double-publish. |
| Customer id | Always null in our parse. |
| Auto-provision | Yes, on CHIP key save. Events include refunded/preauth even though we drop them. Localhost rewritten to `lazuar-local-dev.com` (which Billplz public-base would **refuse** — inconsistency). |

Quality verdict: **best local webhook** we have (JSON, RSA, metadata, fees). Gaps: refund event registered but not mapped; EventId fallback is unsafe; no dispute event; `GatewayCustomerId` unused so dunning depends on token purchase id.

**Settlement, payout, KYC.**

- Official FAQ: SSM-registered business + business bank account in the registered name. Docs: SSM, IC, bank statement; extra docs per method (FPX, cards, wallets, BNPL). NGOs/mosques/tahfiz accepted with ROS/JAIS/Yayasan papers.
- Multi-brand: one CHIP account, many `brand_id`s (one per website). Our `MerchantId` is Brand ID.
- Test mode before live.
- Send is a separate payout product (not Collect settlement). Collect settlement is the tables above.

**Invoicing / payment links.**

- Payment Links are a first-class no-code product (`/collect/payment-links`). Share anywhere. Rival of Billplz Catalog and Lazuar portal.
- API payment link = a Purchase with `checkout_url`. We already emit that.
- Receipts can be sent by CHIP (`send_receipt`). We do not set it; Commerce/Communications own receipts.

**e-Invoice / LHDN.**

- **No MyInvois product** on Collect/pricing/docs index fetched 16 Aug 2026.
- CHIP receipts ≠ LHDN. Same split as Billplz.

**Developer DX.**

- Best-in-class among MY PGs: OpenAPI-ish docs, llms.txt, GitHub org, official plugins, test mode, webhook CRUD, public key endpoint, whitelist, pre-auth, charge token, refund, list tokens.
- “Integrate with AI agent” / vibe-coding guide exists — marketing, but the API is actually agent-friendly.
- DX score: **A−**. Only miss versus Stripe: no Billing Portal, no Connect, no dispute API, subscription clock is DIY.

**Merchant-facing checkout UX vs Lazuar portal.**

CHIP hosted checkout is a modern single page: amount, method tiles (FPX with bank list, cards with Google Pay, DuitNow QR, wallets, BNPL). Screenshots on `/collect/payments` show RM 120 Baju Melayu card form and RM 188 workshop FPX/Maybank2u. It looks closer to Stripe Checkout than to a 2016 Billplz bill.

Lazuar portal still does **not** show those methods. We collect email and bounce to CHIP. Versus a CHIP Payment Link, we add coupon/PWYW/identity and lose one hop of convenience.

CHIP mini (in-person QR) is a **rival of in-person desk collect**, not of Hub. Do not put CHIP mini inside Pay. A merchant that wants phone-QR at the counter can use CHIP mini **alongside** Hub for online deposits.

**Adapter in our repo.** Live, richest local adapter (refund + off-session + fees + metadata + auto webhook). Gaps: EventId Guid fallback; refund webhook ignored; preauth ignored (correct for money, but no “hold” domain event); portal throws; localhost webhook rewrite is a foot-gun.

**Rail vs rival.**

- **Second primary rail. Keep. Production-harden.** This is the right K2 for merchants who need cards + vault + refunds + DuitNow QR + no annual fee.
- **Rival checkout** via Payment Links and CHIP mini. Do not clone mini or Advance or Expense.
- **Do not** treat CHIP as a no-show auto-charge on FPX. Vault is card.

---

### 3. ToyyibPay

**Legal / brand.** toyyibPay. Site: [toyyibpay.com](https://www.toyyibpay.com/). API: form POST to `https://toyyibpay.com/index.php/api/...`. Sandbox: `dev.toyyibpay.com` (separate registration). Positions as Shariah-compliant by design; Santai plan is NPO-only free FPX B2C. Woo plugin official.

**What it is in the Malaysian mental model.** The cheap FPX bill tool for SMEs, mosques, schools, and “I don’t want to pay Billplz RM 1.25.” Category + Bill metaphor is a clone of Billplz’s Collection + Bill, with uglier docs and a loyal Malay-speaking SME base.

**Pricing (official, [toyyibpay.com/pricing-plans](https://www.toyyibpay.com/pricing-plans/), 16 Aug 2026).**

| Plan | FPX B2C | FPX B2B | Settlement | Who |
|------|---------|---------|------------|-----|
| **Santai** | **RM 0.00** | RM 2.00 | Next **10** business days | Non-profits only |
| **Standard** | **RM 1.00** | RM 2.00 | Next **1–4** business days | Everyone |

**Cards (via partners, not the core plan):**

- Local card **1.50%**
- Foreign card **3.5%**
- Onboarding **RM 100**
- Yearly **RM 100** starting subsequent year
- MYR only
- Settlement next **4** business days (best effort)

**DuitNow QR:** **1.00% or RM 1.00** per txn; all users subject to provider approval; next **2** business days.

No annual fee on Standard FPX. Secondary write-ups (accounting.my) quote the same FPX numbers.

**Hosted vs API.**

- Mental model: **Category** (group) then **Bill** (invoice). Bill URL is `https://toyyibpay.com/{BillCode}`.
- `createCategory` → `createBill` → redirect. Classic 2016 PHP `multipart/form-data` / `x-www-form-urlencoded`. Secret key in body, not Bearer.
- Bill flags: fixed vs dynamic amount (`billPriceSetting` 0/1), require payer info, return URL, callback URL, `billExternalReferenceNo`, split payment (FPX only, JSON of user ids), payment channel 0=FPX / 1=card / 2=both, charge-to-customer, expiry date or expiry days (1–100), FPX B2B enable, DuitNow QR enable.
- Amount in **sen**.
- Sandbox: replace host with `dev.toyyibpay.com`.
- **No official JSON metadata object.** External reference only — same class of problem as Billplz, slightly better because `order_id` comes back on callback.

**Recurring / tokenisation.**

- **None as a first-class API.** You create another bill. No token, no e-mandate, no off-session.
- Not a subscription rail.

**Webhooks quality.**

Callback POST fields: `refno`, `status` (1 success, 2 pending, 3 fail), `reason`, `billcode`, `order_id`, `amount`, `transaction_time`, `hash`.

Hash (docs, 16 Aug 2026):

```
MD5( userSecretKey + status + order_id + refno + "ok" )
```

DuitNow QR callbacks omit `transaction_time` / `fpx_transaction_id`; extra `dnqr_transaction_id`; identify via `billpaymentChannel = "DuitNow QR"` on `getBillTransactions`.

Return URL is GET: `status_id`, `billcode`, `order_id`.

Also: `getBillTransactions`, `inactiveBill`, `checkDuitNowQRStatus`. Enterprise partner APIs: `createAccount`, `getUserStatus`, `getSettlementSummary` — Toyyib as a **platform for platforms** (white-label users). That is a **rival of Hub workspace provision**, not a rail we need.

Quality verdict: **legacy but parseable**. MD5 + magic `"ok"` is 2014-grade. No RSA, no event types, no fees, no refund event. Better than unsigned. Worse than Billplz HMAC and CHIP RSA. Localhost callback **explicitly does not work**.

**Settlement, payout, KYC.**

- Standard FPX 1–4 days is **slower and less precise** than Billplz/CHIP next-day.
- Santai 10 days is a donation-desk product.
- Cards 4 days + RM 100/year is a bolt-on.
- KYC is self-serve registration; card channel extra approval.

**Invoicing / payment links.**

- A bill code **is** the payment link. Dynamic bills let the payer type the amount (zakat / donation).
- Dashboard bill create is the no-code path.
- Split payment to other Toyyib users (FPX only).

**e-Invoice / LHDN.**

- **None found** on pricing or API reference.

**Developer DX.**

- One long PHP page. Sample secret keys in the docs (treat as compromised examples). Inconsistent parameter names (`userSecretKey` vs `secretKey`). Enterprise APIs feel bolted on.
- DX score: **D+**. Integratable in an afternoon; miserable to maintain. We would wrap it only if a vertical (NPO, tahfiz, sekolah) arrives with Toyyib already in their bank letter.

**Merchant-facing checkout UX vs Lazuar portal.**

Toyyib hosted bill is a simple, Malay-first page: amount, FPX (and optionally card/QR), payor fields if required. Faster to send from the dashboard than to create a Lazuar Commerce product. Looks cheaper than Billplz; feels less institutional. A mosque treasurer will pick Toyyib Santai over Lazuar every time — **correctly**. We should not sell CaaS to Santai NPOs.

**Rail vs rival.**

- **Rival checkout** for NPOs and micro-SMEs. **Optional rail, low priority.**
- Integrate **only** if (a) a real integrator cohort is stuck on Toyyib, or (b) we want a zero-MDR NPO story — which we do not, because we are not a zakat product.
- **Never** as default K2. Settlement slowness + MD5 + no vault + no refund API = we would be wrapping a worse Billplz.
- White-label `createAccount` is a **Never** (we would become a Toyyib reseller / payfac-shaped thing).

---

### 4. senangPay (senangPay-DOKU)

**Legal / brand.** senangPay, now marketed as **senangPay-DOKU**. Site: [senangpay.com](https://senangpay.com/). Pricing: [senangpay.com/pricing](https://senangpay.com/pricing/). API guide: [api-guide.senangpay.my](https://api-guide.senangpay.my/). Sandbox registration: `sandbox.doku.com/bo/sandbox-registration?country=MY`. Dashboard still `app.senangpay.my`. Strong SME + social-seller brand; annual membership culture (opposite of CHIP/Billplz Basic).

**What it is in the Malaysian mental model.** “The payment form company.” Instagram sellers, EasyStore/SiteGiant merchants, tabung / association forms. You pay ~RM 199–349 a year and get hosted forms, quotations, shopping-cart plugins, and (on Advance) tokenisation / recurring / payout API.

**Pricing (official, 16 Aug 2026; Raya promo RM50 off with `RAMADAN26` was still on the page).**

| Package | List | Promo on page | What you get |
|---------|------|---------------|--------------|
| **Starter** | **RM 199 / year** | RM 149 | Instant approval* for FPX + e-wallet; basic features; 24/7 support |
| **Advance** | **RM 349 / year** | RM 299 | All methods; 0% IPP extra; advanced features (tokenisation, recurring, mass/split, faster settlement, foreign cards) |
| **Enterprise** | Custom | — | Lowest MDR, dedicated AM, flexible settlement, custom API |

\*Instant approval = FPX and e-wallet only. Cards are slower / package-gated.

**Transaction rates (official “Fair Rates” section):**

| Method | Rate |
|--------|------|
| FPX | **RM 1 or 1.5%, whichever is higher** |
| Local cards | **RM 0.65 or 2.5%*, whichever is higher** (*Enterprise can customise; JCB not yet on new DOKU platform) |
| Foreign cards | “Based on packages” |
| E-wallets | **RM 0.65 or 1.5%, whichever is higher** (Boost & Shopback not yet on new DOKU platform) |
| SPayLater (1 / 3 / 6 / 12 / 18 / 24) | **2.0%** |
| Grab PayLater 4x / postpaid | **6.0%** (8x/12x extra processing) |
| Atome 3x | **5.5%** |
| Bank IPP | Extra; one-time activation fee; up to 24 months |

Worked FPX examples: RM 50 deposit → **RM 1.00** (floor). RM 200 service → **RM 3.00** (1.5%). So senangPay FPX is **percentage-expensive above ~RM 67**, unlike Billplz/CHIP/Toyyib flats.

Local card: RM 50 → RM 1.25 (2.5%); small tickets hit the RM 0.65 floor.

Subscription is **non-refundable**. Bank-rejected applications refunded in full.

**Hosted vs API.**

Three integration generations live at once (this is the DX tax):

1. **Open API / payment form** — classic senangPay: POST hash (HMAC-SHA256 over `secret + detail + amount + order_id` in newer docs; older merchant-hosted still shows **MD5** over a long concatenated string including PAN — **do not implement merchant-hosted**). Return URL GET + callback that must print `OK`.
2. **Query APIs** — `apiv1/query_order_status`, `get_transaction_list` with **MD5** (`merchant_id + secret + order_id`).
3. **Tokenisation / MOTO / 3D Get Token** — `app.senangpay.my/apiv1/pay_cc`, `/tokenization/`, `/get_payment_token`. 2D get-token deprecated 31 Dec 2019. 3D Get Token is **not REST** (full webview). RM 1 × 2 authorisations reversed for card validation.
4. **DOKU-hosted / sandbox.doku.com** — the “new platform.” JCB / Boost / Shopback called out as not migrated yet.

We would implement **hosted payment request + HMAC callback only**. We would **never** implement merchant-hosted PAN post or the old MD5-with-card-number string.

**Recurring / tokenisation.**

- Officially on **Advance and Enterprise**: “Recurring payment” + “Tokenisation.”
- 3D Get Token → store token → `pay_cc` with token. Callback + return URL configured under Dashboard → Settings → Profile → Shopping Cart Integration Link (global, not per-request — **worse than Billplz per-bill callback**).
- Advance Recurring Callback is a separate dashboard toggle (guide.senangpay.com).
- Recurring clock may live in senangPay (unlike CHIP). Confirm per merchant; do not assume we can turn it off and own dunning.

**Webhooks quality.**

| Property | senangPay |
|----------|-----------|
| Transport | GET/POST return + callback; callback expects body `OK` |
| Signature | Mix of HMAC-SHA256 (newer) and MD5 (query + some hosted). Field order is sacred. |
| Event taxonomy | Status 1/0. Recurring has an “advance callback.” Not an event catalog. |
| Metadata | `order_id` + `detail`. No JSON bag. |
| Fees | Not in callback. |
| Config | **Account-level** return/callback URLs. Multi-tenant Hub would need one senangPay MID per workspace **or** a single callback that switches on `order_id`. Per-tenant URL stamp like Billplz is **not** the native model. |
| Refund hash | Docs in community ports: `HMAC-SHA256(secret + transaction_id + refund_amount)` — unofficial completeness. |

Quality verdict: **legacy SME**. Workable with a careful adapter and a single callback router. Hostile to Hub’s per-tenant callback stamp. DOKU migration means we must pick **one** generation and refuse the rest.

**Settlement, payout, KYC.**

- Instant approval for FPX/e-wallet on Starter/Advance. Cards later.
- “Faster settlement” is an Advance+ feature, not a published T+N table.
- Payout API on Advance+.
- Annual fee is the real KYC gate — they want a subscription relationship.
- SSM / bank docs standard.

**Invoicing / payment links.**

- **This is the product.** Digital Catalog, quotation, “e-invoice and quotation,” payment forms without a website. Starter is explicitly sold to “social media sellers without a web store.”
- Rival of Billplz Catalog **and** of Lazuar portal for the same informal ICP.

**e-Invoice / LHDN.**

- Marketing: “E-invoice and quotation” / “customisable invoice.”
- Developer Facebook group has people asking about **LHDN document hash** in preprod — so some merchants are trying to bolt MyInvois onto senangPay flows — but **senangPay is not an official MyInvois middleware** on the pricing page. Treat “e-invoice” here as **their PDF/quotation**, unless a specific Enterprise SKU is shown to submit UBL.
- Lazuar LHDN remains the compliance hook.

**Developer DX.**

- Fragmented guides (api-guide.senangpay.my, guide.senangpay.com, DOKU docs). Hash algorithms disagree across pages. MOTO example still posts raw PAN to `pay_cc`.
- DX score: **D**. Integratable, but every year of DOKU migration makes a new adapter a liability. ADR 020 listed SenangPay as a Phase-1 target when the company was still “local SME PG.” In 2026 it is “local SME PG being rewritten onto an Indonesian stack.” Wait until one API generation is stable **or** skip.

**Merchant-facing checkout UX vs Lazuar portal.**

senangPay payment form is the thing Instagram sellers already know: logo, amount, FPX/wallets/cards, pay. Quotations with embedded pay. That **is** their CaaS. Lazuar portal is more “creator checkout” (coupon, PWYW, identity) and less “send quotation.” If we try to out-form senangPay, we lose ADR 023 (do not rebuild the CMS / form builder).

**Rail vs rival.**

- **Primarily a rival checkout** for informal MY sellers.
- **Rail: Later / pain-gated only.** Higher FPX MDR above RM 67, annual fee, account-level callbacks, DOKU flux.
- **Never** implement merchant-hosted PAN.
- **Never** become “Lazuar payment forms” to beat their Catalog.

---

### 5. Fiuu (Razer Merchant Services / MOLPay)

**Legal / brand.** Fiuu is the 2024 rebrand of **Razer Merchant Services**, itself the 2018 rebrand of **MOLPay** (founded 2005). Still in the Razer group. Site: [fiuu.com](https://fiuu.com/). Self-serve: [booster.fiuu.com](https://booster.fiuu.com/). Claims on homepage (fetched 16 Aug 2026): **US$13B TPV FY2025**; 70,000+ merchants (secondary); 960M+ transactions (secondary, Fiuu newsroom via Airwallex). Markets: MY, SG, TH, ID, PH, VN, TW, HK. PCI DSS SP Level 1, ISO 27001:2022, FSA 2013. Direct acquiring licences cited for Visa, Mastercard, UnionPay, Discover.

**What it is in the Malaysian mental model.** The **enterprise / marketplace / 7-Eleven cash** gateway. If you are TikTok Shop, airasia, Grab-scale, or you need “pay at 7-Eleven for an online cart,” you are on Fiuu (or iPay88). SMEs meet it through EasyStore “free first year (worth RM 899)” bundles.

**Product surface (official homepage + feature pages).**

Online: Hosted Payment Page, Seamless Integration (stay on merchant site), Inpage checkout, Virtual Terminal (now **Tap to Pay on iPhone** in MY & SG), Mobile XDK, Tokenization, Marketplace Payment (sub-merchant split), Recurring Payment, Gateway Solutions, Instalment / Easy Payment Plan, Mass Payment, Payment Links, Restorify (carbon — ignore).

Channels: 110+ including Apple Pay, Google Pay, e-wallets, **Fiuu Cash** (cash at 2,000+ 7-Eleven), BNPL, **Direct Debit** (Affin, Bank Islam, HSBC, Maybank, StanChart, UOB logos), Alipay+, FPX B2C/B2B, DuitNow, crypto.

In-person: terminals, bill payment, gift cards, reloads, offline e-wallets.

Developer: official API spec PDF living on GitHub `FiuuPayment/Documentation-Fiuu_API_Spec` (merchant spec v13.93 cited by their own cheatsheet). Community: `FiuuPayment/Cheatsheet-BestPractices-Fiuu_API` — “for those developers who is frustrated by the lengthy official docs.” That sentence is the DX review.

**Pricing.**

**Official site does not publish MDR, setup, annual, or settlement.** Airwallex’s 17 Apr 2026 review: dedicated pricing page 404s; quote via Booster or sales. Card approval “around a month.” Shopify Payment App overlay **+0.25%** (Fiuu FAQ via that review).

**Unofficial ranges** (label clearly; do not contract on these):

| Source | Setup | Annual | FPX | Cards |
|--------|-------|--------|-----|-------|
| DHL Discover MY comparison, 25 Feb 2026 | RM 400–499 | RM 99–499 | 2.4%–3.8% **or** RM 0.60 | ~2.4% |
| EasyStore | first-year annual waived, list **RM 899** | — | not listed | — |
| Fiuu Booster PDF Apr 2024 (old RMS) | — | — | weekly Thursday settlement example; e-wallet 2.40% or MYR 1.40 | terminal rental extra |

Treat live Fiuu as **sales-quoted**, often **percentage FPX** (bad for RM 50 deposits vs Billplz RM 0.75–1.25) unless the merchant negotiated a flat. Annual fee culture.

**Hosted vs API.**

- Default recommendation for low-tech: **Hosted Payment Page**.
- Seamless / inpage / XDK for enterprises who want to own chrome.
- Payment Links from dashboard or Virtual Terminal “Share Link.”
- Recurring: tokenise on first pay → Fiuu charges on schedule → notify merchant + buyer. Docs also describe instalment rules (end date, ≤1 year between, 3DS only on first, **no chargeback liability protection on subsequent**).
- Direct Debit is a **channel**, not Curlec-grade e-Mandate product marketing.
- Marketplace split is a **payfac-shaped** feature. We must not wrap this as “Lazuar marketplace.” GMV take-rate stays 0%.

**Recurring / tokenisation.**

- First-class **Tokenization** and **Recurring Payment** marketing. Clock can live at Fiuu (they “charge the buyer when the period is met”).
- If we adapter-wrap Fiuu, we must decide: **Fiuu-owned schedule** (we become a dumb webhook sink) vs **our BillingEngine** (we need a charge-token API equivalent to CHIP). The public pages do not document a clean “charge this token now” the way CHIP does. The PDF spec does; it is long.
- Not a reason to pick Fiuu over CHIP/Curlec for a greenfield merchant.

**Webhooks quality.**

Classic MOLPay/RMS: merchant IDs, `skey`/`vcode` MD5/SHA hashes, channel codes, IPN URL in merchant portal. Multiple integration modes (HPP vs seamless vs inpage) have **different signature recipes**. Retries exist. Event taxonomy is payment-status, not Stripe-like events.

Quality verdict: **battle-tested, documented in a 100+ page PDF, hostile to new adapters.** A Fiuu adapter is a multi-week archaeology project, not a weekend port of `IPaymentGatewayAdapter`.

**Settlement, payout, KYC.**

- **Not published.** Ask AM: frequency, minimum, currency (MYR vs multi-market), SST/GST on MDR, payout fee.
- Digital onboarding “within minutes” on homepage vs “card channel ~1 month” in Booster/FAQ. Both can be true (FPX first, cards later).
- Fiuu Cash settlement follows the cash-agent cycle, not FPX T+1.

**Invoicing / payment links.**

- Payment Links: dashboard “Default Link” + Virtual Terminal share. Aimed at freelancers, no-website, plumbers, social sellers — **same ICP as Billplz Catalog and senangPay**, with a heavier brand.
- Mass Payment = disbursement rival of Billplz Payment Order / CHIP Send.

**e-Invoice / LHDN.**

- **No MyInvois product** on the feature list fetched 16 Aug 2026.
- Restorify is carbon, not tax.

**Developer DX.**

- Six integration methods is not a gift. It is a decision tree.
- Official spec versioned into the v13.9x range (2014–2025 changelog). Cheatsheet exists because the spec is unreadable.
- DX score: **C− for enterprise teams with an AM; F for a solo Hub engineer adding a fifth adapter “because README said Fiuu.”**

**Merchant-facing checkout UX vs Lazuar portal.**

Fiuu HPP is a wide method grid (110 channels). Guest sees everything including cash-at-7-Eleven and crypto. That breadth **hurts** conversion for a RM 50 deposit (choice overload) and **helps** a TikTok Shop. Lazuar should never expose 110 channels on a small deposit. If a merchant’s Fiuu MID has cash + crypto enabled, our checkout_url would show them unless Fiuu supports channel lock in the HPP request (it does, via channel parameters in the spec). An adapter would need an explicit **allow-list** (FPX + wallets + cards) or we will ship surprise crypto on a low-ticket checkout.

**Rail vs rival.**

- **Rival checkout / rival enterprise PG.** Not a default rail.
- **Optional rail, Wave-late, pain-gated:** only when a real integrator **refuses to leave Fiuu** (existing MID, 7-Eleven cash requirement, marketplace split already contracted).
- **Never** as the way we “complete ADR 020.” The roadmap is watermarked.
- **Never** wrap Marketplace Payment as a Lazuar take-rate.
- README bullet “Billplz, Fiuu, CHIP, Xendit, Razorpay” should be **edited for honesty** (docs ticket, not this program’s code).

---

### 6. iPay88 (NTT DATA / ADAPTIS e-Commerce)

**Legal / brand.** iPay88 founded 2006; joined NTT DATA Group in 2015. Corporate entity in MY has been moving toward **NTT DATA Payment Services / NTT DATA e-Commerce Solutions**. Public site [ipay88.com](https://www.ipay88.com/) now serves **ADAPTIS e-Commerce** copy. Parent marketing: [my.nttdatapay.com](https://my.nttdatapay.com/).

**What it is in the Malaysian mental model.** The **oldest enterprise internet PG** many Woo/Magento shops still have in `web.config`: MerchantCode, MerchantKey, PaymentId, backend URL. AirAsia / banks / insurers / Watsons-class logos on the ADAPTIS e-Commerce page. If a finance controller says “we already passed iPay88 compliance,” they will not open Billplz for fun.

**ADAPTIS e-Commerce product (official page, 16 Aug 2026).**

- **Direct Link** — one-off, cards / e-wallet / online banking.
- **Email Payment Link** — request via email.
- **Recurring Payment** — schedule when a payment should be made.
- **Easy Payment Plan** — 0% bank IPP.
- Acceptance grid: Visa, Mastercard, MyDebit, UnionPay, **American Express**, Diners, JCB (Amex is a real differentiator vs Billplz/CHIP/Curlec public pages).
- Also: online banking, e-wallets, instalments, BNPL, “Others.”
- Trust wall: Affin Hwang, AirAsia, CTOS, Experian, Indah Water, Malindo, Manulife, Rakuten, Sun Life, Watsons, BookXcess, Firefly, Instarem, J&T, Lalamove, Rapid Rail, SSM, Sunway, Thunder Match.

**Pricing.**

**Not on ipay88.com or my.nttdatapay.com.** Quote-only. Unofficial ranges:

| Source | Setup | Annual | MDR |
|--------|-------|--------|-----|
| DHL Discover MY, 25 Feb 2026 (row “ADAPTIS”) | RM 688–4,988 | RM 500–800 | FPX **1.7%–2.4%**; cards **1.7%–2.4%** |
| EasyStore historical iPay88 table (Sep 2025) | — | — | Visa/MC **2.8–3.2%**; debit 2.5–2.8%; FPX 2.8–3.2% or min RM 0.60 |
| HitPay 2026 blog | “Applies on some plans” | annual applies | unpublished |

DHL also says some ADAPTIS packages **waive** setup/annual — i.e. everything is negotiable. **Do not publish a Lazuar marketing number for iPay88.**

Percentage FPX at 1.7–2.4% is **terrible** for RM 50 deposits (RM 0.85–1.20 plus possible min) versus Billplz 0.75 flat, and can be **worse than CHIP RM 1 flat**. iPay88 is priced for **higher tickets and card-present + IPP**, not for small deposits.

**Hosted vs API.**

- Classic iPay88: server POST to hosted page with MerchantCode, PaymentId, RefNo, Amount, Currency, signature; backend POST; return URL.
- Signature recipes are **versioned and unforgiving** (field order, amount formatting `"1.00"` vs sen).
- Recurring and email links are productised on ADAPTIS page; API access is AM-gated.
- Plugins exist for every legacy cart. That is how SMEs integrate; platforms still do raw POST.

**Recurring / tokenisation.**

- ADAPTIS lists Recurring Payment as a pillar. Depth is “schedule when a payment should be made,” not Curlec-style e-Mandate + usage/quantity models.
- Tokenisation exists in the enterprise contract. Not self-serve like CHIP.
- Amex + IPP are the upsell, not subscriptions.

**Webhooks quality.**

Backend URL (IPN) + return URL. Shared secret signature. Status codes, not event names. Must handle “pending” (FPX bank delay) separately from fail. No JSON metadata bag; RefNo + Remark fields.

Quality verdict: **ops-stable, DX-poor, pending-state-heavy.** An adapter must model `pending` explicitly or we will mark payments failed while Maybank is still thinking.

**Settlement, payout, KYC.**

- Enterprise KYC. Bank + card scheme reviews. Weeks, not a weekend.
- Settlement scheduled in the contract (often weekly or T+2/T+3). Not a public T+1 flex.
- This KYC friction is **why SMEs left for Billplz**.

**Invoicing / payment links.**

- Email Payment Link is the no-code path. Weaker than Billplz Catalog / CHIP links / Curlec Payment Pages.
- Not the product they win on.

**e-Invoice / LHDN.**

- **Not a MyInvois product** on the e-Commerce page.

**Developer DX.**

- Integration kits, merchant sandbox, AM Slack. No modern public OpenAPI.
- DX score: **C for a team that already has a MID; F for greenfield Hub.**

**Merchant-facing checkout UX vs Lazuar portal.**

iPay88/ADAPTIS hosted page is a dense method list with bank logos and IPP banners. Feels like 2012 enterprise. Fine for AirAsia. Worse than CHIP/Curlec/Stripe for a mobile guest. Lazuar portal → iPay88 HPP would feel like a **downgrade** versus Billplz/CHIP unless the merchant needs Amex/IPP.

**Rail vs rival.**

- **Rival enterprise checkout.** Merchants with a live iPay88 MID will compare “just use iPay88 hosted” vs “also pay for Hub.”
- **Rail: Never-by-default.** Add **only** as a named enterprise adapter if a paying integrator’s MID cannot move. Then we implement **one** ADAPTIS/iPay88 contract, not a second GHL contract (see next dossier).
- **Never** chase Amex/IPP as Lazuar features. Those stay on the HPP.

---

### 7. GHL / eGHL

**Legal / brand.** **GHL Systems Berhad** was Malaysia’s listed terminal + acquiring group (eGHL = internet PG). **NTT DATA Japan agreed on 27 May 2024 to buy 58.7%** (and subsequently moved to take the rest). 2025: company presented as **NTT DATA Payment Services**; consumer suite **ADAPTIS** (In-Store, e-Commerce, Financing, Enterprise, VAS, Bill Payments). Paysys = Android POS / EDC / soundbox. Merchant portal leftover: `secure2pay.ghl.com` titled “eGHL — Welcome to ADAPTIS Merchant Portal.” Instagram: “We have moved from eGHL MY to GHL Malaysia.” `www.eghl.my` **did not resolve** on 16 Aug 2026.

**What it is in the Malaysian mental model.**

- **Historically:** eGHL = the other enterprise HPP (often cheaper FPX flat than iPay88 in old SME quotes); GHL = the terminal at the clinic/retail counter.
- **Today:** same holding company as iPay88. In-store + enterprise + financing. Online checkout is ADAPTIS e-Commerce. Terminals are ADAPTIS In-Store / Paysys.

**Pricing.**

Not on a public GHL/eGHL/ADAPTIS page we could fetch. Old SME folklore (forums, 2022–2024 comparisons) put eGHL FPX in the **flat RM 1.00–1.50** neighbourhood with setup + annual. **Do not reuse those numbers in a contract.** Current quotes go through NTT DATA sales.

**Hosted vs API.**

eGHL HPP was a typical region PG: merchant id, password, service id, amount, callback. SiteGiant/PrestaShop plugins still say “eGHL.” New merchants are pointed at ADAPTIS. An adapter written against 2019 eGHL docs will rot.

**Recurring / tokenisation.**

Available on enterprise paper, not a self-serve developer product.

**Webhooks quality.**

Same family as iPay88: backend URL, hash, pending states. Two leftover signature dialects (eGHL vs iPay88) is the trap.

**Settlement, payout, KYC.**

Terminal + acquiring KYC. In-store settlement cycles. Not Hub’s world.

**Invoicing / payment links.**

Not their wedge. In-store + IPP + financing + bill-payment VAS are the wedge.

**e-Invoice / LHDN.**

Not their wedge.

**Developer DX.**

Worse than iPay88 public web (the eGHL marketing site is dying). DX score: **F for greenfield; do not start here.**

**Merchant-facing checkout UX vs Lazuar portal.**

Irrelevant for our ICP except: a merchant that already rents a **GHL terminal** at the desk is solving in-person collect, not Hub checkout. Hub must not try to become a terminal driver. ADAPTIS In-Store is a **Never**.

**Rail vs rival.**

- **Do not add a second adapter named GHL** if we ever add iPay88/ADAPTIS. One NTT DATA adapter, two credential shapes at most, behind one `GatewayType` or a discriminator.
- **Never** integrate Paysys / soundbox / bill-payment VAS.
- **Rival** only in the sense that a mall clinic with GHL terminals will not understand why we want them to open Billplz. Answer: online deposits ≠ counter MDR. They can keep the terminal.

---

### 8. Curlec / Razorpay MY

**Legal / brand.** Curlec founded MY 2018 (Direct Debit / e-Mandate specialist). Razorpay acquired majority in 2022 (first international expansion). Public brand **Razorpay Curlec**. BNM-regulated; PayNet member; PCI DSS Level 1. Sites: [curlec.com](https://curlec.com/), pricing [curlec.com/pricing](https://curlec.com/pricing/). Docs: [curlec.com/docs](https://curlec.com/docs/api/). **API gateway is `https://api.razorpay.com/v1`** — same host as India Razorpay. Onboarding: `accounts.curlec.com` → `easy.curlec.com/my/onboarding`.

**What it is in the Malaysian mental model.** The **subscription / tuition / gym / SaaS collections** company. “Direct Debit” means FPX **e-Mandate**: customer authorises, then you pull. Also a full PG since ~2023: cards, FPX one-shot, e-wallets, BNPL, Payment Links / Pages / Buttons / Invoices, payouts. Flash Checkout claims a pool of 4M+ saved cards (Razorpay network effect).

**This is the product our Payments README already named (“Stripe, Billplz, FPX, Curlec”) and that our `RAZORPAY` adapter is a partial, India-shaped implementation of.**

**Pricing (official table, 16 Aug 2026).** Page hero: “from 1.5% per transaction” with a footnote “18% GST applicable” (India GST language leaking onto a MY page — **confirm whether MY invoices are SST 8% or that GST line is leftover**. Do not ignore it).

| | **Basic** | **Premium** | **Enterprise (> RM 1M / mo)** |
|--|-----------|-------------|-------------------------------|
| Setup | **RM 0** (table). FAQ on the same page also says “minimal setup fee of **RM 99** for Basic” — **contradiction; treat table as primary, confirm at signup** | **RM 999** | Custom |
| Annual | None | None | Custom |
| Domestic cards | **2.40%** | **2.00%** | Custom |
| Foreign cards | **3.30%** | **3.10%** | Custom |
| FPX (+ 30 methods) | **1.50% or RM 1, whichever greater** | **1.00% or RM 1, whichever greater** | Custom |
| BNPL (Atome) | **6.00%** | **5.00%** | Custom |
| E-wallet TnG / Boost | **1.50%** | **1.30%** | Custom |
| GrabPay | **1.50%** | **1.50%** | Custom |
| Bundled free | Payment Links, Pages, Buttons, Invoices | same | same |

Airwallex Curlec review (10 Apr 2026 data, published 13 Apr 2026) matches this table and adds: settlement **T+2**; onboarding **1–2 business days**; SSM + director IC + bank statement header; **no unregistered sole props**.

**RM 50 deposit math (Basic):** FPX = **RM 1.00** (floor). Same as CHIP. Worse than Billplz Standard RM 0.75. Better than senangPay 1.5% on larger tickets? On RM 200: Curlec Basic = RM 3.00; Billplz = RM 0.75–1.25; CHIP = RM 1.00. **Curlec FPX is percentage-expensive above ~RM 67**, like senangPay. You pick Curlec for **e-Mandate and product suite**, not for cheap FPX.

**Hosted vs API.**

- Payment Links API (`client.PaymentLink.Create`) — **this is what our adapter uses** for one-shot. `short_url`, notes as metadata, `callback_url` GET.
- Registration links (`Invoice.CreateRegistrationLink`) when `setupFutureUsage`: `subscription_registration.method = card`, `max_amount = amountPaise * 10`, 10-year expiry. Card-only.
- Also: Payment Pages (no-code storefront), Payment Buttons, Invoices, Checkout / Standard / Custom / S2S (docs “integration types”), Shopify + Woo plugins.
- Flash Checkout / saved-card pool is a conversion feature we do not expose.
- **FPX** is a payment method on the hosted Checkout / link, not a separate adapter.

**Recurring / tokenisation — Curlec’s actual wedge.**

Three official billing models (Airwallex citing curlec.com/payment-gateway):

1. Fixed schedule  
2. Quantity-based  
3. Usage-based  

Plus trials, upfront, add-ons, plan change, **smart retry**. Collection rail = **Direct Debit via e-Mandate** (bank account), plus card tokens.

This is the only MY PG whose *original* product is “pull from the customer’s bank next month.” CHIP/Stripe/Razorpay card tokens cannot replace e-Mandate for tuition/gym/rental where the customer does not want a card.

**Our adapter’s off-session path is the India Recurring API** (`Order.Create` + `Payment.CreateRecurringPayment`) with:

```csharp
{ "email", "billing@lazuar.com" },
{ "contact", "0000000000" },
```

`docs/001-gaps/01-dunning-engine.md` already flags this as **likely production-broken**. Dummy contact is not an e-Mandate. Even for Indian cards it is sloppy. For Curlec MY it is the wrong product.

**Webhooks quality.**

| Property | Razorpay / Curlec |
|----------|-------------------|
| Transport | JSON POST |
| Signature | `X-Razorpay-Signature` via `Utils.verifyWebhookSignature` |
| Event we handle | **`payment.captured` only** |
| Event id | `X-Razorpay-Event-Id`, else payment id, else **fail closed** (good; CHIP should copy this) |
| Metadata | `notes` on payment entity |
| Fees | `fee` + `tax` on payment; net = amount − fee |
| Customer / token | `customer_id`, `token_id` parsed |
| Everything else | verified, ignored (`payment.failed`, refunds, mandate events, subscription events) |

Quality verdict: **modern, signed, fee-bearing.** Our mapping is **too narrow**. Curlec mandate/subscription webhooks are the events we would need for e-Mandate dunning, and we drop them.

**Settlement, payout, KYC.**

- T+2 typical (secondary, Apr 2026).
- Payouts product exists (real-time to MY banks) — rival of Billplz Payment Order / CHIP Send, not a Hub job.
- KYC: SSM mandatory, 1–2 days. Fastest serious PG after “CHIP test mode.”
- MYR settlement only. No multi-currency accounts (Airwallex’s attack line; true enough for our ICP).

**Invoicing / payment links.**

- Links / Pages / Buttons / Invoices bundled at RM 0 extra. This is a **full rival CaaS** for SMEs who do not need LHDN XML.
- Payment Pages ≈ Billplz Catalog Store ≈ Lazuar portal. Ours wins only with LHDN + WhatsApp dunning + integrator webhook.

**e-Invoice / LHDN.**

- **No MyInvois** on pricing/docs fetched. Invoices are Razorpay invoices, not UBL.

**Developer DX.**

- Best public DX in the set after Stripe: versioned docs, keys test/live, sandbox, errors, Postman, official .NET SDK (**we already depend on it**).
- Curlec docs are a skin on Razorpay docs. That is why one adapter can theoretically serve IN + MY.
- Risks: India defaults (paise language, GST 18% footnote, INR examples, `+91` muscle memory). Our adapter already hardcodes `+60100000000` as fallback phone — MY-shaped, good — then blows it in off-session with `0000000000`.
- DX score: **A for hosted links; B− for our current off-session; incomplete for e-Mandate.**

**Merchant-facing checkout UX vs Lazuar portal.**

Curlec Payment Page / Link is a polished Razorpay Checkout: method tiles, FPX bank list, cards, wallets, BNPL, optional Flash Checkout. It is the closest “Stripe-quality” hosted page in MY besides CHIP. Lazuar portal → Curlec short_url is a reasonable two-hop. Curlec Payment Page **alone** is a one-hop rival that also does subscriptions.

**Adapter vs Curlec honesty.**

| Need | Our `RAZORPAY` adapter | Curlec the product |
|------|------------------------|--------------------|
| One-shot Payment Link | Yes | Yes |
| Notes metadata | Yes | Yes |
| Card registration link | Yes (method=card) | Yes |
| e-Mandate / FPX Direct Debit | **No** | **Yes — the company reason** |
| Subscription objects / smart retry | No (Commerce owns clock) | Yes, native |
| Off-session charge | India recurring + dummy PII | Mandate + token APIs |
| payment.failed / refund / mandate webhooks | Dropped | Exist |
| GatewayType name | `RAZORPAY` | Merchants say “Curlec” |

**Rail vs rival.**

- **Rail: yes, but rename-in-docs and finish MY.** Treat `RAZORPAY` as the Curlec/Razorpay family. Do not add a second `CURLEC` type unless credentials/hosts diverge (today they share `api.razorpay.com`).
- **Highest-value missing capability in the whole list:** FPX e-Mandate as a Hub capability flag (`SupportsOffSessionBankMandate`). That is how gyms/tuition/SaaS in MY actually recurring-charge. CHIP card token is the other half.
- **Rival checkout** via Payment Pages / Invoices. Same rule as Billplz Catalog: do not clone; beat on LHDN + one webhook + fulfillment.
- **Never** use dummy `billing@lazuar.com` in production. That is a bug, not a strategy.

---

### 9. Revenue Monster

**Legal / brand.** Revenue Monster. Site: [revenuemonster.my](https://revenuemonster.my/). Docs: [doc.revenuemonster.my](https://doc.revenuemonster.my/docs/introduction/overview/). Merchant: `merchant.revenuemonster.my`. Claims: >10,000 brands MY/SG/SEA; BNM-licensed PG; PCI-DSS **4.0**; widest e-wallet grid (TnG, MAE, ShopeePay, GrabPay, Boost, S Pay Global, M Cash, Setel, Alipay, WeChat, plus MyDebit, UnionPay, DuitNow, FPX, Visa/MC, Atome, Grab PayLater).

**What it is in the Malaysian mental model.** Not “a bill API.” An **omnichannel super-app**: online gateway + smart POS + merchant app + loyalty + membership + **LivePay** (TikTok/FB/IG live selling) + **alacarte.my** store + “e-Invoice Payment Links” + gamification / WhatsApp mini-programs (upsell on the pricing footer). F&B, retail, live sellers. A merchant that bought RM for the counter QR will already have online checkout whether they want Hub or not.

**Pricing (official, [revenuemonster.my/pricing](https://revenuemonster.my/pricing), 16 Aug 2026).**

| | **Advanced (SME)** | **Corporate+** |
|--|--------------------|----------------|
| Setup | **RM 499 one-time** | Personalised |
| Extra settlement account | **RM 99** one-time each | Custom |
| MDR (cards / FPX / wallets) | **“Competitive rates” — not published** | Custom |
| Payout | FAQ: **typically T+2** | Custom |
| Terminal rental | **RM 50 / month / unit** | Tailored |
| Terminal deposit | RM 300 / unit | — |
| Terminal buy-off | RM 1,300 / unit | — |
| SIM | RM 120 / year optional | — |
| Training | 1 session | 2 sessions |
| Custom integration | Optional, scoped | Included-ish + “quick support” |
| Store (alacarte.my) | Limited-time included | Included |
| Wallet / membership / custom workflows | — | Corporate+ |

**No public FPX sen or card %.** You cannot price a RM 50 deposit. Assume “not the cheap bill company.”

**Hosted vs API.**

- “One-time API integration” then all methods. Open API + iOS/Android SDKs + plugins.
- Payment Link + e-Invoice from merchant portal if you have no website.
- Recurring Payment is a separate product page (`/recurring-payment`).
- LivePay is a **separate product** (social live). Never a Hub job.
- Loyalty program hooks at checkout — rival of merchant loyalty, not of Pay.

**Recurring / tokenisation.**

- Marketed (cards, e-wallets, DuitNow, BNPL “all in one” on the digital-invoice page — that sentence is doing too much work). Treat as **card/wallet token + their recurring product**, not as Curlec e-Mandate, until docs are read per MID.

**Webhooks quality.**

Open API docs exist (`doc.revenuemonster.my`). Typical modern HMAC JSON. Need an adapter spike to rate event taxonomy, pending states, and whether metadata is a bag. Not researched to CHIP depth because **we should not build this adapter next**.

**Settlement, payout, KYC.**

- T+2 typical.
- Onboarding: register → profile → upload docs → approval → channels on.
- Extra RM 99 per settlement account = multi-branch feature.

**Invoicing / payment links.**

- **Digital invoicing + payment links** ([/digital-invoice](https://revenuemonster.my/digital-invoice)): WhatsApp / Telegram / email / SMS. Instant generation, no technical setup. Recurring mentioned on the same page.
- “e-Invoice Payment Links” on the pricing feature list.

**e-Invoice / LHDN.**

- RM has used “e-Invoice” since at least 2019 in the **payment-request** sense (Instagram 2019: “RM E-Invoices”).
- 2026 pricing still says “e-Invoice Payment Links.”
- **I did not find a public MyInvois UBL/LHDN developer product equivalent to Lazuar’s Lhdn module.** Do not assume RM submits to MyInvois. Assume they sell **invoice-shaped payment links**. If a specific Corporate+ SKU now pushes LHDN, it is AM-land. Lazuar still wins on actual UBL 2.1 + status polling + `invoice.valid` webhooks.

**Developer DX.**

- Real docs site, OAuth merchant login, SDKs. Better than Toyyib/senangPay/iPay88 public.
- Product surface is huge; a payments-only adapter must ignore 80% (loyalty, LivePay, terminals, mini-programs).
- DX score: **B for a retail omnichannel app; C as a Hub rail** (we would spend more time refusing features than mapping checkout).

**Merchant-facing checkout UX vs Lazuar portal.**

RM hosted checkout is mobile-optimised, wallet-first, sometimes loyalty-aware. LivePay and invoice links are how TikTok sellers get paid **without** a CaaS portal. A merchant who is already live-selling on TikTok with RM will not adopt Lazuar portal for deposits. Different ICP.

**Rail vs rival.**

- **Rival omnichannel OS.** Not a CaaS peer.
- **Rail: Never-by-default.** No unique online method Billplz+CHIP+Curlec cannot present. Terminals/LivePay/loyalty are out of scope.
- **Exception:** a large F&B-adjacent chain whose finance team will only settle to an existing RM MID. Then one adapter, methods allow-listed, loyalty **off**.

---

## Adapter status in our repo

### What is actually wired (16 Aug 2026)

`PaymentGatewayFactory` resolves `IPaymentGatewayAdapter` by `GatewayType`. Registration + allow-list:

| `GatewayType` | Class | File |
|---------------|-------|------|
| `STRIPE` | `StripeGatewayAdapter` | `…/Gateways/StripeGatewayAdapter.cs` |
| `BILLPLZ` | `BillplzGatewayAdapter` | `…/Gateways/BillplzGatewayAdapter.cs` |
| `CHIP` | `ChipCollectGatewayAdapter` | `…/Gateways/ChipCollectGatewayAdapter.cs` |
| `RAZORPAY` | `RazorpayGatewayAdapter` | `…/Gateways/RazorpayGatewayAdapter.cs` |

`Endpoints.MapPaymentsEndpoints` allow-list is the same four strings. Anything else — including the names in README/ADR 020 (`Fiuu`, `SenangPay`, `Xendit`, `Midtrans`, `Cashfree`) — returns **400** `Unsupported payment gateway type` so the processor does not retry into a 500.

Ops / platform: `PUT /payment-config` with `gateway_type`, `api_key`, `collection_id` (overloaded: Billplz collection / CHIP brand / etc.), `webhook_secret`, `secret_key`, `is_active`.

M2M: `POST /api/v1/integrations/payments/checkouts` picks the workspace’s **active** BYOK gateway (or `gateway_name` if sent). Guest is redirected to `checkout_url`. Integrator waits for signed `payment.completed` / `payment.failed`.

Commerce: `GenerateCheckoutSessionQuery` → same adapters. Customer portal handler is **Stripe-only**. Off-session from `BillingEngineJob` / `DunningEngineJob`.

### Per-adapter capability (code, not marketing)

| Capability | Stripe | Billplz | CHIP | Razorpay (code) |
|------------|--------|---------|------|-----------------|
| Hosted checkout | Checkout Session | v3 Bill URL | Purchase `checkout_url` | Payment Link `short_url` |
| Amount rounding | Stripe minor units | **Truncate** `*100` | **Banker round** | **Truncate** |
| Metadata out | Session metadata | `reference_1/2` + **callback query** (`type`, `reference_1`, `checkout_id`) | `purchase.metadata` | `notes` |
| Metadata in | Yes | Reconstructed (ADR 009) | Yes | Yes |
| Signature | `Stripe-Signature` | HMAC-SHA256 `x_signature` (try with/without extra fields) | RSA PEM `X-Signature` | `X-Razorpay-Signature` |
| EventId discipline | Stripe event id | Bill id | Envelope `id` or **new Guid** | Header or payment id or **fail closed** |
| Exact fees | balance_transaction | **Always 0** (estimation params dead) | `fee_amount` / `net_amount` | `fee` + `tax` |
| Maps paid | Yes | `paid`/`state` | `purchase.paid` only | `payment.captured` only |
| Maps failed | Partial / gaps | Unpaid callback → `PAYMENT_FAILED` (publish still gappy per gap-06) | `purchase.payment_failure` | Other events ignored |
| Maps refund webhook | Dispute yes; refund webhook incomplete | n/a | `payment.refunded` **registered, not mapped** | Not mapped |
| Vault / setupFutureUsage | `setup_future_usage` | Ignored | `force_recurring` + `skip_capture` | Registration link, method=**card** |
| Off-session | PaymentIntent off_session | **`NotSupportedException`** | GET token purchase + charge | Recurring payment + **dummy email/phone** |
| Refund API | Yes | **`false`** | Yes (optional amount) | Yes |
| Customer portal | Yes | Throws | Throws | Throws |
| Sandbox switch | Key prefix `sk_test_` / `sk_live_` | Host + `App:BillplzEnvironment`; **not** K1 prefix | Test mode in CHIP portal; we always hit `gate.chip-in.asia` | Key pair |
| Auto webhook provision | No | No (URL stamped on each bill) | **Yes** on key save | No |
| Production callback hygiene | Stripe dashboard URL | `BillplzPublicBase` public HTTPS required | Localhost rewritten to `lazuar-local-dev.com` | n/a |

### Known adapter defects that change the competitive story

1. **Billplz is v3 (frozen).** Official new work is v4/v5 (Agreements, Payment Orders, checksum). We will drift.
2. **Billplz cannot refund or vault in our code.** Product must say “refund in Billplz dashboard” and “no auto-charge on FPX.”
3. **Billplz fees are lies (always 0).** Ledger net = gross. Fine if we never show “you earned RM X after fees.” Bad if we do.
4. **CHIP EventId Guid fallback** can double-fulfill. Copy Razorpay fail-closed.
5. **CHIP refund + preauth events registered and dropped.** Refunds via API may never emit `GatewayRefundCompleted` from webhook.
6. **Razorpay off-session dummy PII** is a production bug. Blocks treating RAZORPAY as a real Curlec rail.
7. **Razorpay is not e-Mandate.** Naming it Curlec in README without mandate support is a lie.
8. **Interface is capability-blind.** Callers learn Billplz has no portal by catching exceptions. Need `SupportsOffSession`, `SupportsRefund`, `SupportsPortal`, `SupportsBankMandate`, `SupportsPartialRefund`.
9. **`KEY_MODE_MISMATCH` only understands Stripe-shaped K2.** Billplz/CHIP/Razorpay secrets will not be gated by test/live.
10. **Portal custom pay route is `notFound()`.** We are worse at “send this one invoice” than every PG on this list.
11. **README / ADR 020 still advertise Fiuu + SenangPay as BYOK.** Honesty gap versus factory allow-list.

### Endpoints the integrator actually sees

| Method | Path | Auth | Role |
|--------|------|------|------|
| POST | `/api/v1/integrations/payments/checkouts` | Bearer K1 `payments.checkouts:write` | Create hosted session |
| GET | `/api/v1/integrations/payments/checkouts/{id}` | `payments.checkouts:read` | Reconcile UX, **not** money |
| GET | `/api/v1/integrations/payments/me` | any `payments.*` | Workspace + `has_active_gateway` + `gateway_names` (no K2 leaked) |
| POST | `/webhooks/payments/{gatewayType}/{tenantId}` | processor signature | Hop A inbound |
| GET/PUT | `/api/v1/platform/payment-config` (and Ops twin) | human | BYOK vault |
| Commerce public | `/{tenant}/checkout/{slug}` then redirect | guest | CaaS form |
| LHDN | `/lhdn/*` + `invoice.*` | separate | Tax, not acquiring |

Normalised outbound (One dispatcher): `payment.completed`, `payment.failed` with `X-Lazuar-Signature: t=…,v1=…`. Commerce additionally: `subscription.*`, `order.completed`, `payment_link.paid`.

### What “integrate them as a rail” means in *this* codebase

A new MY PG is not a plugin. It is:

1. `IPaymentGatewayAdapter` implementation.
2. `GatewayType` string + factory registration + **Endpoints allow-list**.
3. Ops `PaymentSettingsPage` fields (CHIP needs Brand ID; Fiuu will need more than two secrets).
4. `UpdatePaymentConfigCommandHandler` provision (CHIP-style auto webhook if the PG supports it).
5. Sandbox/live rule that does **not** reuse Billplz host heuristics.
6. Tests for signature vectors, minor units, EventId fail-closed, unpaid ≠ paid.
7. Capability flags so Commerce dunning does not throw into the void.
8. Docs: honesty about what the hosted page shows (FPX/TnG/Grab) vs what Hub implements.

Until (1)–(8) exist, README must not list the logo.

---

## Feature tables

### A. Typical MDR and membership (official unless marked ※)

| | Annual / setup | FPX B2C | Local card | Wallet | BNPL | Settlement (typical) |
|--|----------------|---------|------------|--------|------|----------------------|
| **Billplz Basic** | RM 0 | **RM 1.25** | 1.8% | 1.5% | Atome 6% | FPX next BD; card T+2 |
| **Billplz Standard** | RM 999 / yr | **RM 0.75** | 1.5% | 1.5% | Atome 6% | same |
| **CHIP Collect** | RM 0 | **RM 1.00** (B2B RM 2) | 2.0% credit / **1.0% debit** | 1.4% | Atome 5.3%+SST; SPayLater 1.4% | FPX next day; card T+2 |
| **Toyyib Standard** | RM 0 | **RM 1.00** | 1.50%※ partner + RM 100/yr | DuitNow QR 1% or RM 1 | — | FPX 1–4 BD |
| **Toyyib Santai NPO** | RM 0 | **RM 0** | — | — | — | **10 BD** |
| **senangPay Starter** | **RM 199 / yr** | **RM 1 or 1.5% ↑** | RM 0.65 or 2.5% ↑ (Advance+) | RM 0.65 or 1.5% ↑ | SPayLater 2%; Grab 6%; Atome 5.5% | not published; “faster” on Advance |
| **Fiuu** | quote (※ annual often hundreds; EasyStore cites RM 899) | quote (※ often **%** not flat) | quote | quote | yes | **not published** |
| **iPay88 / ADAPTIS** | quote (※ DHL RM 688–4988 setup, RM 500–800 / yr) | quote (※ DHL 1.7–2.4%) | quote (※ 1.7–3.2%) | yes | IPP + BNPL | contract |
| **GHL / eGHL** | quote (same group) | quote | quote | yes | IPP | contract / terminal cycle |
| **Curlec Basic** | RM 0 (FAQ also says RM 99 — confirm) | **1.5% or RM 1 ↑** | 2.40% | 1.50% | Atome 6% | T+2※ |
| **Curlec Premium** | RM 999 setup | **1.0% or RM 1 ↑** | 2.00% | 1.30–1.50% | Atome 5% | T+2※ |
| **Revenue Monster** | **RM 499 setup** | unpublished | unpublished | unpublished | yes | T+2※ |
| **Lazuar Pay** | SaaS / credits; **0% GMV** | = merchant’s rail | = rail | = rail | = rail if collection has it | = rail |

※ unofficial or FAQ-only.

### B. Hosted page vs API vs no-code link

| | Hosted pay page | Modern REST + JSON metadata | No-code link / form | Dashboard invoices | In-person |
|--|-----------------|----------------------------|---------------------|--------------------|-----------|
| Billplz | Bill Page | v3/v4/v5; metadata weak | Catalog Link / Store | Bills + Catalog Billing | no (not their game) |
| CHIP | checkout_url | **Yes** | Payment Links | receipts | CHIP mini + POS |
| Toyyib | Bill URL | form POST, weak | create bill in UI | bills | no |
| senangPay | Payment form | mixed / DOKU | **Catalog / quotation** | “e-invoice & quotation” | senang Terminal SKU |
| Fiuu | HPP + seamless + inpage | PDF spec | Payment Links + VT share | VT | terminals + 7-Eleven cash |
| iPay88 | HPP / Direct Link | legacy POST | Email link | limited | via ADAPTIS In-Store |
| GHL | eGHL HPP (legacy) | legacy | weak | no | **core** (Paysys) |
| Curlec | Checkout / Links | **Yes** (`api.razorpay.com`) | Links, Pages, Buttons | Invoices | no |
| RM | Hosted + SDKs | Open API | **e-Invoice links** + LivePay | Digital invoice | **core** (smart POS) |
| **Lazuar** | Portal form **then** rail HPP | Hub M2M + Commerce | Commerce product URL; custom `/pay/{id}` **hidden** | Commerce + Billing docs; not a WhatsApp invoice app | Not Pay |

### C. Recurring / tokenisation / refunds / webhooks

| | Vault | Who runs the clock | Off-session rail | Refund API | Webhook grade | Pending state |
|--|-------|--------------------|------------------|------------|---------------|---------------|
| Billplz | Card Agreements v5 beta / Auto-Deduct; **not FPX** | Merchant or Catalog Billing | Not in our adapter | Dashboard / PO; **our adapter false** | HMAC form; no events | bill `due`/`paid` |
| CHIP | Card token (`force_recurring`) | **Merchant (us)** | Charge token API — **we implement** | Yes | RSA JSON **A−** | `pending_charge`; preauth separate |
| Toyyib | No | n/a | No | No | MD5 form **D** | status 2/4 pending |
| senangPay | Token (Advance+) | senangPay recurring | Token pay_cc | Hash’d refund (docs) | HMAC/MD5 **D+** | yes |
| Fiuu | Token | Fiuu can run it | Spec-heavy | Yes in spec | PDF hash **C** | yes |
| iPay88 | Enterprise token | Their recurring | AM | AM | legacy **C** | **yes, must model** |
| GHL | Enterprise | AM | AM | AM | legacy **D** | yes |
| Curlec | Card + **FPX e-Mandate** | Curlec subscriptions **or** us | Mandate + recurring APIs; **our code is India dummy** | Yes | signed JSON **A** (we map 1 event) | yes |
| RM | Marketed | Their product | Unknown depth | Unknown | modern **B** (not spiked) | likely |
| **Lazuar** | Depends on rail | Commerce jobs | Stripe/CHIP/(broken)Razorpay | Stripe/CHIP/Razorpay; Billplz no | One `payment.*` | session status; browser redirect ≠ paid |

### D. KYC, payout, LHDN

| | KYC speed (public claim) | Payout product | MyInvois / LHDN |
|--|--------------------------|----------------|-----------------|
| Billplz | Days–weeks※ | **Payment Order** real-time | **No** |
| CHIP | Digital + docs; test mode first | **CHIP Send** RM 1 | **No** |
| Toyyib | Self-serve | Settlement summary API (enterprise) | **No** |
| senangPay | Instant FPX/wallet; cards later | Payout API Advance+ | Marketing “e-invoice” ≠ proven MyInvois |
| Fiuu | Minutes digital; **cards ~1 month** | Mass Payment | **No** |
| iPay88 | Weeks, enterprise | contract | **No** |
| GHL | Enterprise / terminal | contract | **No** |
| Curlec | **1–2 days**, SSM required | Payouts | **No** |
| RM | After doc review | T+2 + extra accounts | “e-Invoice links” ≠ proven UBL |
| **Lazuar LHDN** | Intermediary TIN / cert pending signatures | n/a | **Yes — first-class module** |

### E. Rail vs rival (decision table)

| | Integrate as Hub rail? | Treat as rival checkout / OS? | Why |
|--|------------------------|-------------------------------|-----|
| **Billplz** | **Yes — primary** | Yes (Catalog / WhatsApp bills) | Default MY guest money. Deepen, don’t clone Catalog. |
| **CHIP** | **Yes — primary #2** | Yes (Payment Links, mini) | Best local API + vault + refunds + QR. Harden adapter. |
| **ToyyibPay** | Only if a cohort is stuck | Yes (NPO/SME bills) | Worse Billplz. Santai is not our ICP. |
| **senangPay** | Pain-gated Later | **Yes — form/quotation rival** | Annual fee, % FPX, DOKU flux, account-level callbacks. |
| **Fiuu** | Pain-gated enterprise | **Yes — enterprise / cash / 110 channels** | No public price; huge spec; README-honesty issue. |
| **iPay88 / ADAPTIS** | Pain-gated enterprise, **one** NTT adapter | Yes — legacy enterprise HPP | Amex/IPP/MID lock-in. Bad default for RM 50 FPX. |
| **GHL / eGHL** | **Do not add separately** | In-store OS / terminals | Same group as iPay88. Terminals = Never for Pay. |
| **Curlec / Razorpay MY** | **Yes — finish the rail we already named** | Yes (Pages / Invoices) | e-Mandate is the MY recurring truth. Fix dummy PII. |
| **Revenue Monster** | Never-by-default | **Yes — omnichannel + LivePay + POS** | No unique online rail; huge OS surface. |
| **HitPay** (adjacent; see `06`) | Pay-only K2 if ever | Yes | Do not rebuild as Aura/Hub-native acquirer. |
| **Stripe MY** (adjacent; see `04`) | Already a rail | Weak informal rival (expensive FPX) | Keep for cards / international / portal. |

### F. Merchant checkout UX scorecard vs Lazuar portal

Score 1–5, 5 = better for **Malaysian guest converting on a phone from WhatsApp**. This is not “more features.”

| Surface | Time-to-send for owner | Hops for guest | Local methods visible | Trust chrome | After-pay fulfillment | LHDN | Notes |
|---------|------------------------|----------------|----------------------|--------------|----------------------|------|-------|
| Billplz Bill / Catalog | **5** | **5** (one link) | 5 | 5 (PDRM-grade) | 2 (receipt only) | 1 | Informal winner |
| CHIP Link / checkout | 5 | 5 | **5** (QR + GPAY) | 4 | 3 | 1 | Best modern HPP |
| Toyyib bill | 5 | 5 | 3 | 3 | 2 | 1 | Cheap, slow settle |
| senangPay form / quotation | **5** | 5 | 4 | 3 | 2 | 2 (PDF “invoice”) | Quotation ICP |
| Fiuu link / HPP | 3 | 4 | 5 (too many) | 4 | 3 | 1 | Choice overload |
| iPay88 / ADAPTIS | 2 | 3 | 4 | 4 enterprise | 2 | 1 | Ugly, pending-heavy |
| GHL terminal | n/a online | n/a | 5 in-store | 4 | 3 | 1 | Not a link product |
| Curlec Link / Page | 5 | 5 | 5 | 4 | 3 + native subs | 1 | Best SME CaaS rival |
| RM invoice / LivePay | 5 | 5 | 5 wallets | 3 | 3 + loyalty | 2 (link “e-invoice”) | Live-seller ICP |
| **Lazuar portal** | **2** (need product + gateway) | **3** (form → HPP → bank) | **1** (methods only after redirect) | 3 | **4** (Commerce unlock + `payment.*`) when soak is real | **5** (module exists) | Wins on identity/coupon/LHDN/webhook; loses send-a-link |
| **Hub M2M only** (no portal) | n/a (integrator) | 2 (app → HPP) | 1 on our side | 3 | **5** (one signature) | 5 if they call LHDN | Our actual wedge for Aura / second apps |

**The UX hole that makes us lose to Billplz/CHIP/Curlec/senangPay:** we do not have a first-class “type RM 50, copy link, send on WhatsApp” that is *ours*. Commerce custom checkout is hidden. Portal is a product-attached creator checkout. Informal MY comparison is a **link**, not a product slug.

That does not mean “build Catalog.” It means Hub should treat **the processor hosted URL** as the guest method UI, and treat **our** job as: correct amount, signed fulfillment, remainder honesty, LHDN if wanted, dunning if vaulted.

---

## Recommended next adapters vs never

### Do this first (not new logos)

These beat any sixth adapter.

1. **Prove hop A + hop B** (cashier soak). A fifth PG on an unproven cashier is theatre.
2. **Capability flags** on `IPaymentGatewayAdapter` so dunning/refund/portal stop using try/catch.
3. **Billplz honesty pack:** copy + Ops UI: no API refund, no FPX auto-charge, fees unknown, v3 frozen, environment ≠ K1 prefix. Optionally spike v4 bills (same object, future-proof).
4. **CHIP harden:** fail-closed EventId; map `payment.refunded`; do not rewrite webhook to `lazuar-local-dev.com`; document Brand ID; consider `payment_method_whitelist` so deposits are FPX+wallets+cards, not stablecoins.
5. **Razorpay/Curlec finish:** delete dummy PII; map `payment.failed` + refunds; document as **Curlec / Razorpay MY**; spike **e-Mandate** as a separate capability (do not pretend registration-link-card is Direct Debit).
6. **README/ADR 020 honesty:** remove Fiuu/SenangPay from “we have these BYOK gateways” lists until they exist.
7. **Do not un-hide `/pay/{sessionId}` just to chase senangPay quotations** unless a real CaaS ICP asks. Rival-checkout envy is how ADR 023 dies.

### Next adapters, ordered

| Priority | Adapter | When | Scope cap |
|----------|---------|------|-----------|
| **P0** | *(none new)* | After soak + CHIP/Razorpay harden | — |
| **P1** | **Curlec e-Mandate** (extend `RAZORPAY`, don’t fork) | Commerce or a MY SaaS/tuition integrator needs bank pull | Mandate APIs + webhooks; still BYOK; clock stays Commerce or Curlec-native, pick one |
| **P2** | **Fiuu HPP-only** | A paying merchant/integrator **cannot** leave Fiuu (cash 7-Eleven, existing MID, marketplace already live) | Hosted page + IPN verify + channel allow-list. No seamless, no XDK, no marketplace split, no VT |
| **P3** | **iPay88/ADAPTIS HPP-only** | Same, for iPay88 MIDs | **One** NTT DATA adapter. Model `pending`. No GHL twin. No terminals |
| **P4** | **senangPay hosted + HMAC** | Only if we lose a documented cluster of Advance merchants who refuse CHIP/Billplz | Hosted form only. Account-level callback router. No PAN, no MD5 merchant-hosted |
| **P5** | **ToyyibPay** | Only NPO/sekolah cohort with written demand | createCategory/createBill + MD5 callback. No white-label `createAccount` |
| **P6** | **Revenue Monster online-only** | Almost never | If ever: checkout + webhook, loyalty/LivePay/POS **off** |

### Never (company-shape, not “hard”)

| Never | Why |
|-------|-----|
| Become a **Billplz Catalog / senangPay form builder / Curlec Payment Pages** clone | ADR 019/023. We lose to their 30-second send. We win on fulfillment + LHDN + one signature. |
| Become a **BNM acquirer** or take GMV | Capital, scheme, chargebacks. |
| **GHL / Paysys / CHIP mini / RM terminal / Fiuu VT** inside Pay | In-person. Out of CaaS. |
| **RM LivePay, loyalty, WhatsApp mini-programs, alacarte.my** | Super-app envy. |
| **Fiuu Marketplace / iPay88 sub-merchant / Toyyib createAccount** | Payfac / reseller shape. |
| **Merchant-hosted PAN** (senangPay merchant-hosted, raw MOTO) | PCI blast radius. Always hosted or token. |
| **Aura-native FPX / DuitNow QR / TnG** | Rebuild PayNet inside Aura/Hub. Methods stay on the rail HPP. |
| **HitPay as a locked Hub acquirer** | Wrap later as K2 if ever; see `06`. |
| **Second adapter named GHL** plus iPay88 | Same group. |
| **Auto-charge no-show on FPX** | No mandate. Keep-deposit or vaulted card/e-mandate only. |
| **Use Billplz for Aura Pro / Lazuar SaaS seats** | System A = Paddle. |
| **Claim LHDN because the PG said “e-invoice”** | Submit UBL ourselves or don’t claim it. |
| **Stablecoins / crypto on a small deposit** because CHIP/Fiuu can | Allow-list methods. |
| **Implement ADR 020 Phase 1 as a logo checklist** | Watermarked wishlist. Xendit/Midtrans/Cashfree are other countries (`06`). |

### What to tell sales / merchants (one liners)

- “If you have **Billplz**, paste the collection + secret. Guests pay on Billplz. We tell your app when it actually paid.”
- “If you need **cards saved and refunds via API**, use **CHIP** (or Stripe). Billplz cannot auto-charge FPX.”
- “If you need **bank auto-debit** for memberships, that is **Curlec e-Mandate**, not Billplz, not a finished Hub feature today.”
- “If you have **iPay88 / Fiuu / RM** already, you can keep sending *their* links. Hub is worth it when you want one webhook + LHDN + deposit math. We will not wrap those rails unless you cannot move.”
- “We are not a cheaper Billplz. Their FPX can be RM 0.75. We charge software, not MDR.”

---

## Tracker IDs

Schema authority: [`20-sequencing-and-tracker-schema.md`](./20-sequencing-and-tracker-schema.md). Living matrix: [`00-checklist-tracker.md`](./00-checklist-tracker.md).

Promote the rows below into `00-checklist-tracker.md` only via the schema’s promotion rule. **Not a commitment to ship.**

| ID | Feature | Pay now | V | W | P | Class | Why |
|----|---------|---------|---|--:|--:|-------|-----|
| **PY-023** | Billplz rail honesty (v3, no refund API, no FPX vault, fees 0, env ≠ K1) | partial | Partial | 1 | 0 | hygiene | We already depend on Billplz; the lie is more dangerous than a missing logo. |
| **PY-024** | Billplz v4 bills (or documented freeze) | none | Later | 8 | 2 | later-nice | Official v3 is frozen. Only after soak. |
| **PY-025** | Billplz Agreements / Auto-Deduct (card) as optional capability | none | Later | 10 | 3 | later-nice | Do not advertise until implemented; never call it FPX auto-charge. |
| **PY-026** | CHIP EventId fail-closed + refund webhook mapped | partial | Partial | 1 | 1 | hygiene | Adapter is live; EventId Guid and dropped `payment.refunded` are correctness holes. |
| **PY-027** | CHIP method allow-list on deposits | none | Later | 8 | 2 | later-nice | Stop surprise stablecoin/BNPL on RM 50. |
| **PY-028** | Curlec/Razorpay off-session dummy PII removed | partial | Partial | 1 | 0 | hygiene | Production-broken dunning. |
| **PY-029** | Curlec named in Ops/docs as Razorpay MY; map failed/refund events | partial | Later | 8 | 2 | table-stakes | README already says Curlec. |
| **PY-030** | FPX e-Mandate (Curlec) capability + dunning path | none | Later | 10 | 2 | differentiator | Only MY-honest membership pull. Pain-gated to a real subscription ICP. |
| **PY-031** | Adapter capability matrix type (`Supports*`) | none | Later | 8 | 1 | hygiene | Stops Billplz off-session from being a surprise exception. |
| **PY-032** | README/ADR 020 logo honesty (drop Fiuu/SenangPay-as-shipped) | doc_off | Partial | 1 | 2 | hygiene | Docs ticket. |
| **PY-033** | Fiuu HPP adapter | none | Later | 11 | 3 | later-nice | Pain-gated existing MID only. |
| **PY-034** | iPay88/ADAPTIS HPP adapter (single NTT type) | none | Later | 11 | 3 | later-nice | Pain-gated; includes pending state; **not** a GHL twin. |
| **PY-035** | senangPay hosted adapter | none | Later | 11 | 3 | later-nice | Pain-gated; no PAN. |
| **PY-036** | ToyyibPay adapter | none | Later | 11 | 3 | later-nice | NPO cohort only. |
| **PY-037** | Revenue Monster adapter | none | **Never** default | — | — | trap unless written exception | No unique online rail. |
| **PY-038** | GHL / eGHL / Paysys / terminals as Pay rail | none | **Never** | — | — | trap | In-person; same group as PY-034. |
| **PY-039** | Clone Catalog / Payment Pages / quotation builder / LivePay | none | **Never** | — | — | trap | Rival checkout envy. |
| **PY-040** | Payfac / marketplace split / Toyyib createAccount / Fiuu marketplace | none | **Never** | — | — | trap | GMV take-rate. |
| **PY-041** | Merchant-hosted PAN / raw MOTO | none | **Never** | — | — | trap | PCI. |
| **PY-042** | PG “e-invoice” treated as LHDN | none | **Never** | — | — | trap | Lhdn module is the only claim. |
| **PY-043** | WhatsApp-send of **Hub/processor checkout URL** with amount honesty | partial | Later | 3 | 1 | differentiator | The actual UX gap vs Billplz links. Not a form builder. |

**Wave reminder:** Wave 0–1 is soak + honesty (`PY-023`, `PY-026`, `PY-028`, `PY-032`). New logos (`PY-033`+) are Wave **11** at the earliest. e-Mandate (`PY-030`) is Wave **10** and only if a subscription ICP is real. Catalog clones (`PY-039`) have **no wave**.

If `20` later assigns a different family prefix for Pay-only rows, **keep these IDs** and alias rather than minting a second taxonomy.

---

*End of 05. Do not condense this file into the tracker; promote IDs only. Do not ship adapters from this document. Production guest pay remains unclaimed. Lazuar is BYOK software, not an acquirer.*
