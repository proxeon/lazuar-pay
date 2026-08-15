# 06 — SEA fintech platforms vs Lazuar

**Program:** `plans/007-feats` — competitor features vs Lazuar Pay  
**Date:** 16 August 2026  
**Status:** Full uncondensed analysis — no product code from this file  
**Subject:** Lazuar Pay (BYOK Checkout-as-a-Service + LHDN, Malaysia-based) versus the regional platforms a merchant, founder, or salon owner will actually name when they say “I already have a payment link / invoice / subscription tool.”  
**Especially:** HitPay and Xendit as the closest *productized checkout + invoices* rivals in SEA.  
**Not:** a salon-OS comparison. This file is about **money rails and checkout products**, not calendars. Aura is a Hub **customer**, not a rival.

**Standing constraints (do not contradict):**

- Guest money (System B / Lazuar Pay / Billplz) is **not** SaaS money (System A / Paddle).
- Production guest fulfillment on Aura is **not** claimed until a sandbox three-book soak.
- Lazuar is **not** Merchant of Record for guest GMV. BYOK. Settlement stays on the merchant processor account.
- Lazuar monetizes SaaS + prepaid utility credits (LHDN XML, WhatsApp dunning), **not** a GMV take-rate.
- Do not treat “they have QR” as a product gap in Aura domain. QR is a **processor / rail**. Aura/Lazuar consume it via an adapter.
- Do not become an acquirer, marketplace, or treasury bank to “match” Xendit / Airwallex / Rapyd.

**Lazuar honesty watermark (repo, 16 Aug 2026):**

| Claim in marketing / README | Ground truth in this repo |
| --- | --- |
| BYOK gateways | **Yes** — vaulted keys; money settles on merchant processor. Adapters actually compiled: **Billplz, CHIP, Stripe, Razorpay**. |
| “Local Asian Gateways: Billplz, Fiuu, CHIP, Xendit, Razorpay” | **Partial.** Fiuu adapter: **not present**. Xendit adapter: **not present** (`PaymentGatewayFactory` only resolves `BILLPLZ` / `CHIP` / `STRIPE` / `RAZORPAY`). Xendit is a *named* Phase-1 ambition, not a shipping adapter. |
| Commerce subscriptions + dunning | **Partial.** Commerce module owns catalog, public buy links, subscription lifecycle, email dunning templates. WhatsApp dunning is **Phase D / roadmap**, not guaranteed demoable. |
| Double-entry billing ledger | **Yes** as architecture. `Billing` module treats gateway fee + tax as first-class ledger lines. |
| LHDN e-Invoice | **Backend pipeline exists** (UBL 2.1 XML strategies for invoice / credit / debit / refund / self-billed; submit + poll; `invoice.valid` / `invoice.invalid` webhooks). **Ops B2B UX is intentionally unrouted** until Phase D.3 (ADR 021 + 023). XAdES v1.1 signing is **unimplemented** pending sandbox certificates. |
| Hub is not MoR | **True.** `PAYMENTS_NOT_CONFIGURED` if no BYOK gateway. |
| Headless checkout | **True as intent.** `lazuar-portal` is the cash register; domain objects (orders, bookings) stay in the integrator app for Payments cashier. Commerce is the exception (Hub owns catalog). |

If a later sentence in this file says “Lazuar has X,” it means the **module / contract exists**, not that every tenant in production is using it.

---

## Method

### What this file is for

A Malaysian or regional merchant who already books on WhatsApp, or a founder who already has a Framer / Next.js / Shopify front, will not compare Lazuar to Boulevard. They will compare it to:

1. **The payment link they already send** — HitPay, Xendit Payment Links (historically xenInvoice), Billplz bill, Midtrans Payment Link, PayMongo Payment Link.
2. **The invoice they already send** — HitPay Invoicing, Xendit Invoice API / Payment Links, Airwallex Billing, Aspire invoicing.
3. **The subscription they already run** — Xendit Subscriptions, HitPay Recurring Billing, Midtrans Subscription API, PayMongo Subscriptions, Airwallex Billing.
4. **The “we expand to SG/ID/PH next year” conversation** — Xendit xenPlatform, 2C2P / Antom, Airwallex, Rapyd, HitPay 8-country API.
5. **The treasury conversation that is adjacent, not competing** — Aspire, Wise Business, Airwallex Business Accounts.
6. **The rail they already print on the counter** — DuitNow QR, PayNow, GrabPay, FPX, QRIS, PromptPay, QR Ph.

This file answers: **for each named platform, what is actually sold, how it is priced, how the API and webhooks work, what invoicing / subscriptions / tax look like, how multi-country works, and where Lazuar is the same job, a different job, or a missing job.**

### How the research was done

1. **Primary vendor pages fetched on 16 August 2026.** Pricing numbers below are only those printed on a vendor pricing page, official fee schedule, or official help/docs page on that date. If a number is not on that page, it is marked **unknown** or labelled as a secondary source.
2. **Docs over blogs.** Product capability (webhooks, invoice objects, subscription retries, platform split) is taken from vendor developer docs (`docs.xendit.co`, `docs.hitpayapp.com`, `docs.midtrans.com`, `docs.paymongo.com`, `docs.rapyd.net`, Airwallex pricing/docs, 2C2P product pages).
3. **Vendor blogs used only as context**, and labelled. HitPay’s own “best gateway in Malaysia 2026” and “best subscription software Philippines 2026” posts are marketing. They are useful for *what HitPay wants to be compared against*, not for market share.
4. **Lazuar ground truth from this workspace**, not from the landing-page diagram: ADR 019 / 021 / 023, `apps/lazuar-docs`, `Modules/Payments`, `Modules/Commerce`, `Modules/Lhdn`, `Modules/Billing`, `PaymentGatewayFactory` adapters.
5. **Tracker IDs** from `20-sequencing-and-tracker-schema.md` and `00-checklist-tracker.md` are reused when the job is the same (`PY-*`, `CP-*`, `SA-*`). New IDs in family `SEA-*` are minted here for **platform-vs-platform** jobs that the salon tracker does not already name (xenPlatform-style split, multi-country entity, treasury). Promotion rule: a `SEA-*` row only enters `00-checklist-tracker.md` if it is a job Aura or Lazuar will actually sell to a MY merchant. Traps stay `Never`.

### Pricing rule (do not invent)

- **Public** = printed on vendor pricing / fee schedule / help article fetched 16 Aug 2026.
- **Custom / sales** = vendor says “contact sales,” “volume discounts,” or hides the number.
- **Secondary** = a dated third-party blog. Used only to flag a *range*, never as a cell in a pricing table.
- **Unknown** = not found on a primary page today.

Taxes: most SEA processors quote **exclusive of VAT / GST / SST**. Xendit says local taxes apply and are deducted at settlement. PayMongo says “all prices exclusive of VAT.” Midtrans says all charges exclusive of VAT except QRIS / GoPay / ShopeePay (those rates include VAT). HitPay’s public MY/SG pages do not print a tax-inclusive/exclusive banner on every rate; treat as exclusive unless a contract says otherwise.

### What “vs Lazuar” means

Lazuar is **not in the same regulatory box** as Xendit / HitPay / Midtrans / PayMongo / 2C2P.

| Axis | Licensed SEA processor (Xendit, HitPay, Midtrans, PayMongo, 2C2P) | Lazuar Pay |
| --- | --- | --- |
| License | Local payments license / merchant acquirer (BNM, MAS, BI, BSP, etc.) | Software. Not an acquirer. |
| Money | Merchant money sits in **their** float, then payout | Merchant money never enters Lazuar. Processor account of the tenant. |
| KYC | They KYC the merchant | Tenant already has a Billplz / CHIP / Stripe account |
| Fee | MDR + sometimes software add-on (HitPay +0.2% on invoicing/POS) | SaaS + credit wallet. **0% GMV** by design (ADR 019 / 021) |
| Checkout UI | Hosted payment page they own | Hub portal + integrator front. Headless on purpose. |
| Tax filing | Almost none file LHDN MyInvois as a first-class product | LHDN is the **stated moat** (ADR 021). Shipping honesty: backend yes, full UX later. |

So “vs Lazuar” is **not** “who has a lower card MDR.” It is:

- **Displacement:** would a MY founder pick HitPay / Xendit Payment Links instead of Lazuar Commerce buy-links?
- **Complement:** would a MY founder plug Xendit or HitPay **into** Lazuar as a BYOK adapter (the README already names Xendit; the adapter is not built)?
- **Adjacency:** Aspire / Wise / Airwallex treasury are **where the money goes after** checkout, not a checkout competitor.
- **Rail:** DuitNow / PayNow / GrabPay are **methods inside** a processor, not products a salon “switches to.”

### Limitations

- There is no public census of “how many MY SMEs are on HitPay vs Xendit vs Billplz.” Installed-base claims on vendor homepages are vendor claims.
- Xendit, 2C2P, Airwallex, Rapyd enterprise rates are negotiated. Public cards are **list** rates.
- HitPay geo-redirects `hitpayapp.com/pricing` to the visitor’s market. MY and SG pages were fetched separately.
- Rapyd’s public marketing pages are thin on SEA-specific MDR. Rapyd is documented here as a **global FaaS** that SEA platforms evaluate, not as a MY SME default.
- Midtrans is **Indonesia-only** as a licensed product. It appears in this file because Indonesian marketplaces and GoTo-adjacent merchants name it, and because Aura/Lazuar expansion-to-ID conversations will hit it immediately.
- 2C2P’s public site does not print a self-serve MDR table. Pricing is sales-led. Capability is taken from the payment-method matrix (400+ methods, 600,000+ OTC locations — vendor claim).

---

## Regional map

### How a SEA merchant assembles payments in 2026

Southeast Asia is not “Stripe + cards.” It is a **stack of national rails** with a thin software layer on top.

```
BUYER PAYS WITH
  cards (Visa/MC/JCB/Amex/UnionPay)     — everywhere, expensive, chargebacks
  national bank rail                    — FPX (MY), PayNow (SG), BI-FAST/VA (ID),
                                          InstaPay/PesoNet (PH), PromptPay (TH)
  national QR                           — DuitNow QR (MY), PayNow QR (SG), QRIS (ID),
                                          QR Ph (PH), PromptPay QR (TH), VietQR (VN)
  e-wallets                             — TnG, GrabPay, Boost, ShopeePay, GCash,
                                          Maya, GoPay, OVO, DANA, TrueMoney, ZaloPay
  OTC / cash-in                         — 7-Eleven, Alfamart, Cebuana, Indomaret
  BNPL / instalments                    — Atome, BillEase, Kredivo, Akulaku, bank IPP

SOFTWARE LAYER THE MERCHANT NAMES
  HitPay          — SG-born SMB suite: link + invoice + POS + recurring + store
  Xendit          — ID-born, now SEA+MX: API + Payment Links (xenInvoice lineage)
                    + Subscriptions + xenPlatform + payouts
  Midtrans        — ID only, GoTo: Snap + Core API + Payment Link + Subscription
  PayMongo        — PH only: API + links + subscriptions + wallet/ledger (new)
  2C2P / Antom    — TH-born, Ant International: 400+ methods, travel/enterprise
  Airwallex       — global treasury + collect + billing + embedded finance
  Rapyd           — global Collect / Disburse / Wallet / Issue
  Billplz / CHIP / Fiuu / iPay88 / Revenue Monster
                  — MY-local pipes (out of this file except as Lazuar adapters)

TREASURY (adjacent)
  Aspire          — SG multi-currency operating account + cards + invoices
  Wise Business   — multi-currency receive + FX
  Airwallex BA    — same job, heavier platform

RAILS (not products)
  DuitNow / PayNow / GrabPay / FPX / QRIS / PromptPay / QR Ph
```

### Country-by-country: who a merchant names

| Country | Default consumer rails | Software / processor they name | Licensed note (vendor-claimed, 16 Aug 2026) | Lazuar posture today |
| --- | --- | --- | --- | --- |
| **Malaysia** | FPX, DuitNow QR, TnG, GrabPay, Boost, ShopeePay, cards | Billplz, CHIP, Fiuu, iPay88, Revenue Monster, **HitPay**, **Xendit**, 2C2P (enterprise), Airwallex (cross-border) | Xendit via **Payex PLT** (BNM merchant acquirer, non-bank). HitPay: BNM **approved MSB agent** + **registered merchant acquirer** (footer links to BNM directories). | Home market. BYOK to Billplz/CHIP/Stripe. LHDN is the local tax job nobody else in this file owns. |
| **Singapore** | PayNow, cards, GrabPay, PayLah | **HitPay**, Stripe, Airwallex, Aspire, 2C2P, Xendit | HitPay MAS-licensed (vendor: “MAS-licensed payment platform”). Airwallex Singapore MPI PS20200541. Aspire PSI. Wise MPI. | Not a shipping market. A MY merchant with SG customers uses HitPay cross-border PayNow or Xendit SG, or Stripe. |
| **Indonesia** | QRIS, VA (BCA/BNI/BRI/Mandiri…), GoPay, OVO, DANA, ShopeePay, cards, Alfamart/Indomaret | **Midtrans (GoTo)**, **Xendit**, Doku, Midtrans Payment Link, GoPay Mini App | Midtrans: licensed by Bank Indonesia. Xendit: BI payment-system license (site footer). | No ID adapter. README names Xendit; adapter missing. Coretax is a Phase-1 ambition in README, not a module. |
| **Philippines** | GCash, Maya, QR Ph, cards, OTC (7-Eleven, Cebuana, Palawan), InstaPay | **PayMongo**, **Xendit**, Dragonpay, 2C2P | PayMongo: BSP-regulated (vendor). Xendit PH live. | No PH adapter. PayMongo is the local “HitPay of Manila.” |
| **Thailand** | PromptPay, cards, TrueMoney, LINE Pay, bank IPP | **2C2P**, Omise, Xendit, GB Prime Pay | 2C2P founded Bangkok 2003; still the TH enterprise default. | No TH adapter. PromptPay is a rail, not a product. |
| **Vietnam** | VietQR, ZaloPay, cards, bank transfer | Xendit, 2C2P, OnePay, Payoo | Xendit VN site live. | Out of scope until ID/SG exist. |
| **Hong Kong** | FPS, cards, AlipayHK, WeChat Pay | Airwallex, Xendit HK, Stripe, 2C2P | Xendit HK pricing public. | Treasury / collect, not CaaS. |

### What “closest rival” means (read this before the dossiers)

Two products can be close on a screenshot and far apart as a company.

| Rival | Why a merchant says the name | Why they are / are not Lazuar |
| --- | --- | --- |
| **HitPay** | “I send a WhatsApp payment link and an invoice. I also have a POS and recurring.” No monthly fee. MY expansion is real (DuitNow, FPX, TnG, terminals, BNM listings). | **Closest productized checkout + invoices + subscriptions + POS** for SMBs. They **are** the acquirer. Lazuar is software on top of someone else’s acquire. HitPay wins the non-technical founder. Lazuar wins only if LHDN + BYOK + headless + ledger are the actual pain. |
| **Xendit** | “I need one API for ID + PH + MY + TH + VN + SG, payment links, subscriptions, and sub-accounts.” xenInvoice is the historical name for hosted invoices / payment links; the current public product is **Payment Links** plus a **legacy `/v2/invoices` API** that Xendit is migrating to Payment Sessions. | **Closest developer checkout + invoices + subscriptions + platform.** They **are** the acquirer **and** the payout network **and** xenPlatform. Lazuar’s README already treats Xendit as a *BYOK gateway to plug in*, which is the correct relationship — except the adapter is not built. Competing with Xendit’s hosted Payment Links as a MY SMB product is a losing fight. Competing as **compliance + LHDN sitting on top of Xendit** is the intended shape. |
| **Midtrans** | “We are an Indonesian website. We use Snap.” | Not a MY rival. The ID expansion rival. Snap is the hosted UX; Core API is the custom UX; Subscription API is cards/GoPay. No LHDN, no MY rails. |
| **PayMongo** | “We are a PH startup. We use PayMongo API.” | PH analogue of HitPay + a slice of Stripe. Not in MY. Pattern library for subscriptions + payment links + (new) wallet/ledger. |
| **2C2P / Antom** | “We are AirAsia / Lazada / a hotel / an airline.” | Enterprise acquiring + 400 methods + IPP. Not an SMB invoice product. Sales-led. |
| **Airwallex** | “We collect globally, hold multi-currency, pay suppliers, issue cards, and now we invoice/subscribe.” | Overlaps Lazuar on **billing/invoices/subscriptions** and overlaps Aspire on **treasury**. Heavier, more expensive on collect MDR than local processors. No LHDN. |
| **Rapyd** | “We need 100+ countries Collect + Disburse + wallets from one API.” | Global FaaS. Rarely the MY SMB default. Relevant if Lazuar ever embeds payouts (ADR 019 Phase 3 Wise MassPay). |
| **Aspire / Wise** | “We need a multi-currency operating account.” | **Not checkout.** They sit **after** Lazuar. Aspire even invoices natively — a soft overlap. |
| **GrabPay / PayNow / DuitNow QR** | “Can my customer pay with the app they already have?” | **Rails.** Lazuar must expose them **through** Billplz/CHIP/HitPay/Xendit, not rebuild them. |

### Competitive topology (one picture)

```
                    ┌─────────────────────────────────────┐
                    │     GOVERNMENT TAX / E-INVOICE      │
                    │   LHDN MyInvois · (GSTN) · Coretax  │
                    │         Lazuar owns this job        │
                    │     HitPay/Xendit/Airwallex: no     │
                    └──────────────────▲──────────────────┘
                                       │ XML / QR / UUID
┌──────────────┐   BYOK keys    ┌──────┴───────┐   hosted UI   ┌──────────────┐
│ Integrator   │───────────────▶│  LAZUAR HUB  │◀─────────────▶│ Buyer        │
│ Aura / SaaS  │  payment.*     │  cashier +   │  portal       │ (guest)      │
│ / Framer     │  subscription.*│  commerce +  │               └──────▲───────┘
└──────────────┘                │  ledger +    │                      │
                                │  dunning     │                      │
                                └──────┬───────┘                      │
                                       │ adapter                      │
                    ┌──────────────────┼──────────────────┐           │
                    ▼                  ▼                  ▼           │
              Billplz/CHIP         Stripe            (Xendit          │
              FPX/DuitNow          cards             not built)       │
                    │                  │                  │           │
                    └──────────────────┴──────────────────┘           │
                                       │                              │
                    ALTERNATIVE: merchant never uses Lazuar           │
                    and sends buyer straight to ──────────────────────┘
                         HitPay link / Xendit invoice / Midtrans Snap
```

The **displacement fight** is the bottom path. The **complement fight** is “Lazuar as the compliance + subscription + ledger brain, processor as the pipe.” ADR 019/021 already chose complement. The product risk is that HitPay and Xendit have made the **pipe look like a product** (invoices, subscriptions, POS, payment links), so the merchant never feels the missing brain.

---

## Dossiers

Each dossier uses the same fields. “Why a merchant would pick them over Lazuar” is written from the merchant’s chair.

---

### 1. Xendit (Indonesia-born, regional; invoices / xenInvoice lineage, xenPlatform, subscriptions)

#### Identity

- **Legal / brand:** Xendit Inc. / local licensed entities. Public site copyright 2026 Xendit Inc.
- **Origin:** Indonesia. Now markets: **Indonesia, Philippines, Malaysia, Thailand, Vietnam, Singapore, Hong Kong, Mexico**.
- **MY license (vendor FAQ, en-my, 16 Aug 2026):** Licensed by Bank Negara Malaysia under **Payex PLT**. Vendor instructs merchants to find “Payex PLT” on BNM’s list of regulatees under Merchant Acquiring Services > Non-banks. PCI-DSS claimed.
- **Positioning:** “Financial infrastructure that transforms Southeast Asia / Latin America.” Accept + send + platform + financing + treasury + cards + BaaS. This is a **full-stack payments company**, not a checkout widget.
- **URLs:** [https://www.xendit.co/en/](https://www.xendit.co/en/) · [https://www.xendit.co/en-my/](https://www.xendit.co/en-my/) · [https://www.xendit.co/en/pricing/](https://www.xendit.co/en/pricing/) · [https://docs.xendit.co/](https://docs.xendit.co/) · [https://docs.xendit.co/docs/xenplatform-overview](https://docs.xendit.co/docs/xenplatform-overview) · [https://docs.xendit.co/recurring](https://docs.xendit.co/recurring)

#### What merchants actually buy

A typical Xendit merchant is not buying “a payment link.” They are buying **one contract that covers**:

1. **Accept** — 100+ methods (vendor). In MY: FPX, DuitNow Pay, DuitNow-related VA, cards, TnG, GrabPay, ShopeePay, Alipay/Alipay+, WeChat Pay, SPayLater, Grab PayLater, virtual accounts.
2. **Payment Links** — no-code or API-created hosted checkout. Historical product name **xenInvoice**. Dashboard + WhatsApp/Viber share + QR. Branded page, reminders, PCI offload.
3. **Subscriptions** — scheduled auto-debit on cards, e-wallets, direct debit. Custom interval, retries, up to 5 linked payment accounts, notifications. Dashboard or API. Requires a channel that supports **Merchant-Initiated Transactions**.
4. **xenPlatform** — master account + sub-accounts. Accept on behalf of merchants, split fees, transfers between accounts, payouts. Recipes for PSPs, SaaS, POS platforms, conglomerates.
5. **Payouts** — API and Excel batch. Public list rate (16 Aug 2026): **1.00% with a per-market minimum** + Xendit processing fee (e.g. MY: 1% min MYR 1.50 + MYR 0.90 processing).
6. **Adjacencies** — early settlement, xenCapital financing, corporate cards, expense, global treasury (17 currencies — vendor), identity, BaaS / e-money-as-a-service, in-person POS terminals.

xenInvoice as a **standalone brand** is mostly archived. Chinese locale still routes `/zh/products/xeninvoice/`. English product IA says **Payment Links**. The **legacy Invoice API** is `POST /v2/invoices` (create), `GET /v2/invoices/{id}`, `GET /v2/invoices`, expire. Xendit’s own docs (updated Jul 2026) tell merchants to **migrate legacy Payment Links / Invoices to Payment Sessions** (`POST /sessions`). Webhook names change: “invoice paid / invoice expired” → session-lifecycle + Payment / Payment Token webhooks. **Do not document xenInvoice as a current SKU.** Document it as the lineage of Payment Links + `/v2/invoices`.

#### Invoicing (the job merchants mean by xenInvoice)

Two different objects get called “invoice” at Xendit. Do not collapse them.

| Object | What it is | Tax? | vs Lazuar |
| --- | --- | --- | --- |
| **Payment Link / Invoice API (`/v2/invoices`)** | A **payable** hosted page. Amount, expiry, payment methods, customer, items, redirect URL. When paid, webhook. This is a **checkout**, not a statutory tax invoice. | No MyInvois. No UBL 2.1. Optional item lines for display. | Same job as Lazuar **Payments cashier** + **Commerce buy link**. Xendit is the processor *and* the page. Lazuar is the page *on top of* a processor. |
| **Commercial invoice / PDF** | Dashboard can send a branded request. Still not LHDN. | No. | Lazuar LHDN is a **different document** (UBL XML + UUID + QR). |

Xendit Payment Links features (product page, 16 Aug 2026):

- Single-use or multiple-use.
- Share via SMS, email, WhatsApp, Viber.
- QR codes for national QR (QRIS, QRPH, PromptPay, VietQR — vendor).
- Automated reminders for upcoming / overdue.
- Instant paid notification.
- Logo, colours, personalized message.
- API automation or dashboard no-code.
- Methods on the same page: QR, e-wallets, VA, direct debit, cards, OTC, PayLater.

That is a **finished SMB billing UX**. Lazuar Commerce public buy links are the comparable surface. Lazuar Payments cashier is the comparable *integrator* surface. Lazuar does **not** today ship a no-code “type amount, WhatsApp the link, remind them, expire it” dashboard that a non-technical founder finishes in five minutes without reading TypeSpec.

#### Subscriptions

Xendit Subscriptions (product + docs, Jul 2026):

- Cycles: daily / weekly / monthly / yearly; custom scheduler; anchor date; number of occurrences.
- Methods: **cards + e-wallets + direct debit** (the “first in dynamic markets” claim). This is the real differentiator vs Stripe-only recurring.
- Link up to **5 payment accounts** per subscriber; retry across them.
- Failed-payment recovery: retries, optional **payment link fallback**, optional deactivate plan.
- Notifications to end user.
- Two API entry points: (1) Payment Session `type = SUBSCRIPTION` → hosted UI tokenizes → plan activates; (2) existing `payment_token_id` → create plan immediately.
- Dashboard create flow exists (no-code).
- Webhooks: Payment Session completion, Payment Token, Subscription Plan, plus per-cycle payment events.
- Use cases they sell: streaming, donations, insurance, fintech repayments, utilities, telco usage-based.

**Pricing of the Subscriptions product itself:** Xendit’s FAQ says subscriptions “may incur separate charges” on top of payment-method fees. The public pricing table fetched 16 Aug 2026 is **method MDR + processing fee only**. The incremental subscription SKU price is **unknown / sales**. Do not invent a “Xendit subscriptions cost X%.”

**vs Lazuar Commerce subscriptions:**

| Job | Xendit | Lazuar |
| --- | --- | --- |
| Plan + interval | Yes (dashboard + API) | Yes (Commerce) |
| Off-session charge on local wallets / FPX direct debit | **Yes** (MIT channels) | **Depends on adapter.** Billplz: **no** card vault / off-session. CHIP: off-session charge exists in adapter. Stripe: yes. Xendit adapter: **not built**. |
| Dunning | Retries + payment-link fallback + notifications | Email dunning templates exist. WhatsApp dunning is roadmap. |
| Customer portal | Xendit-hosted linking + their UI | `lazuar-portal` magic link. CHIP explicitly has no managed portal (`InvalidOperationException` in adapter). |
| Ledger + tax on each cycle | Settlement report | Double-entry + (intended) LHDN B2C consolidation on the 28th (ADR 021 Pillar 1) |
| Who holds the token | Xendit | Processor (Stripe/CHIP), not Hub, under BYOK |

Lazuar cannot honestly sell “FPX auto-debit subscriptions” on Billplz. That job belongs to Xendit / HitPay GIRO (SG) / CHIP / a future FPX-DD adapter.

#### xenPlatform

Docs (updated 9 Jul 2026):

- **Master account** creates **sub-accounts** (merchants, partners, branches, drivers).
- Each sub-account: own balance, own transaction history, own Account ID (`user-id` / `owner_id` / `business_id` synonyms).
- Flows: create sub-account → accept on their behalf (own page or Xendit hosted) → **Split Fees** for commission → payout or let them withdraw → **Transfers** between accounts.
- Recipes:
  - **PSP** (Helixpay, Tazapay): own payment page, split fees, payout or merchant withdraw.
  - **SaaS** (TADA, Qoala): API onboard, hosted checkout, split fees, disburse.
  - **POS platforms** (ESB, Raptor): invite merchants, they manage own payments.
  - **Conglomerates** (XL, Ciputra): branches operate own dashboard; HQ transfers balances up.

This is **Stripe Connect for SEA**. Lazuar’s equivalent is **not** xenPlatform. Lazuar’s equivalent is:

- **One workspace per tenant** (Aura org ↔ Hub workspace).
- **BYOK** so Aura never holds guest GMV.
- **Provision** (`external_product`, `external_org_id`) mints a machine key.

Lazuar **must not** grow a “take 2% and split to salons” xenPlatform. That is tracker trap `PY-022` / `XX-003`. The honest platform play is: **Aura (or any SaaS) uses Lazuar as the cashier; each tenant brings their own Xendit/Billplz keys; if the SaaS itself is a marketplace, they should be a Xendit xenPlatform customer, not a Lazuar-as-acquirer customer.**

#### API and webhooks

- **Style:** REST, resource URLs, HTTP Basic (API key as username, empty password), `https://api.xendit.co`.
- **Generations in flight (2026):**
  - Legacy Invoice / Payment Link: `/v2/invoices`.
  - Unified Payments: Payment Requests, Payment Tokens, Payment Sessions (`POST /sessions`).
  - Recurring / Subscriptions: plan + token + session type `SUBSCRIPTION`.
  - xenPlatform: `for-user-id` header (historical) / Account ID on requests.
  - Payouts, Customers, Refunds, Transactions, Balance.
- **Auth:** secret API key. Test vs live keys.
- **Webhooks:** dashboard or `POST /callback_urls/:type`. Types include `invoice` (paid/expired), `payment_method_v2`, `direct_debit`, `ewallet`, `fva_paid`, regional OTC paid, refunds, etc. Verification token in dashboard. xenPlatform can set per-sub-account callback URLs.
- **Idempotency:** Xendit supports idempotency keys on mutating calls (standard for their stack; treat as table-stakes).
- **SDKs:** official Node, PHP, Python, Go, Java historically. New docs site at `docs.xendit.co` with `llms.txt`.
- **Plugins (MY FAQ):** Shopify, WooCommerce, WooCommerce Subscriptions, Magento 2, OpenCart 3, EasyStore, Ecwid, plus regional builders.

Lazuar’s comparable contract is **narrower and cleaner on purpose**: `POST /api/v1/integrations/payments/checkouts` → `payment.completed` / `payment.failed` / (maturing) `payment.refunded`. Commerce has `order.completed`, `subscription.*`, `payment_link.paid`. LHDN has `invoice.valid` / `invoice.invalid`. **Do not collapse these.** Xendit’s sprawl is what Lazuar exists to hide — if the adapter exists.

#### Pricing posture (public list, 16 Aug 2026)

Model printed on [xendit.co/en/pricing](https://www.xendit.co/en/pricing/): **fixed processing fee + payment-method fee (or payout fee)** per successful transaction. Sign-up free. Volume discounts via sales. Fees exclusive of VAT/GST/SST; taxes deducted at settlement. Chargeback: **USD 25** admin. Shopify partner fee: **+0.5%** on top of method fee.

**Malaysia (selected, public):**

| Method | Payment-method fee | Xendit processing |
| --- | --- | --- |
| Domestic debit cards Visa/MC | 1.90% | MYR 0.90 |
| Domestic credit cards Visa/MC | 2.00% | MYR 0.90 |
| International cards Visa/MC | 3.80% | MYR 0.90 |
| FPX Direct Debit (Personal) | MYR 1.20 | MYR 0.90 |
| FPX Direct Debit (Corporate) | MYR 2.00 | MYR 0.90 |
| DuitNow Pay Online Banking | MYR 2.00 | MYR 0.90 |
| Touch ’n Go local | 1.80% | MYR 0.90 |
| Touch ’n Go foreign | 2.50% | MYR 0.90 |
| GrabPay MY | 2.00% | MYR 0.90 |
| ShopeePay MY | 2.50% | MYR 0.90 |
| Alipay | 2.50% | MYR 0.90 |
| Alipay+ | 3.00% | MYR 0.90 |
| WeChat Pay MY | 2.50% | MYR 0.90 |
| SPayLater | 2.5% | MYR 0.90 |
| Grab PayLater postpaid | 6.00% | MYR 0.90 |
| Grab PayLater 4-month | 8.00% | MYR 0.90 |
| Virtual Accounts MY | 0.50% min MYR 1.00 | MYR 0.90 |
| Payouts MY banks/e-wallets | 1.00% min MYR 1.50 | MYR 0.90 |

**Indonesia (selected):** VA / Alfamart-style OTC often **IDR 9,000 + IDR 4,000** processing; cards **2.90% + IDR 2,000 + IDR 4,000**; QRIS **0.70% incl. VAT + IDR 4,000**; GoPay non-digital **3.00%** / digital **5.00%**; OVO digital **5.50%**.

**Singapore cards:** domestic 3.30% + SGD 0.50 + SGD 0.30 processing; international 3.80% + SGD 0.50 + SGD 0.30. **PayNow QR:** 1.30% + SGD 0.30.

**Philippines cards:** domestic 3.50% + PHP 11.00; international 4.50% + PHP 10.00 + PHP 11.00. GCash e-wallet 3.00% / auto-debit 3.20% + PHP 11.00. QRPH 1.50% min PHP 15 + PHP 11.00.

**Refunds:** “A Xendit processing fee applies to all transactions” (FAQ). Do not assume refunds are free.

#### Tax

- Xendit does **not** submit LHDN MyInvois, Indonesia Coretax e-Faktur as a merchant-of-record tax engine, or PH BIR CAS on behalf of the merchant as a first-class product on the pages fetched.
- They collect **their own** VAT/GST/SST on fees.
- Multi-currency cards: extra FX on conversion (docs: card multi-currency processing).
- ADR 021’s entire reason to exist is this gap.

#### Multi-country

This is Xendit’s core sales motion: one integration, many licensed entities, local methods. A Singapore HQ selling into MY/ID/PH can (with KYC per entity / xenPlatform structure) accept local rails without signing six gateway contracts. **Mexico** is the extra-SEA beachhead.

Operational reality: it is still **one licensed entity per market** under the hood. xenPlatform is how a SaaS hides that from sub-merchants. Cross-border payouts add FX. Do not tell a MY SDM “Xendit is one legal person for all of ASEAN.”

#### Why a merchant would pick Xendit over Lazuar

- They need **ID + PH + MY** next quarter, not LHDN this month.
- They need **auto-debit on e-wallets**, not a Billplz redirect every cycle.
- They are a **marketplace / SaaS** and xenPlatform is the product.
- They want **payouts** (affiliates, drivers, sellers) in the same vendor.
- They want a **no-code payment link** this afternoon with WhatsApp share.
- They already have a WooCommerce / Shopify shop and a plugin.
- They trust a licensed acquirer more than “paste your Billplz secret into a Hub vault.”

#### Why they would pick Lazuar over Xendit

- They **already have** Billplz/CHIP/Stripe and refuse a second acquire + second KYC + second float.
- They are **SST + LHDN** constrained. Xendit will not file the XML.
- They want **0% GMV software** and will pay a SaaS + credits instead of 0.90 MYR + method fee *and* a software layer.
- They are a **headless** product (Aura, a custom SaaS) and want one normalized `payment.*` contract across processors.
- They need a **double-entry ledger** that isolates gateway fee vs tax vs net cash — Xendit gives a settlement report, not a general ledger.

#### vs Lazuar — verdict

**Complement first, rival second.** Build the **Xendit adapter** (README already promised it). Do **not** rebuild xenPlatform. Do **not** compete with Payment Links on “time-to-first-WhatsApp-link.” Compete on **compliance at the point of sale** and **subscription state machine + dunning that is processor-agnostic**. If Lazuar’s Commerce buy-link is slower to create than Xendit’s dashboard invoice, non-technical MY founders will never start.

---

### 2. Midtrans (GoTo; Snap, Core API, subscriptions)

#### Identity

- **Legal:** PT Midtrans. Site footer: “a Gojek / GoTo Financial company” (`gotofinancial.com`). Copyright 2026 PT Midtrans.
- **Market:** **Indonesia only.** Not a MY or SG processor.
- **License (vendor):** Bank Indonesia; Kominfo registered electronic-system provider; PCI-DSS; ISO 27001; AES-256.
- **URLs:** [https://midtrans.com/en](https://midtrans.com/en) · [https://midtrans.com/pricing](https://midtrans.com/pricing) · [https://docs.midtrans.com/](https://docs.midtrans.com/) · [https://midtrans.com/features/recurring-payment](https://midtrans.com/features/recurring-payment) · [https://midtrans.com/product/payment-link](https://midtrans.com/product/payment-link)

#### What merchants actually buy

Three integration products, plus a no-code Payment Link, plus GoPay Mini App, plus promo management, plus Iris payouts (historically separate docs).

1. **Snap** — hosted / embedded payment UI. “No monthly fee.” Small business start; large business customize. Token-based. Mobile-friendly popup, historically in-page without full redirect (product copy still says this; implementation has evolved). This is the default ID checkout.
2. **Core API (VT-Direct)** — merchant builds the UI; Midtrans charges. Now also a **BI-SNAP** (Standar Nasional Open API Pembayaran) generation:
   - Legacy: `api.sandbox.midtrans.com` / `api.midtrans.com`
   - BI-SNAP: `merchants-app.sbx.midtrans.com` + `merchants.sbx.midtrans.com` (sandbox); production `merchants-app.midtrans.com` / `merchants.midtrans.com`
   - Bank Indonesia is pushing merchants onto BI-SNAP. A 2026 ID expansion cannot ignore this.
3. **Subscription / Recurring** — Snap tokenization **or** Core API one-click / two-click **or** Subscription API `POST /v1/subscriptions` (`api.sandbox.midtrans.com/v1/subscriptions`). Fixed amount, custom interval, retries. One-click stores CVV-in-token; two-click re-asks CVV. GoPay + cards.
4. **Payment Link** — no-code “terima pembayaran dengan mudah.” Same family as Xendit Payment Links / HitPay links.
5. **GoPay Mini App** — stay inside Gojek/GoPay. Super-app distribution, not a web checkout.
6. **Fraud:** Aegis (AI + rules).

#### Invoicing

Midtrans Payment Link is a **payable request**, not a tax invoice and not Indonesia e-Faktur / Coretax. Itemization exists for display. No statutory XML product on the pages fetched.

#### Subscriptions

Product page (2026):

- Save payment info for returning customers (one-click / two-click).
- Automatic subscription charges; set interval; future start date.
- Retry on failure.
- API: Recurring API / Subscription API (`https://api-docs.midtrans.com/#recurring-api`).
- Methods emphasized: **GoPay + credit/debit cards**. Not the full VA/OTC set (those are customer-present).
- PCI + tokenization so the merchant never stores PAN.

This is **narrower than Xendit** (no e-wallet + DD matrix across SEA) but **deeper inside GoTo** (GoPay token + Mini App).

#### API and webhooks

- Snap: create transaction → `token` → snap.js.
- Core API: charge, status, cancel, refund, expire, capture.
- HTTP Basic (server key).
- Notifications: HTTP POST to merchant `notification_url` (and finish/unfinish/error redirects). Signature key verification (`sha512` of order_id + status_code + gross_amount + server key — the long-standing Midtrans scheme).
- Status polling is first-class; ID merchants are trained to **never trust the redirect**.
- Subscription API: create / get / disable / enable.
- BI-SNAP: different hosts, OAuth-style Get Auth Code, national standard payloads.

Lazuar’s hop-A / hop-B discipline (redirect is UX, signed webhook is fulfillment) is the **same religion** as Midtrans notification URLs. An ID adapter would map Midtrans notification → `payment.completed` / `payment.failed`.

#### Pricing posture (public, midtrans.com/pricing, 16 Aug 2026)

No setup, no monthly, no Core API / website integration fee. Charge on **successful** transactions. Exclusive of VAT except QRIS, GoPay, ShopeePay (those quoted rates include VAT per the page).

| Method | Public rate |
| --- | --- |
| Bank transfer / VA (BCA, BRI, BNI, Mandiri, Permata, CIMB, Danamon, BSI, SeaBank, Bank Saqu, …) | **IDR 4,000** / success |
| GoPay | **2%** (gaming & digital different) |
| QRIS | **0.7%** (gaming & digital different) |
| ShopeePay | **2%** on one table; Indonesian long-form on the same page also says **1.5%** — **do not flatten; treat as verify-on-contract**. The visual EN table showed 2%; the ID essay showed 1.5%. |
| DANA | **1.5%** |
| OVO | **1.5%** |
| Cards Visa/MC/JCB/Amex/UnionPay | **2.9% + IDR 2,000** |
| Indomaret | Direct to partner + **IDR 1,000** |
| Alfamart / Alfamidi / DAN+DAN | **IDR 5,000** |
| Akulaku PayLater | **1.7%** |
| Kredivo | **2%** |
| Direct debit (essay): Octo / e-Pay BRI / Danamon | **IDR 5,000** (VAT-inclusive per essay) |
| BCA KlikPay (essay) | **IDR 2,200 + BCA fee** |
| Payout to bank | **IDR 5,000** |
| Payout to GoPay | **IDR 1,000** (essay) / visual “withdrawal” tab also showed IDR 5,000 to all banks |

MDR is deducted from the Midtrans balance; withdrawal is net.

**vs Xendit ID:** same VA ballpark (Xendit IDR 9,000 + 4,000 processing is **higher** than Midtrans IDR 4,000 on VA). QRIS both ~0.7% (BI-regulated). Cards similar 2.9% + 2,000. Xendit adds a **processing fee on every charge** (IDR 4,000). Midtrans usually does not split “scheme + Xendit fee.”

#### Tax

No Coretax product. VAT on Midtrans fees. QRIS/GoPay/ShopeePay special VAT-inclusive quoting.

#### Multi-country

**None.** A MY company expanding to Jakarta either: (a) opens a PT and a Midtrans account, or (b) uses Xendit / 2C2P as the regional layer. Midtrans will not take FPX.

#### Why a merchant would pick Midtrans over Lazuar

- They are **Indonesian**. Midtrans is the default, GoPay is the wallet, Snap is in every tutorial.
- They want **GoPay Mini App** distribution.
- They want BI-SNAP compliance from the market leader.
- They do not care about LHDN.

#### Why they would pick Lazuar

- They would not, as a **processor**. They would pick Lazuar as a **headless subscription + ledger + (future) Coretax** layer **on top of** Midtrans — only if Lazuar builds a Midtrans adapter and an Indonesia tax product. Neither exists.

#### vs Lazuar — verdict

**ID expansion adapter, not a MY rival.** Pattern to steal: Snap’s “token then popup” UX; notification-URL religion; Subscription API shape; GoPay token. Pattern to refuse: becoming a GoTo-only company. If Aura/Lazuar ever sell into Jakarta salons, the **named** processor will be Midtrans or Xendit, not Billplz.

---

### 3. HitPay (Singapore; payment links, invoicing, POS, subscriptions, MY expansion)

**This is the closest “productized checkout + invoices” rival in SEA. Treat it as the primary SMB displacement threat.**

#### Identity

- **Brand:** HitPay. Dashboard `dashboard.hit-pay.com`. Checkout hosts `securecheckout.hit-pay.com` / `securepayment.hit-pay.com`.
- **Origin:** Singapore. Markets on API docs (16 Aug 2026): **Singapore, Malaysia, Philippines, Indonesia, Thailand, Hong Kong, Australia, New Zealand** — “8 countries in Asia-Pacific.”
- **License (vendor, site footer + blog):**
  - Singapore: MAS-licensed payment platform (vendor copy). Footer points at MAS FID.
  - Malaysia: footer links BNM **approved money service business agent** directory and **registered merchant acquirer** list.
- **Positioning:** Zero monthly fee, zero setup, pay-per-success. One account for **online + payment links + invoicing + recurring + online store + POS + terminals + Tap to Pay + remittance**. SMB-first, WhatsApp-native, vertical pages for F&B, fitness, wellness/salon, education, retail, travel, nonprofit.
- **URLs:** [https://hitpayapp.com/pricing](https://hitpayapp.com/pricing) · [https://hitpayapp.com/my/pricing](https://hitpayapp.com/my/pricing) · [https://hitpayapp.com/sg/pricing](https://hitpayapp.com/sg/pricing) · [https://hitpayapp.com/invoicing](https://hitpayapp.com/invoicing) · [https://hitpayapp.com/payment-links](https://hitpayapp.com/payment-links) · [https://hitpayapp.com/recurring-billing](https://hitpayapp.com/recurring-billing) · [https://docs.hitpayapp.com/apis/overview](https://docs.hitpayapp.com/apis/overview) · [https://docs.hitpayapp.com/apis/guide/events](https://docs.hitpayapp.com/apis/guide/events)

#### What merchants actually buy

HitPay is a **suite**, not a gateway SKU. The same KYC’d account unlocks:

1. **Payment Links** — create in dashboard or API. Fixed or open amount. Single or repeating. Branding. 100+ currencies (vendor). Method picker reorderable by amount/currency. Share on WhatsApp, IG, email, iMessage. QR. Partial payments claimed on the invoicing FAQ (and payment-link FAQ points at invoices for partials).
2. **Invoicing** — professional invoice (number, due date, line items, tax line, discount, footer, logo). Email + reminders (e.g. 3 days before due). Repeating invoices. Partial payments / deposits. Mark as paid (cash). Multi-currency. Mobile app create. Accounting sync. Invoice link + QR. **This is a commercial invoice, not LHDN e-Invoice.** GST 9% appears in their SG demo screenshot. MY page does not claim MyInvois.
3. **Recurring Billing** — plans (daily / weekly / monthly / yearly / custom). Public plan pages / shareable recurring links. Customer portal (view, update method). Pause / cancel. Email templates. Failed-payment retries. Analytics. SG **GIRO** for recurring (FAQ). MY recurring methods on pricing: cards, ShopeePay 2.2%, GrabPay 2%, TnG 1.9%.
4. **Online Store** — no-code storefront. Extra **0.2%** on top of MDR (MY and SG pricing pages).
5. **POS + terminals + Tap to Pay** — phone NFC free; WisePad 3 MYR 310 / SGD 85; All-in-One MYR 850 / SGD 680; FlexiPOS MYR 1,750 / SGD 500; SG SoundBox SGD 30; POS MAX SGD 700. In-person domestic cards MY **1.4% min RM 0.30** (FAQ).
6. **Static QR / Borderless QR** — tourist pays with home QR (PayNow, PromptPay, etc.); merchant settles in home currency; **1% FX markup** at settlement (printed).
7. **Plugins:** Shopify, WooCommerce, Xero, Wix. Extra plugin fee referenced on pricing (amount not fully extracted from the JS tables; treat **unknown** except the **0.2%** business-software add-on).
8. **Platform APIs** — sub-accounts, platform key + business key, webhook routing to platform for `charge.*`. Reseller / refer-a-merchant programs.
9. **FX and Payouts / Remittance API** — new product in IA.
10. **MCP Server** via Zapier (docs nav) — 2026 DX fashion; not a merchant job.

Vertical marketing explicitly includes **Health, Beauty, and Spa**. Aura will lose deals to “we already take deposits on HitPay WhatsApp links” long before it loses deals to Fresha Pay.

#### Invoicing (deep)

HitPay invoice object (webhook example `invoice.created` / `invoice.updated` on docs):

- `invoice_number` (e.g. `INV-CJHA-20250406`), `reference`, `due_date`, `invoice_date`
- `amount`, `balance_amount`, `amount_no_tax`, `subtotal`
- `tax_setting` (nullable in sample)
- `allow_partial_payments`
- `invoice_link` (short link)
- `payment_methods` array (card, paynow_online, paynow_transfer, grabpay_direct, grabpay_paylater, WeChat/Alipay via qfpay, shopback, upi_qr, …)
- `payment_requests[]` nested when the buyer pays
- `charges[]` with `fixed_fee`, `discount_fee`, `discount_fee_rate`, `all_inclusive_fee`, `payment_provider` (often `stripe_sg` under the hood in the sandbox sample)
- statuses: `sent` → `paid`; void fields (`void_reason`, `voided_at`)
- late-fee fields exist (`late_fee_type`, percentage, fixed, grace) — productized AR, not just a pay link
- `channel`: `dashboard` in the sample

This is **years ahead** of Lazuar as an AR product. Lazuar LHDN is a **statutory document**. HitPay is a **get-paid document**. Malaysian SMEs need **both**. HitPay currently ships the one they feel daily (get paid). Lazuar ships (backend) the one they fear (LHDN). The winning product is **get-paid invoice that also files MyInvois**. Neither party fully has that on 16 Aug 2026.

HitPay pricing for the invoicing *software*: **+0.2%** on top of processing (MY and SG pages). Not a monthly SaaS.

#### Subscriptions / recurring

- Plans with public/private status, start date = sign-up or fixed.
- Shareable recurring payment links (multiple customers, same plan).
- Customer self-serve portal.
- Retries on failed cards.
- Renewal reminder emails.
- SG GIRO (bank recurring) — important because PayNow is not a true MIT card-on-file.
- Cross-border recurring: MY merchant can collect SG PayNow-like rails? Pricing page lists cross-border recurring with THB 3.4%, VND 3.35%, and (on MY page) SG PayNow-style rates for online cross-border. **1% FX** at settlement.
- API: Recurring Billing guide + Save Payment Method (charge any amount later).

**vs Lazuar:** HitPay is **merchant-usable this afternoon**. Lazuar Commerce is **integrator-usable** if you speak TypeSpec. Off-session on MY cards is a HitPay/Stripe/CHIP job; Billplz cannot do it. HitPay’s +0.2% recurring software fee is cheap compared with building dunning. Lazuar’s only winning wedge is **LHDN on each cycle + processor-agnostic state machine + WhatsApp dunning (when real)**.

#### POS

HitPay is a **real POS**. Lazuar / Aura System C is **cash + proof + DuitNow screenshot**, not a card terminal. Do not copy HitPay terminals. Aura `PS-*` stays cash/proof. If a salon wants a terminal, they buy HitPay or Qashier and Aura records the visit as paid. Tracker: do not mint a Lazuar terminal SKU.

#### API and webhooks

Docs overview (16 Aug 2026):

- REST. HTTPS. HMAC-SHA256 webhook verification.
- Rate limits: **400 req/min** general; **70 req/min** payment creation.
- Sandbox available.
- Surfaces:
  - Redirect Checkout (hosted, all methods, zero frontend).
  - Embedded QR (PayNow, DuitNow, QRPH) for kiosks.
  - Recurring Billing.
  - Save Payment Method.
  - Card-reader / in-person terminal API.

Webhook events (registered per URL under Developers):

| Event | Trigger |
| --- | --- |
| `charge.created` | Payment succeeded |
| `charge.updated` | Refund / partial refund |
| `payout.created` | Payout succeeded |
| `order.created` / `order.updated` | Online store / POS order |
| `invoice.created` | Invoice created |
| `transfer.*` | created / updated / processing / scheduled / paid / failed / canceled |
| `payment_request.completed` / `.failed` | Payment request |
| `recurring_billing.method_attached` / `.method_detached` | Wallet/card attach |
| `recurring_billing.subscription_updated` | Status or dashboard edit |

Headers: `Hitpay-Signature` (HMAC-SHA256 of **raw body** with **per-webhook salt**), `Hitpay-Event-Type`, `Hitpay-Event-Object`, `User-Agent: HitPay v2.0`.

**Two salts (easy to get wrong):**

1. **API-key salt** (Developers page) — signs older payment-request / plugin callbacks. Signature in payload field `hmac` over sorted concatenated params.
2. **Per-webhook salt** (Developers → Webhooks) — signs event webhooks. Signature in `Hitpay-Signature` over raw JSON.

Platform accounts: converting to Platform does **not** auto-send webhooks. Platform registers endpoints on the **platform** account. For Platform API charges (both `X-BUSINESS-API-KEY` and `X-PLATFORM-KEY`): `charge.*` goes to **platform** endpoints; `payment_request.*` still goes to the **sub-account** endpoints. `business_id` in payload identifies the sub.

Lazuar’s comparable discipline: one HMAC scheme, one event family per product line. If a HitPay adapter is built, map `charge.created` → `payment.completed`, `charge.updated` (refund) → `payment.refunded`, `payment_request.failed` → `payment.failed`. Do not ingest HitPay `invoice.*` as LHDN `invoice.*`.

#### Pricing posture (public, 16 Aug 2026)

**No monthly. No setup.** Custom pricing if last-6-month average volume > **RM 50k** (MY) / **S$50k** (SG) / **PHP 500k** (PH) / **USD 35k** (other).

**Malaysia (hitpayapp.com/my/pricing):**

| Item | Public rate |
| --- | --- |
| Domestic cards (online; recurring same) | **1.2% + RM 1** |
| International cards | **3% + RM 1** |
| Foreign-currency transactions | **+2%** |
| In-person domestic cards | **1.4% min RM 0.30** (FAQ) |
| DuitNow | **1.2%** (also printed on earlier crawl of the same page) |
| FPX | **1.8% + RM 0.40** (earlier crawl) |
| Touch ’n Go | **1.9%** |
| GrabPay | **2%** |
| ShopeePay / SPayLater | **2.2%** |
| Maybank QRPay | **2.1%** (earlier crawl) |
| Cross-border online from SG PayNow | **0.9% min S$0.20** below S$100; **0.65% + S$0.30** at/above S$100 |
| Cross-border in-person SG | **1.5%** below S$100 (partial extract) |
| FX on cross-border settle to MYR | **1% markup** |
| Payment Links / Invoicing / Store / POS / Recurring software | **+0.2% each** |
| Refunds | no extra processing fee; original fee kept |
| Payout non-card | **T+2** business days (MY page) |
| Payout cards | from **T+1** business days |

**Singapore (hitpayapp.com/sg/pricing):**

| Item | Public rate |
| --- | --- |
| Domestic cards | **2.8% + S$0.50** |
| International cards | **3.65% + S$0.50** |
| Foreign-currency | **+2%** |
| PayNow ≥ S$100 | **0.65% + S$0.30** |
| PayNow < S$100 | **0.9% min S$0.20** |
| GrabPay | **3%** |
| PayLater by Grab | **5.5%** |
| ShopeePay | **3%** |
| ShopeePay Later | **5%** (online) / **5.5%** (recurring table) |
| Atome | **5.5%** |
| Shopback | **3.9% + S$0.20** |
| WeChat Pay | **1.5%** |
| UPI | **2%** |
| GIRO (recurring) | **S$2.25 + 0.65%** |
| Software add-ons | **+0.2%** |
| Payout non-card | **T+1 calendar day** |
| Payout cards | from **T+1 business day** |

HitPay **is more expensive than Xendit** on several MY local methods once you add the MYR 0.90 Xendit processing vs HitPay’s % — compare per ticket size. Example RM 100 FPX: Xendit MYR 1.20 + 0.90 = **RM 2.10**; HitPay 1.8% + 0.40 = **RM 2.20**. RM 100 TnG: Xendit 1.80 + 0.90 = **RM 2.70**; HitPay 1.9% = **RM 1.90**. RM 100 domestic card: Xendit 2.00 + 0.90 = **RM 2.90**; HitPay 1.2% + 1 = **RM 2.20**. **Do not claim a universal winner.** Claim: both are pay-as-you-go acquirers; HitPay bundles software; Xendit bundles regional API + payouts.

Under the hood, HitPay’s sandbox charge sample showed `payment_provider.code = stripe_sg`. HitPay is often a **facade over Stripe / Adyen / local acquirers**. The merchant does not care. Lazuar should not care either, except for refund/chargeback behaviour.

#### Tax

- Invoice tax line: **commercial GST/SST display**, not government e-invoice.
- **No LHDN MyInvois** on any HitPay page fetched 16 Aug 2026.
- This is the single largest product hole Lazuar can drive a truck through — **if** the LHDN UX actually ships.

#### Multi-country

HitPay’s motion is **one SMB account, many rails, including cross-border QR and PayNow→MYR**. A JB salon taking SG tourist PayNow is a real 2026 story (HitPay Borderless QR + POS). Entity still matters for settlement currency and KYC. They are **not** xenPlatform-complete in public marketing the way Xendit is, but Platform APIs + reseller exist.

#### Why a merchant would pick HitPay over Lazuar

- They are **non-technical**. Dashboard in 10 minutes. WhatsApp the link.
- They want **POS + online + invoices + recurring** in one login.
- They want **Tap to Pay** on the phone they already hold.
- They want **SG + MY** tourists / clients without opening two processors.
- They already heard the name from another salon / F&B group.
- They do not want to create a Billplz account and paste keys into Hub.
- **+0.2%** feels like “free software.”

#### Why they would pick Lazuar over HitPay

- They refuse to **move acquire** (already on Billplz, CHIP, a bank terminal).
- They need **LHDN** (HitPay invoice will not save them from MyInvois).
- They are a **SaaS / headless** product (Aura) and cannot put salon money through HitPay’s merchant-of-record-shaped flow without becoming a HitPay platform account.
- They want **0% software on GMV** at scale (HitPay’s 0.2% + MDR compounds).
- They want a **ledger** and dunning that is not trapped in HitPay’s dashboard.

#### vs Lazuar — verdict

**Primary SMB displacement threat in MY+SG.** Product lesson: **payment link + invoice + reminder + WhatsApp share** is table stakes for anyone selling to humans who chat. Lazuar’s headless purity (ADR 015 / 019 “we are not a CMS”) is correct for *front-end*, but the **ops user still needs a 30-second “create payable invoice” screen**. If that screen does not exist in `lazuar-ops`, HitPay wins the founder and Aura never gets a chance to be the system of record.

Do **not** become HitPay (terminals, online store builder, 0.2% SKUs). Do **steal** the invoice object shape, reminder cadences, and WhatsApp-first share. Do **file LHDN on the same invoice** — that is the only durable wedge.

---

### 4. PayMongo (Philippines)

#### Identity

- **Market:** Philippines. BSP-regulated (vendor).
- **Positioning:** Developer-friendly PH payments. Started as “Stripe for PH.” 2026 site is broader: payments + wallet + ledgers + capital + platforms + AI storefront.
- **URLs:** [https://www.paymongo.com/pricing](https://www.paymongo.com/pricing) · [https://www.paymongo.com/products/accept-payments/subscriptions](https://www.paymongo.com/products/accept-payments/subscriptions) · [https://docs.paymongo.com](https://docs.paymongo.com)

#### What merchants actually buy

- **Payments API** + hosted checkout.
- **Payment Links, plugins, pages** — standard MDR, no extra software fee on the public card.
- **QR Ph** in-store and online: **1.34%**.
- **Cards:** 3.125% + ₱13.39 domestic; **4.02% + ₱13.39** international.
- **E-wallets:** GCash **2.23%**, Maya **1.79%**, GrabPay **1.96%**, ShopeePay (incl. SPayLater, MariBank) **1.70%**.
- **Direct online banking** (BDO, UnionBank, BPI, Landbank, Metrobank): **0.71% or ₱13.39**.
- **BillEase BNPL:** 1.34%.
- **Subscriptions:** cards + Maya automated; GCash subscriptions “contact support.” Plan via API; checkout enroll; retries; dashboard visibility. Invoice generation for subscriptions “coming 2026” (PayMongo YouTube 5 Aug 2026).
- **Storefront AI:** **₱349/month** + ₱50 / 3 credits. The only prominent monthly SaaS on the page.
- **Protect** fraud: **₱120,000/mo** + ₱2.90–₱2.45 per screen — enterprise.
- **Wallet / money movement:** instant settlement (up to 2–3%), payouts **₱10** InstaPay/PesoNet, KYC ₱30/sheet, wallet maintenance ₱15 down to ₱3 / account / month by tier, ledgers **₱20,000** starter / **₱100,000** growth, workflows custom.
- **Platforms:** linked accounts, payment splitting, embedded financial services — custom.
- **Support plans:** free Essential; Advanced **0.3% TPV min ₱58,888**; Premium **0.5% TPV min ₱78,888**.

#### Invoicing

Payment Links are the invoice analogue. Native AR invoicing with BIR-compliant official receipts is **not** a headline SKU on the pricing page. Subscription invoices “coming 2026.” **Not** a tax-authority e-invoice product comparable to LHDN.

#### API and webhooks

REST, PH developer-famous. Resources: Payments, Payment Intents, Sources (legacy), Links, Customers, Subscriptions (plans, subscriptions, invoices-coming). Webhooks for payment paid/failed, source chargeable (older), subscription events. Secret keys. Test mode. This is the pattern PayMongo beat Dragonpay with: **docs a junior can finish in an afternoon**.

#### Pricing posture

Pay-as-you-go MDR, **no setup, no monthly** on Standard. Custom for volume. All **exclusive of VAT**. Storefront is the SMB monthly hook. Ledger/wallet is the fintech upsell — PayMongo is trying to become a mini-Airwallex for PH platforms.

#### Tax

PH invoicing / OR / BIR is the merchant’s problem. PayMongo does not replace a CAS.

#### Multi-country

**None as a home processor.** A PH company going regional uses Xendit / 2C2P / Airwallex, or PayMongo + another acquire.

#### vs Lazuar

**Pattern library, not a MY rival.** Steal: Payment Links priced at **standard MDR** (no +0.2%), subscription API + Maya auto-debit, ruthless docs. Ignore: ₱349 AI storefront (CMS trap, ADR 015/019). Ignore: becoming a wallet vendor (`Ledgers ₱20k`). If Aura expands to PH salons, PayMongo or Xendit is the adapter, Lazuar stays the cashier + (local tax TBD).

---

### 5. 2C2P / Ascend / Antom

#### Identity

- **Brand today:** **“2C2P by Antom”** (site, 16 Aug 2026).
- **History:** Founded Bangkok **2003** (Aung Kyaw Moe). HQ Singapore. Ant International (ex Ant Group IBG) took a **majority / strategic** stake **April 2022**. 2C2P is now a brand inside **Antom**, Ant International’s global acquiring + digitisation arm.
- **“Ascend”:** Thai conglomerate **Ascend Money / Ascend Group** (True / CP orbit) is a **different company**. Older market shorthand sometimes said “2C2P / Ascend” because of Thai payments adjacency and TrueMoney. **Do not treat 2C2P as an Ascend subsidiary in 2026.** The living parent story is **Antom / Ant International**. TrueMoney still appears as a **wallet method** on Xendit TH, not as 2C2P’s owner.
- **Customers they print:** Lazada, AirAsia, Aviva, Lenovo, Thai Airways, Capella, Changi, MSIG, Anantara.
- **URLs:** [https://2c2p.com/](https://2c2p.com/) · [https://2c2p.com/payment-methods/](https://2c2p.com/payment-methods/) · [https://www.antom.com/](https://www.antom.com/)

#### What merchants actually buy

- **Accept:** vendor claim **400+ payment methods**, OTC at **600,000+** locations across Asia. Cards (Visa, MC, Amex, Diners, Discover, UnionPay, JCB, MPU, Korean cards, RuPay), **IPP** (instalment) with a long bank list (UOB, DBS, OCBC, HSBC, SCB, Maybank, CIMB, BDO, BCA, Thai banks…), wallets (Alipay, AlipayHK, WeChat, LINE Pay, …), BNPL, online/offline QR, 123 Network, even a crypto filter on the matrix.
- **Markets in the method matrix:** AU, KH, EU, HK, IN, ID, JP, KR, MY, MM, PH, SG, TW, TH, UK, VN.
- **Pay-by-instalments** is a flagship (TH/MY/SG bank IPP). Lenovo quote on site is about lowering effective price via monthly instalments.
- **Issuing:** wallets and cards to partners/customers.
- **Payout & remittance:** large transfers, complex settlement, domestic and cross-border.
- **Antom overlay:** global acquiring, 300+ methods / 200+ markets (Antom homepage claim), unified APIs, pre-built checkout, SDKs. 2C2P is the **SEA enterprise face**; Antom is the **global API face**.

This is **not** an SMB invoice product. There is no public “create invoice in 2 minutes” dashboard competing with HitPay. The buyer is a **payment or finance team** at an airline, OTA, marketplace, or retailer.

#### Invoicing / subscriptions

Not productized as SMB SKUs on the public site. Recurring / tokenisation / 3DS exist as **enterprise features** behind sales. No LHDN.

#### API and webhooks

Sales-led. Antom docs (`docs.antom.com`) advertise unified payment APIs, hosted checkout, SDKs, API explorer. 2C2P historically had PGW / Payment Gateway API, Redirect, Server-to-server, JWT, backend notifications with HMAC. A 2026 integrator targeting 2C2P should assume **Antom AMS APIs** plus residual 2C2P PGW for existing TH/SG merchants. Webhooks / backend notifications are the fulfillment event (same religion as Midtrans/Lazuar).

#### Pricing posture

**Unknown / custom.** No self-serve MDR table on 2c2p.com. Settlement cycles on the method matrix are often **T+1 to T+3** (cards), some T+7 (Korean cards, Amex up to T+7). Refund / 3DS / chargeback support flagged per method.

#### Tax

None as a government e-invoice product.

#### Multi-country

This is the product. One enterprise MSA, local acquiring, IPP per country, Alipay+/WeChat tourist rails, Antom for extra-SEA. **The named rival when a MY enterprise says “we need TH + SG + ID + cards + instalments.”**

#### vs Lazuar

**Not an SMB rival. An enterprise-acquire rival.** Lazuar should never try to become 2C2P. If an Aura enterprise salon chain (think regional spa group) already has 2C2P, Lazuar’s job is **BYOK adapter + LHDN**, same as Billplz. Adapter does not exist. Priority: far below Xendit/HitPay/Billplz.

---

### 6. Airwallex (collect, payouts, cards, embedded finance)

#### Identity

- **Positioning:** “Intelligent financial platform for global businesses.” Collect + treasury + spend + billing + embedded finance. 200,000+ businesses (vendor). **$287B** global payments annually (vendor, SG homepage).
- **SG license:** Airwallex (Singapore) Pte. Ltd., MAS **Major Payment Institution PS20200541** (Yield/instant-withdrawal footnote, 16 Aug 2026).
- **MY:** `airwallex.com/en-my` exists (online payments / methods blogs dated 2026).
- **URLs:** [https://www.airwallex.com/en-sg](https://www.airwallex.com/en-sg) · [https://www.airwallex.com/en-sg/pricing](https://www.airwallex.com/en-sg/pricing) · [https://www.airwallex.com/en-sg/platform-api-and-embedded-finance](https://www.airwallex.com/en-sg/platform-api-and-embedded-finance)

#### What merchants actually buy (five product lines)

1. **Business Accounts** — Global Accounts in 20+ currencies, collect local (SGD + others), FX 0.4% above interbank majors / 0.6% others, free local transfers to 120+ countries, SWIFT $20–35, Yield on USD/SGD.
2. **Spend** — corporate + employee cards (zero international fees on cards — vendor), expense, bill pay, reimbursements 200+ countries, AI receipt matching.
3. **Payments (Collect)** — hosted checkout, payment links (incl. custom URL), plugins (Shopify, Woo, Magento), Drop-in / API / SDK. **160+ local methods**, 130+ currencies (homepage). Like-for-like settlement in 14 currencies. 3DS, risk thresholds, disputes.
4. **Billing** — invoicing (one-off + recurring) **free** as software; subscription management; usage-based billing (metering). Intelligent dunning, mid-cycle edits, trials, multi-entity, credit notes, auto-reconciliation. **0.50% per successful transaction** on subscription / UBB (on top of collect MDR).
5. **Platform APIs / Embedded Finance** — Connected Accounts, Payments, Accounts, Transactional FX, Payouts, Issuing. Custom pricing (“get in touch”).

This is the **Stripe + Brex + Bill.com + Treasury** bundle. A SG scale-up that already lives on Airwallex will not add Lazuar for “invoices” unless LHDN is mandatory.

#### Invoicing and subscriptions

Airwallex Billing is the **closest global-quality invoice + subscription product** in this file:

- Invoices + payment links with 160+ methods.
- Subscriptions: flat, per-unit, graduated; multi-frequency in one subscription; trials; discounts; dunning; mid-cycle upgrade/downgrade.
- Usage-based: event ingestion, metering, hybrid plans.
- 0.50% billing fee on success (printed).

**vs Lazuar Commerce:** Airwallex is more complete on UBB and mid-cycle. Lazuar is more complete on **MY statutory tax** (when UX ships) and **BYOK / 0% GMV**. Airwallex collect MDR (SG domestic cards **3.30% + S$0.50**) is **worse than HitPay** (2.8% + 0.50) and in the same band as Xendit SG (3.30% + 0.50 + 0.30 processing).

#### API and webhooks

Full REST platform. Connected Accounts (Connect analogue). Issuing. Payouts with dynamic beneficiary schema. Embedded components for payouts. Webhooks for payments, payouts, accounts, issuing. This is a **year-long integration** if you embed finance; a **week** if you only use payment links.

#### Pricing posture (SG public, 16 Aug 2026)

Monthly prices **SGD, exclusive of GST**. Plans (per legal entity):

| Plan | Monthly | Notes |
| --- | --- | --- |
| **Explore** | **Free** | 10 company cards, 5 free spend users then $5/user, Yield 3.30% USD / 0.59% SGD (indicative 15 Aug 2026), instant withdrawal 0.2% |
| **Grow** | **$79** (1-month trial) | 50 cards, better Yield 3.41% / 0.69%, approvals, HRIS |
| **Accelerate** | **from $399** | Unlimited cards, Yield 3.79% / 1.08%, NetSuite/D365, dedicated AM, multi-entity |

**Collect:** domestic cards **3.30% + $0.50**; international **3.60% + $0.50**; local methods **$0.50 + method fee**; subscriptions **+0.50%** per successful card transaction.

Customers on pre-1 Jun 2025 pricing were migrated from 1 Jul 2025 (FAQ).

#### Tax

Invoice tax display possible. **No LHDN.** No Coretax. Airwallex will not keep a MY SDM legally alive.

#### Multi-country

Core motion. Global Accounts mean a SG entity can **collect USD/EUR/GBP locally** without a US bank. That is a **treasury** job Lazuar should never copy. Embedded finance is how a platform issues accounts/cards to users — trap for Lazuar (BaaS).

#### vs Lazuar

**Rival on Billing (invoices + subscriptions + dunning + UBB). Adjacent on treasury. Trap on embedded finance.**

A MY exporter who invoices US customers in USD will pick Airwallex Billing, not Lazuar. A MY domestic subscription business that must file MyInvois will pick Lazuar (if it works) or HitPay + an accountant. **Do not add Global Accounts.** Do **study** Airwallex’s subscription object (multi-frequency, UBB, mid-cycle) before inventing Commerce v2.

---

### 7. Rapyd

#### Identity

- **Positioning:** Fintech-as-a-Service. **Collect, Disburse, Wallet, Issue** across 100+ countries (vendor / APIs.io). Cloud microservices. Plugins + API.
- **URLs:** [https://www.rapyd.net/products/payments/](https://www.rapyd.net/products/payments/) · [https://docs.rapyd.net/en/payment.html](https://docs.rapyd.net/en/payment.html) · [https://docs.rapyd.net/en/subscription.html](https://docs.rapyd.net/en/subscription.html)

#### What merchants actually buy

- **Collect:** cards + local methods + hosted checkout + plugins. Payment object can fund one or more **Rapyd Wallets**. Can attach to Order, Invoice, or Subscription.
- **Subscriptions:** interval billing (docs: monthly or other). Client pays invoice with selected method automatically.
- **Disburse:** payouts to **190+ countries** (blog 2025) — bank, card, e-wallet, stablecoin.
- **Wallet:** hold multi-currency, split, platform money movement.
- **Issue:** cards.
- SEA mention on payments page: “Rapyd helps SEA Gamer Mall enter new markets quickly” + “competitive fee rates and excellent FX.”

Rapyd is what a **global marketplace or gaming platform** evaluates when they do not want six regional Xendits. It is rarely what a Cheras salon or a MY indie SaaS evaluates first.

#### Invoicing / subscriptions

First-class objects in the Payment docs (`Invoice`, `Subscription`, `Order`). This is Stripe-shaped. Tax localization is the merchant’s problem. No LHDN.

#### API and webhooks

Rapyd has a large REST surface (Payments, Customers, Subscriptions, Plans, Invoices, Payouts, Wallets, Issuing). Access key + secret, HMAC signature on requests and webhooks. Sandbox. The developer experience is **powerful and notoriously sprawling**. Support quality is a recurring third-party complaint (secondary; do not treat as fact).

#### Pricing posture

**Custom / partner.** No honest public SEA MDR table was retrieved on 16 Aug 2026 (`/pricing` 404). UK comparison sites quote card-like 2.9% + 20p — **secondary, not for tables**. Assume sales-led, FX as a profit centre.

#### Multi-country

The reason Rapyd exists. Local acquiring via partners. Coverage ≠ depth: a “PH method” on Rapyd can be worse than PayMongo native.

#### vs Lazuar

**Phase 3 adjacency (ADR 019 MassPay / global collect), not a 2026 rival.** If Lazuar ever needs “payout to 190 countries,” Rapyd or Wise or Airwallex is a **vendor**, not a product to clone. Do not build Rapyd Wallets.

---

### 8. Aspire / Wise Business (adjacent treasury)

These are **operating accounts**, not checkouts. They show up in the same founder Slack as HitPay because the founder wants **one app for money in, money out, and cards**.

#### Aspire

- **What:** Multi-currency business account. Singapore **Payment Services Institution**. Money safeguarded at **DBS** (vendor); partnerships Citibank, JPMorgan, Visa (vendor).
- **Rails:** Domestic **FAST, GIRO, MEPS, PayNow, Local TT**. International ACH, SEPA, SWIFT, FPS. Local collection in **8 currencies** (SGD, USD, EUR, GBP, MYR, PHP, IDR, VND) — Aspire vs Wise page, comparison dated **July 2026**.
- **Software:** Native **expense management**, **invoicing**, corporate cards, approvals, budgets, Xero/QuickBooks, Shopify/Temu/Shopee/Amazon.
- **API:** Yes. Batch payments in SGD, INR, USD, IDR, MYR, EUR, PHP, AUD, THB, VND.
- **Pricing (Aspire vs Wise page, Jul 2026):** Basic **S$0/mo**, Premium **S$15/mo**. No setup fee. Local send **free** (PayNow, FAST, scheduled, local TT). SWIFT send **USD 15 SHA / USD 30 OUR**. Receive local free; international receive **SGD 35** (SGD account) / **USD 8** (other) via SWIFT. FX markup **from 0.22%**.
- **Invoicing:** Native AR. **Not LHDN.** Closes the “I invoice from my bank app” loop.

#### Wise Business

- **What:** Multi-currency hold + mid-market FX. SG **Major Payment Institution**.
- **Pricing (wise.com/sg/pricing/business, 16 Aug 2026):**
  - Register: free.
  - **All-in setup to get receive details in 22 currencies: S$99** (one-off).
  - Send / convert: **from 0.19%** (varies by currency); volume discount over 30,000 SGD.
  - Receive domestic (AUD, CAD, EUR, GBP, HUF, NZD, PHP, SGD, USD): **free**.
  - Receive USD wire/SWIFT: **USD 6.11**; GBP SWIFT **GBP 2.16**; EUR SWIFT **EUR 2.39**.
  - Debit card: ATM 100 SGD/mo free then 1.75%; e-wallet top-up 2%; cannot use for **local ATM in Singapore**.
  - Interest / stocks: 0.44% / 0.71% annual fees (capital at risk).
- **Rails:** FAST, PayNow free send (Aspire comparison). International via Wise network + SWIFT. Collection accounts in **22 currencies**.
- **Invoicing:** Native, more limited than Aspire (Aspire’s claim, Jul 2026).
- **API:** Yes.

#### vs Lazuar

**Not competitors for checkout.** Complementary:

- Founder uses **Lazuar** (or HitPay/Xendit) to **charge customers**.
- Founder uses **Aspire/Wise/Airwallex** to **hold, convert, pay suppliers, issue staff cards**.

Soft overlap: Aspire invoices. A founder who only needs to invoice other businesses in SGD/USD may never need Lazuar Commerce. They still need Lazuar **if** they must file MyInvois on those invoices.

ADR 019 Phase 3 already names **Wise MassPay** for affiliate payouts. That is the correct relationship: **Wise as a payout vendor**, not a checkout.

**Trap:** building Aspire-like expense + cards + yield inside Lazuar. That is a bank. Kill it.

---

### 9. GrabPay / PayNow / DuitNow QR as rails

These are **schemes**. Merchants do not “switch to DuitNow” the way they switch to HitPay. They switch to a **processor that acquires the scheme**.

#### DuitNow QR (Malaysia)

- National QR, PayNet / BNM. Person-to-person and person-to-merchant.
- Consumer-side: **no fee** to scan (GrabPay DuitNow FAQ: no additional charges to the payer).
- Merchant-side: **MDR**. BNM (29 Sep 2023) confirmed acquirers may charge MDR after the 2019–2023 waiver; MDR should be **as low as or lower than debit-card MDR**. Actual % is set by the acquirer:
  - HitPay public: **1.2%**
  - Xendit public DuitNow Pay (online banking): **MYR 2.00 + 0.90** (a **flat**, different product from DuitNow QR % — do not conflate DuitNow Pay vs DuitNow QR)
- Cross-border: PayNow–DuitNow linkage (MAS/BNM). SG consumers pay MY QR (PayLah, bank apps). FX markup lives at the bank/wallet (DBS PayLah page: **2%** markup on conversion for some cross-border QR — bank page, not DuitNow scheme fee).
- Informal MY default till: printed DuitNow QR + screenshot. Aura `PS-005` payment proof is this rail with humans in the loop.

#### PayNow (Singapore)

- Proxy (mobile / NRIC / UEN) + QR. FAST under the hood.
- Consumer P2P: typically free at banks.
- Merchant acquiring: **not free**. HitPay: **0.65% + S$0.30** (≥ S$100) or **0.9% min S$0.20** (< S$100). Xendit PayNow QR: **1.30% + SGD 0.30**. Airwallex: **$0.50 + method fee** (method fee unknown on the plan page).
- Recurring: PayNow is a **push**. True subscription uses **GIRO** (HitPay S$2.25 + 0.65%) or cards.
- Cross-border: PayNow–DuitNow; HitPay Borderless QR productizes the merchant side.

#### GrabPay

- Super-app wallet: MY, SG, PH, TH, etc. Also **GrabPay PayLater**.
- Payer: usually no extra fee at checkout (promo-dependent).
- Merchant MDR (public processors):
  - Xendit MY: **2.00% + MYR 0.90**
  - Xendit PH: **2.00% + PHP 11**
  - HitPay MY: **2%**; HitPay SG: **3%**; PayLater Grab SG **5.5%**
  - PayMongo PH: **1.96%**
  - Midtrans: not a MY/SG wallet; ID equivalent is **GoPay 2%**
- GrabPay is often **tokenizable** for recurring (Xendit subscriptions story). Billplz historically weaker here.

#### Other rails Lazuar will be asked for (not full dossiers)

| Rail | Country | Notes |
| --- | --- | --- |
| **FPX** | MY | Bank login redirect. Still the B2C workhorse. Billplz/CHIP/Xendit/HitPay all acquire it. Not off-session. |
| **TnG eWallet** | MY | Highest consumer mindshare with DuitNow QR. HitPay 1.9%; Xendit 1.8%+0.90. |
| **QRIS** | ID | BI-regulated ~0.7%. Midtrans and Xendit. |
| **PromptPay** | TH | 2C2P native; Xendit 2.50% min THB 10 + THB 7. |
| **QR Ph** | PH | PayMongo 1.34%; Xendit 1.50% min 15 + 11. |
| **Boost / ShopeePay / MAE** | MY | Acquirer-dependent. HitPay lists ShopeePay 2.2%. |

#### vs Lazuar

**Lazuar must not build a DuitNow member connection.** PayNet membership is an acquirer job. Lazuar must:

1. Expose whatever the **active BYOK adapter** already acquires (Billplz FPX/DuitNow; CHIP mix; Stripe cards).
2. Add adapters that carry the missing rail (Xendit, HitPay, Fiuu) when a tenant’s Billplz bag is not enough.
3. Keep Aura System C as **proof-of-QR** for merchants who will never KYC a processor.

Tracker: `PY-015` already exists as “CHIP / extra MY rails.” This file adds `SEA-014` (DuitNow QR as a first-class method in Hub checkout UX, not just “whatever Billplz shows”) and `SEA-015` (PayNow as a cross-border method, only via a processor that has it).

---

## Multi-country expansion implications

### The question founders actually ask

“We’re live in KL. Next year we want Singapore clients / a Jakarta store / a Manila course. Can Lazuar do that?”

Honest answer on 16 Aug 2026: **Lazuar can follow a processor that can. Lazuar cannot be that processor.**

### Entity physics (do not hand-wave)

| Expansion | What the law wants | What a processor does | What Lazuar should do |
| --- | --- | --- | --- |
| MY company, SG **customers** | Usually still a MY supply. SST/LHDN export / zero-rate rules. | HitPay Borderless / Xendit SG methods / Stripe | Keep BYOK. Classify ledger as **export** (ADR 021 Pillar 3). File LHDN correctly. Do not open a fake SG acquire. |
| MY company, SG **entity** | New KYC, GST, PayNow UEN. | New HitPay/Xendit/Airwallex account for the SG entity | **New Hub workspace.** 1 legal entity : 1 workspace. Do not mix GST and SST in one ledger. |
| MY company, ID **entity** | PT, NPWP, Coretax, QRIS/VA. | Midtrans **or** Xendit ID | New workspace + **new adapter** (neither ships). Coretax is README-only. |
| MY company, PH **entity** | SEC, BIR, QR Ph/GCash. | PayMongo **or** Xendit PH | Same. No PH tax module. |
| Marketplace / multi-seller | Local e-money / PSP license if you hold funds | xenPlatform / HitPay Platform / PayMongo linked accounts / Airwallex Connected Accounts | **Never hold funds.** Each seller BYOK. `PY-022` Never. |

### What “one API, many countries” really costs

Xendit, 2C2P/Antom, Airwallex, Rapyd sell this sentence. The fine print is:

- **Local KYC still happens** (sometimes on sub-accounts).
- **Settlement currency and payout bank** are per entity.
- **Method mix is per market.** FPX does not work for a Jakarta buyer.
- **Tax is per market.** LHDN XML does not file Coretax.
- **Chargeback and refund law** is per scheme + per regulator.
- **Pricing is per market** (see Xendit’s table). There is no “SEA MDR.”

Lazuar’s correct multi-country architecture is already in the repo: **workspace isolation + BYOK vault + normalized events.** The missing piece is **adapters + tax products per country**, not a Lazuar-licensed acquire.

### Recommended expansion order (product, not company registration)

1. **Stay MY-deep.** Billplz + CHIP + Stripe adapters honest. LHDN UX actually routed. Commerce invoice that files MyInvois. WhatsApp dunning if it is real.
2. **Xendit adapter.** Unlocks MY method depth (TnG, GrabPay, FPX-DD, VA) **and** a path to ID/PH/TH without a second cashier contract. This is the highest-leverage *engineering* expansion.
3. **HitPay adapter (optional).** Only if tenants demand it as *their* acquire (common for SG+MY SMBs). Do not also copy HitPay’s store/POS.
4. **SG as a tax/ledger mode**, not as a license. GST invoice display + export classification. PayNow via Xendit/HitPay/Stripe, not via PayNet membership.
5. **ID only with a tax story.** Midtrans or Xendit adapter **plus** a Coretax plan, or do not bother — ID without e-Faktur is the same empty calorie as MY without LHDN.
6. **Refuse** Rapyd/Airwallex-shaped “we are global FaaS” until MY LHDN + subscriptions are undeniable.

### What multi-country must not change

- 0% GMV.
- 1 Hub workspace : 1 legal merchant entity (Aura already: 1 org : 1 workspace).
- Redirect ≠ paid.
- Paddle remains System A for Aura seats.
- No xenPlatform take-rate.

### HitPay vs Xendit as expansion partners

| Need | HitPay | Xendit |
| --- | --- | --- |
| MY SMB already on them | High | Medium |
| SG tourist / PayNow | **Best** (Borderless QR, POS) | OK (PayNow QR 1.30%) |
| ID depth (VA, QRIS, GoPay, OTC) | Weaker public story | **Best** among regional APIs; Midtrans deeper in GoTo |
| PH depth | Present | **Best** regional; PayMongo deeper locally |
| SaaS sub-accounts | Platform APIs exist | **xenPlatform is the category** |
| Subscriptions on local wallets | Good in SG/MY cards + some wallets + GIRO | **Best MIT story** |
| Docs for a .NET integrator | Good REST + HMAC | Excellent but dual-generation (v2 invoice vs sessions) |
| LHDN | No | No |
| Fits BYOK philosophy | Yes (they are just another acquire) | Yes |

**Build Xendit first** (regional + MY method depth + README debt). **Speak HitPay in sales** (that is the name MY SMBs say). **Integrate HitPay if a beachhead of tenants refuses to leave it.**

---

## Feature tables

Marks: **Y** = sold / documented as a product job. **P** = partial, sales-only, or honesty gap. **N** = not a product job. **—** = not applicable.

**Lazuar column** is repo-honest (16 Aug 2026), not README-ambitious.

### A. Company shape

| Job | Xendit | Midtrans | HitPay | PayMongo | 2C2P | Airwallex | Rapyd | Aspire/Wise | Rails | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Licensed acquirer / MPI / PSI | Y | Y | Y | Y | Y | Y | Y | Y (account) | Y (scheme) | **N** |
| Holds merchant float | Y | Y | Y | Y | Y | Y | Y | Y | — | **N** |
| BYOK / software-only | N | N | N | N | N | N | N | N | — | **Y** |
| 0% GMV software fee | N | N | N (0.2% add-ons) | Y (links at MDR) | N | N (0.5% billing) | N | — | — | **Y** |
| Self-serve signup | Y | Y | Y | Y | N | Y | P | Y | — | P (provision + ops) |
| SMB dashboard no-code | Y | Y | **Y** | Y | N | Y | P | Y | — | P |
| Developer-first API | **Y** | **Y** | Y | **Y** | P | Y | Y | P | — | **Y** |
| Marketplace / Connect | **xenPlatform** | P | Platform APIs | Linked accounts | P | Connected Accounts | Wallets | N | — | **N (Never)** |

### B. Checkout and links

| Job | Xendit | Midtrans | HitPay | PayMongo | 2C2P | Airwallex | Rapyd | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Hosted checkout | Y (Links / Sessions) | Y (Snap) | Y | Y | Y | Y | Y | Y (portal) |
| Payment link no-code | Y | Y | **Y** | Y | N | Y | P | P (Commerce buy links; not 30-sec ops invoice) |
| Open / buyer-set amount | Y | P | Y | P | P | P | P | P |
| QR national rail on link | Y | Y (QRIS) | Y | Y (QR Ph) | Y | P | P | P (via processor page) |
| WhatsApp-first share | Y | P | **Y** | P | N | P | N | N (roadmap comms) |
| Partial / deposit pay | P | P | Y (invoices) | P | P | P | P | P (Aura deposit math; Hub is amount-in) |
| Branding (logo/colours) | Y | Y | Y | Y | P | Y | P | P |
| Method picker reorder | P | P | Y | P | P | P | P | N |

### C. Invoicing

| Job | Xendit | Midtrans | HitPay | PayMongo | 2C2P | Airwallex | Rapyd | Aspire | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Commercial invoice + pay button | Y (Links / `/v2/invoices`) | P (Payment Link) | **Y** | P (Links) | N | **Y** | Y | Y | P (Commerce / not AR-complete) |
| Invoice number / due / reminders | Y | P | **Y** | P | N | Y | Y | Y | P |
| Line items, discount, tax line | P | P | Y | P | N | Y | P | Y | P (ledger tax; invoice UX later) |
| Partial payments | P | N | Y | N | N | P | P | P | P |
| Recurring invoices | via Subs | via Subs | Y | coming 2026 | N | Y | Y | P | Y (Commerce) |
| Mark paid (cash) | P | P | Y | P | N | P | N | P | N (Aura System C, not Hub) |
| Accounting sync (Xero/QB) | P | P | Y | P | N | Y | P | Y | Roadmap (ADR 021 keep) |
| **Statutory e-invoice (LHDN / Coretax / BIR)** | **N** | **N** | **N** | **N** | **N** | **N** | **N** | **N** | **P (LHDN backend; UX unrouted; v1.1 sign later)** |

### D. Subscriptions and dunning

| Job | Xendit | Midtrans | HitPay | PayMongo | Airwallex | Rapyd | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Plans + intervals | Y | Y | Y | Y | Y | Y | Y |
| Hosted subscribe link | Y | P | **Y** | Y | Y | P | Y (Commerce) |
| Cards off-session | Y | Y | Y | Y | Y | Y | P (Stripe/CHIP only) |
| E-wallet / local MIT | **Y** | GoPay | P (GIRO SG; some MY wallets) | Maya (GCash sales) | P | P | N on Billplz |
| Smart retries | Y | Y | Y | Y | Y | P | P (email; WA later) |
| Payment-link fallback on fail | Y | P | P | P | P | P | P |
| Customer portal | Y | P | Y | API DIY | Y | P | P (magic link) |
| Usage-based billing | P | N | N | N | **Y** | P | N |
| Mid-cycle upgrade | P | N | P | P | **Y** | P | P |
| Extra software fee | unknown / sales | N (MDR only) | +0.2% | N | +0.50% | custom | credits / SaaS |

### E. API, webhooks, platform

| Job | Xendit | Midtrans | HitPay | PayMongo | 2C2P | Airwallex | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- |
| REST + secret key | Y | Y | Y | Y | Y | Y | Y |
| Sandbox | Y | Y | Y | Y | Y | Y | Y |
| Signed webhooks | Y | Y | Y (two salts!) | Y | Y | Y | Y (product-scoped) |
| Redirect ≠ fulfillment | Y | **Y** (culture) | Y | Y | Y | Y | **Y** (explicit) |
| Idempotency | Y | P | P | Y | P | Y | P (app guards) |
| Connect / sub-accounts | **xenPlatform** | N | Platform APIs | Linked accounts | P | Connected Accounts | Provision only |
| Split / application fee | Y | P | P | Y | P | Y | **N (Never GMV)** |
| Payouts API | Y | Iris / payout | Transfers + remittance | ₱10 InstaPay | Y | Y | N (Wise later) |
| Issuing / cards out | Y (corp cards) | N | N | N | Y | Y | N |
| Rate limits published | P | P | **400 / 70** | P | P | P | P |

### F. Tax and compliance

| Job | Xendit | Midtrans | HitPay | PayMongo | Airwallex | Aspire | Lazuar |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Display GST/SST/VAT on invoice | P | P | Y | P | Y | P | P |
| File LHDN MyInvois | N | N | N | N | N | N | **P backend** |
| B2C monthly consolidation | N | N | N | N | N | N | **Intended (ADR 021)** |
| TIN validation at checkout | N | N | N | N | N | N | **Intended B2B** |
| Indonesia Coretax | N | N | N | N | N | N | README only |
| PH BIR official receipt | N | N | N | N | N | N | N |
| PCI (they hold PAN) | Y | Y | Y | Y | Y | Y | N (BYOK; processor holds) |
| Own regulator license | Y | Y | Y | Y | Y | Y | N |

### G. Country coverage (accept)

| Market | Xendit | Midtrans | HitPay | PayMongo | 2C2P | Airwallex | Lazuar today |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Malaysia | **Y** | N | **Y** | N | Y | P | **Y** (via MY processors) |
| Singapore | Y | N | **Y** | N | Y | **Y** | N (unless Stripe/Xendit/HitPay keys) |
| Indonesia | **Y** | **Y** | P | N | Y | P | N |
| Philippines | **Y** | N | Y | **Y** | Y | P | N |
| Thailand | Y | N | P | N | **Y** | P | N |
| Vietnam | Y | N | P | N | Y | P | N |
| HK / AU / others | Y (HK) | N | Y (HK, AU, NZ) | N | Y | **Y** | N |

### H. Pricing snapshot (list rates only, 16 Aug 2026)

Compare **like-for-like tickets**. All exclusive of tax unless noted.

**Malaysia RM 100 success, selected methods:**

| Processor | Domestic card | FPX-like | TnG / e-wallet | Extra software |
| --- | --- | --- | --- | --- |
| Xendit | 2.00% + 0.90 = **RM 2.90** (credit) | Personal DD **RM 2.10** | TnG local **RM 2.70** | none public |
| HitPay | 1.2% + 1 = **RM 2.20** | 1.8% + 0.40 = **RM 2.20** | TnG **RM 1.90** | +0.2% if invoice/POS/link SKU |
| Lazuar | **processor’s MDR only** | same | same | SaaS + credits |

**Singapore S$100 card / PayNow:**

| Processor | Domestic card | PayNow ≥ S$100 | Extra |
| --- | --- | --- | --- |
| HitPay | 2.8% + 0.50 = **S$3.30** | 0.65% + 0.30 = **S$0.95** | +0.2% SKUs |
| Xendit | 3.30% + 0.50 + 0.30 = **S$4.10** | 1.30% + 0.30 = **S$1.60** | — |
| Airwallex | 3.30% + 0.50 = **S$3.80** | $0.50 + method fee (**unknown**) | $0–399/mo + 0.50% billing |

**Indonesia IDR 100,000 VA / QRIS / card:**

| Processor | VA | QRIS | Card |
| --- | --- | --- | --- |
| Midtrans | **IDR 4,000** | **0.7%** | 2.9% + 2,000 |
| Xendit | 9,000 + 4,000 = **IDR 13,000** | 0.7% + 4,000 | 2.9% + 2,000 + 4,000 |

### I. vs Lazuar — one-line jobs

| If the merchant says… | They will buy | Lazuar should… |
| --- | --- | --- |
| “Send me a link on WA” | **HitPay** | Ship a 30-second payable invoice in ops; do not become a store builder |
| “One API for ID+PH+MY” | **Xendit** | Build Xendit adapter; stay BYOK |
| “Snap / GoPay” | **Midtrans** | ID adapter later; not MY |
| “GCash + docs” | **PayMongo** | PH adapter later |
| “Airline IPP + 400 methods” | **2C2P** | Ignore until an enterprise tenant brings keys |
| “USD invoice + cards + FX” | **Airwallex** | Do not copy treasury; keep LHDN export codes |
| “Pay 200 countries” | Rapyd / Wise / Airwallex | Vendor, not product |
| “Operating account + staff cards” | **Aspire / Wise** | Adjacent; optional payout vendor |
| “DuitNow QR on the counter” | Rail via any acquirer or printed QR | System C proof; optional adapter method |
| “LHDN will fine me” | **Nobody in this file except Lazuar** | Finish UX + v1.1 sign; this is the only unique sentence |

---

## Tracker IDs

### How these IDs relate to `20` / `00`

- **Reuse** existing money/compliance IDs when the job is the same. Do not mint a second deposit row.
- **Mint `SEA-*`** for platform-vs-platform jobs this chapter introduced. `SEA-*` is a **Lazuar Pay / Hub** family, not a salon calendar family.
- Promotion into `00-checklist-tracker.md` only if the job is something Aura or Hub will sell. Traps stay Never and do not get a wave.

### Existing IDs this chapter depends on (do not fork)

| ID | Job | What this chapter adds |
| --- | --- | --- |
| `PY-001` | Guest online checkout | HitPay/Xendit links are the **comparison UX**. Hub must feel as trustworthy, not as a second acquire. |
| `PY-003` | Signed webhook fulfillment | Same religion as Midtrans notification / HitPay HMAC / Xendit callbacks. |
| `PY-010` | `payment.failed` honesty | HitPay `payment_request.failed` / Xendit failed cycle must never mark paid. |
| `PY-011` | Refunds | HitPay `charge.updated`; Xendit refunds keep processing fee. |
| `PY-014` | Off-session / vault renew | **Blocked on Billplz.** Unlock only via Stripe/CHIP/Xendit/HitPay MIT. |
| `PY-015` | CHIP / extra MY rails | Still valid. Xendit/HitPay adapters are the *other* way to get TnG/GrabPay. |
| `PY-022` | GMV take-rate / Aura-as-MoR | xenPlatform envy. **Never.** |
| `CP-003` | SST line on receipts | HitPay already displays tax on invoices. Table stakes, not LHDN. |
| `CP-004` | LHDN e-invoice | **The only unique row vs every dossier in this file.** Keep Later until demand, but do not pretend HitPay did it. |
| `SA-003` | Customer portal | HitPay/Xendit/Airwallex have one. Commerce portal is the analogue (not Paddle). |
| `XX-003` / `XX-004` | Marketplace / take-rate | xenPlatform / Rapyd Wallets / Airwallex Connected Accounts. Never as Aura/Lazuar acquire. |

### New IDs minted in this file (`SEA-*`)

Suggested wave is **Hub/Lazuar**, not Aura salon Wave 0–12. `W` = suggested Hub sequence after Aura soak is no longer the only story.

| ID | Feature | Lazuar now | HitPay | Xendit | Midtrans | PayMongo | Airwallex | V | W | Class |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `SEA-001` | 30-second ops payable invoice / payment link (amount, due, WA share) | P | Y | Y | Y | Y | Y | **Partial** | 1 | table-stakes vs HitPay |
| `SEA-002` | Commercial invoice object (number, due, reminders, partials, mark-paid) | P | Y | P | N | P | Y | Later | 2 | table-stakes SMB |
| `SEA-003` | Same invoice **also** submits LHDN (get-paid + statutory) | N | N | N | N | N | N | **Ours (wedge)** | 2 | differentiator |
| `SEA-004` | Xendit BYOK adapter (Payment Session + webhooks) | **N** (README lie) | — | Y | — | — | — | **Partial** | 1 | table-stakes debt |
| `SEA-005` | HitPay BYOK adapter (payment request + HMAC) | N | Y | — | — | — | — | Later | 3 | demand-gated |
| `SEA-006` | Fiuu adapter (README named, missing) | N | — | — | — | — | — | Later | 3 | hygiene |
| `SEA-007` | Subscription MIT on local methods (not Billplz) | P | Y | **Y** | P | P | P | Later | 2 | differentiator vs Stripe-only |
| `SEA-008` | Dunning: retry + payment-link fallback + WhatsApp | P | Y | Y | P | Y | Y | Later | 2 | ADR 021 keep |
| `SEA-009` | Normalized `payment.*` across new adapters | Y (4 adapters) | — | — | — | — | — | Both | 1 | hygiene |
| `SEA-010` | Do not ingest processor `invoice.*` as LHDN `invoice.*` | Y (separate buses) | — | — | — | — | — | **Ours** | 0 | hygiene / lock |
| `SEA-011` | Multi-entity = multi-workspace (SG GST ≠ MY SST) | Y (workspace) | P | P | — | — | Y | Both | 4 | expansion lock |
| `SEA-012` | Export / zero-rate classification on foreign payers | P | N | N | N | N | P | Later | 4 | ADR 021 Pillar 3 |
| `SEA-013` | Midtrans adapter (Snap or Core + notification) | N | — | — | Y | — | — | Later | 5 | ID only |
| `SEA-014` | DuitNow QR visible as a method in Hub (via processor) | P | Y | P | — | — | — | Later | 2 | rail honesty |
| `SEA-015` | PayNow / cross-border QR via processor (not PayNet membership) | N | Y | Y | N | N | P | Later | 4 | SG tourists |
| `SEA-016` | Processor-agnostic subscription customer portal | P | Y | Y | N | P | Y | Later | 3 | table-stakes |
| `SEA-017` | Xero/QB sync after LHDN + ledger | N | Y | P | N | N | Y | Later | 4 | ADR 021 keep |
| `SEA-018` | Usage-based billing | N | N | P | N | N | Y | Later | 6 | later-nice |
| `SEA-019` | xenPlatform-style split / hold funds | N | P | Y | N | Y | Y | **Never** | — | trap |
| `SEA-020` | Terminal / Tap to Pay / SoundBox | N | Y | P | N | P | N | **Never** (Aura System C) | — | trap |
| `SEA-021` | Online store / website builder | N (killed) | Y | N | N | ₱349 storefront | N | **Never** | — | CMS trap |
| `SEA-022` | Global Accounts / Yield / staff cards | N | N | P | N | P | Y | **Never** | — | treasury trap |
| `SEA-023` | Rapyd/Airwallex-style issuing + wallets | N | N | P | N | P | Y | **Never** | — | BaaS trap |
| `SEA-024` | Compete on MDR / become acquirer | N | Y | Y | Y | Y | Y | **Never** | — | license trap |

### Suggested Hub sequence (not Aura Wave 0)

0. **Locks:** `SEA-010`, `SEA-019`–`024` written as Never in the tracker so nobody “just adds xenPlatform.”
1. **Debt + HitPay-shaped surface:** `SEA-004` (stop lying about Xendit), `SEA-001`, `SEA-009`.
2. **Wedge:** `SEA-003` (invoice that files), `SEA-002` as the commercial shell, `SEA-007`/`SEA-008` if CHIP/Xendit MIT is real, `SEA-014`.
3. **Demand-gated:** `SEA-005` HitPay adapter, `SEA-006` Fiuu, `SEA-016` portal.
4. **Expansion:** `SEA-011`, `SEA-012`, `SEA-015`, `SEA-017`.
5. **ID:** `SEA-013` only with a tax plan.
6. **Nice:** `SEA-018` UBB after Airwallex-shaped tenants exist.

### Promotion rule

A `SEA-*` row may be copied into `00-checklist-tracker.md` when:

1. It is a job a **MY merchant or Aura integrator** can say in one breath, and
2. It does not violate `PY-022` / System A-B-C, and
3. The owner is **Hub/Lazuar**, not Aura calendar.

Until then this file is the SSoT for SEA platform comparison. Do not summarize it into a slide that says “we should be HitPay.” The slide, if one is ever needed, is:

> **Be the compliance and subscription brain. Plug into HitPay/Xendit/Billplz. Never become them. Finish the invoice that files. Build the Xendit adapter the README already promised.**

---

## Appendix — primary sources fetched 16 August 2026

| Source | URL |
| --- | --- |
| Xendit home / MY / pricing / Payment Links / Subscriptions | xendit.co/en/ · /en-my/ · /en/pricing/ · /en/products/payment-links/ · /en/products/subscriptions/ |
| Xendit xenPlatform overview | docs.xendit.co/docs/xenplatform-overview |
| Xendit subscriptions how-it-works | docs.xendit.co/recurring |
| Xendit invoice → Payment Session migration (legacy `/v2/invoices`) | docs.xendit.co/docs/migrate-to-payment-session |
| Midtrans pricing / recurring / home | midtrans.com/pricing · /features/recurring-payment · /en |
| Midtrans BI-SNAP Core API hosts | docs.midtrans.com/reference/core-api-snap-open-api-overview |
| HitPay MY + SG pricing | hitpayapp.com/my/pricing · /sg/pricing |
| HitPay invoicing / links / recurring | hitpayapp.com/invoicing · /payment-links · /recurring-billing |
| HitPay API + webhooks | docs.hitpayapp.com/apis/overview · /apis/guide/events |
| PayMongo pricing + subscriptions | paymongo.com/pricing · /products/accept-payments/subscriptions |
| 2C2P + methods | 2c2p.com · 2c2p.com/payment-methods/ |
| Antom | antom.com |
| Airwallex SG + pricing | airwallex.com/en-sg · /en-sg/pricing |
| Wise Business SG pricing | wise.com/sg/pricing/business |
| Aspire vs Wise (Jul 2026 comparison) | aspireapp.com/compare/aspire-vs-wise |
| Rapyd payments / payment / subscription docs | rapyd.net/products/payments · docs.rapyd.net/en/payment.html · /en/subscription.html |
| BNM DuitNow QR MDR note (2023, still the public policy text) | bnm.gov.my/-/duitnow-qr-payments |
| Lazuar: README, ADR 019/021/023, product-lines, events, Lhdn README, PaymentGatewayFactory | this workspace |

**Secondary / labelled only:** HitPay blog rate explainers; HitPay “best gateway MY 2026”; third-party PayMongo historical MDRs (superseded by 16 Aug 2026 official card); UK Rapyd fee blogs; TechCrunch/Forbes 2022 Ant–2C2P ownership.

End of chapter 06.
