# 18 — Pricing, onboarding, compliance trust

**Program:** `plans/007-feats` — competitor-feature research  
**Subject product:** **Lazuar Pay** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`), also sold as Lazuar Hub / Checkout-as-a-Service. Aura is a first-party *integrator*, not the product under review.  
**Scope:** Commercial models (take-rate vs SaaS vs prepaid credits), time-to-first-checkout, KYC/BYOK trust, PDPA / privacy / terms / refund, SLA / status / support, sandbox, self-serve vs sales-led, partner/affiliate (Wise MassPay), checkout trust badges, SST on **our own** platform fee.  
**Date of research:** 2026-08-16  
**Status:** Full uncondensed analysis. Not a commitment to ship. Not legal advice. Not a claim that production guest fulfillment on Aura is proven.  
**Does not implement product code.**

Standing constraints (do not contradict):

- Guest / merchant GMV on BYOK rails is **not** platform SaaS money. Hub is **not** Merchant of Record for guest GMV (ADR 019, architecture-who-does-what M1/M6).
- Aura salon Pro (RM 149 / 1,490) is **Paddle MoR** and lives **outside** Hub (`apps/lazuar-docs/docs/guide/product-lines.md`).
- Do **not** introduce a platform take-rate on merchant GMV to “match” Paddle / Lemon Squeezy / Fresha / Gumroad Discover (tracker `SA-008`, `PY-022`, `XX-003`).
- ADR 020 Phase 3 **Wise MassPay / PayPal Payouts / Tremendous** affiliate mass-pay is a **later wishlist**. **Refuse for now.**
- ADR 020 Phase 3 **Singpass / MyDigital ID / Aadhaar** national KYC is a **later wishlist**. Hub avoids KYC **today** because BYOK pushes KYC onto Billplz / Stripe / CHIP / Razorpay.
- ADR 023 “UI lobotomy” hid LHDN / TIN / tax-invoice UI. Backend LHDN and the prepaid wallet still exist. Do not market “Compliance CaaS is live in Ops” until Phase D.3 remounts those routes.
- Production Aura guest soak is **not** claimed (`00-checklist-tracker.md` header; `PY-008` still none). Sandbox three-book soak is the honesty gate.

Companion files (do not re-litigate salon-OS SST/PDPA/trial here):

| File | Boundary |
|------|----------|
| `19-compliance-pricing-onboarding.md` | **Aura** salon packaging (Paddle RM 149, 7-day trial, SST *on tickets*, PDPA on `/book`) |
| `12-payments-deposits-rails.md` | Aura guest deposits / rails / refunds honesty |
| `00-checklist-tracker.md` | Living SA / CP / ON / PY cells |
| `20-sequencing-and-tracker-schema.md` | Wave and ID rules |

This file is about **Lazuar Pay as a commercial product** that Aura (and any other app) consumes.

---

## Method

### What was inspected (Pay repo, 2026-08-16)

Absolute paths unless noted.

| Concern | Path |
|---------|------|
| Product thesis (BYOK, prepaid wallet, no 8% take) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` |
| CaaS pivot | `docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` |
| Compliance CaaS / 3 pillars | `docs/architecture-decision-log/021-compliance-caas-pivot.md` |
| Integration wishlist (Wise, KYC, escrow) | `docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` (watermark: Phase 2/3 **not** Phase C) |
| Pure CaaS UI hide | `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` |
| Domain split | `docs/architecture-decision-log/016-platform-domain-strategy.md` |
| Ops login + **self-serve signup** | `apps/lazuar-ops/src/components/LoginPage.tsx` |
| Ops routes (ADR 023 unrouted islands) | `apps/lazuar-ops/src/App.tsx` |
| Ops sidebar (what humans actually see) | `apps/lazuar-ops/src/components/Sidebar.tsx` |
| BYOK vault UI | `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` |
| Credit wallet UI | `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` |
| Utility ledger UI | `apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx` |
| Legal / TIN / SST profile (unrouted) | `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` |
| API keys + Payments integrator preset | `apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx` |
| Outbound webhooks | `apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx` |
| Public register + cookie | `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` |
| Register creates workspace + 5 entitlements | `apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` |
| Starter grant 50 credits | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/StarterCreditSeederHandler.cs` |
| Credit packages + costs | `apps/lazuar-api/src/Lazuar.Api/appsettings.json` (`Credits`) |
| Top-up min RM 50, system checkout | `apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminCreditsEndpoints.cs` |
| Top-up grant + ledger (no SST line) | `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformTopUpEventHandler.cs` |
| Admin login (no signup) | `apps/lazuar-admin/src/components/LoginPage.tsx` |
| Admin = platform gateway vault only | `apps/lazuar-admin/src/App.tsx`, `.../PlatformPaymentSettingsPage.tsx` |
| Buyer legal pages | `apps/lazuar-portal/src/app/legal/{terms,privacy,refund}/page.tsx` |
| Checkout consent + “direct transaction with Creator” | `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` |
| Portal landing (lock icon, magic-link only) | `apps/lazuar-portal/src/app/page.tsx` |
| Provision / Connect | `apps/lazuar-docs/docs/integrations/provision.md` |
| Environments / Billplz sandbox vs live | `apps/lazuar-docs/docs/integrations/environments.md` |
| Who-does-what / not-MoR | `apps/lazuar-docs/docs/guide/architecture-who-does-what.md` |
| Product lines (Paddle stays outside) | `apps/lazuar-docs/docs/guide/product-lines.md` |
| Billing module honesty (wallet vs ledger, LHDN 3+1) | `docs/001-gaps/05-billing-module.md` |
| Aura Connect runbook | Aura repo `apps/aura-docs/docs/ops/settings/lazuar-pay.md` (first-party consumer; not edited here) |
| Aura provision hatch | Aura repo `services/lazuar-api/src/Lazuar.Api/Infrastructure/HubPayments/HubWorkspaceProvisioner.cs` (read-only context) |

### What was fetched (competitor commercial pages, 2026-08-16)

| Competitor | URL / source | Role vs Pay |
|------------|--------------|-------------|
| **Paddle** | https://www.paddle.com/pricing (fetched) | MoR take-rate, 5% + 50¢, 24/7 buyer support, tax remittance |
| **Lemon Squeezy** | https://www.lemonsqueezy.com/pricing (fetched); docs fees | MoR 5% + 50¢; +1.5% intl; +1.5% PayPal; +0.5% subs; payout 1% non-US |
| **Polar** | https://polar.sh/docs/merchant-of-record/fees (fetched) | MoR Starter 5% + 50¢; Pro $20 / 3.8% + 40¢ … Scale $400 / 3.4% + 30¢ |
| **Gumroad** | https://gumroad.com/pricing (search + corroboration) | 10% + 50¢ direct; **30% Discover** marketplace |
| **Chargebee** | https://www.chargebee.com/pricing/ (search, 2026 writeups) | SaaS billing **on top of** a PSP; Starter $0 to $250k lifetime; Performance ~$599/mo |
| **Stripe / Connect** | https://stripe.com/pricing, https://stripe.com/connect/pricing | PSP + optional platform application fee; Connect Express/Custom ~$2/acct + 0.25% + 25¢ payout |
| **Billplz** | https://main.billplz.com/pricing (fetched) | MY pipe: FPX B2C **RM 1.25 / 0.75**; cards 1.8% / 1.5%; wallets 1.5%; Basic free / Standard **RM 999 / year** |
| **HitPay MY** | https://hitpayapp.com/pricing (fetched, MY locale 14 Aug 2026) | Zero monthly; domestic cards **1.2% + RM 1**; intl 3% + RM 1; T’nG 1.9%; +0.2% software add-ons |
| **Xendit** | https://www.xendit.co/en-id/pricing/ + help 1 Jul 2026 | SEA aggregator; MY example Alipay+ **2.50% + MYR 0.90**; monthly min / dormancy fees exist |
| **CHIP** | https://www.chip-in.asia/ + terms | MY collect; **quote-led** fees in onboarding email; no public rate card |
| **CardUp Collect** | https://www.cardup.my/business/collect/pricing | Explicit **“All CardUp fees … subject to an additional 8% SST”** |
| **Stripe SST MY** | support.stripe.com taxes-on-stripe-fees-for-malaysia-based-businesses | Processing fees: Stripe MY **no longer** charges SST; non-processing products still may |
| **RMCD / SST digital** | MySST, SToDS, Commenda / Anrok / Kintsugi 2026 guides | 8% service tax; **RM 500,000** 12-month threshold; SaaS / payment processing named |

Salon-OS commercial numbers used only as **adjacent** pressure (Fresha Independent MYR ~67.95 / Team per seat + 20% marketplace; Aura Pro RM 149) — full argument lives in `19`.

### Honesty rules used throughout

1. **Code and ADRs win** over README Phase 1/2/3 marketing (“15 apps”, “WhatsApp dunning is current”, “LHDN at checkout”).
2. **Three money desks** must stay named: (A) salon/creator → platform SaaS or credits; (B) guest → merchant via BYOK; (C) desk cash, never Hub.
3. **A legal page for buyers is not a merchant DPA.**
4. **A 99.9% sentence in TOS is not an SLA.**
5. **`sk_test_` is not a sandbox product** if Billplz environment follows `App:ApiBaseUrl` containing `lazuar.com`.
6. **Avoiding KYC is a feature and a trust hole at the same time.**
7. **Credits are not double-entry unearned revenue** until Billing is fixed (`05-billing-module.md`).
8. Rates below are **public pages on 2026-08-16**. Gateways change rate cards; quote the URL, not folklore.

---

## Competitor commercial models

Every checkout / billing / rail vendor monetizes **one or more** of four levers. Lazuar Pay’s thesis is a deliberate **refusal** of lever 1 on GMV, a **half-built** lever 2, and a **config-only** lever 3.

| Lever | What the vendor taxes | Typical public form (2026) |
|-------|----------------------|----------------------------|
| **1. Take-rate / MoR / MDR** | Gross merchandise or processing | 5% + 50¢ (Paddle/LS/Polar Starter); 10%+50¢ (Gumroad); RM 1.25 FPX (Billplz Basic); 1.2%+RM1 cards (HitPay) |
| **2. SaaS seat / location / plan** | The software, independent of GMV | Chargebee $599/mo; Billplz Standard RM 999/year; Aura Pro RM 149; Fresha Team per bookable |
| **3. Credits / usage** | Discrete expensive API actions | Twilio / Meta WA / OpenAI tokens; **Pay `TenantCreditBalance`** |
| **4. Marketplace tax** | New customers the platform sourced | Gumroad Discover 30%; Fresha new-client 20% min USD 6; Treatwell / Booksy Boost |

A fifth lever — **float** (hold funds, earn interest, delay payout) — is how some MoRs and payfacs make the take-rate feel cheaper. BYOK **gives this lever away** on purpose: money never sits in Lazuar’s account.

### 1. Merchant-of-Record take-rate (Paddle, Lemon Squeezy, Polar, Gumroad)

These vendors **are** the seller of record. The buyer’s card statement says Paddle / Lemon Squeezy / Polar / Gumroad. The vendor files VAT/SST/sales tax, fights chargebacks, and often answers buyer billing tickets.

**Paddle** (https://www.paddle.com/pricing, fetched 2026-08-16):

- Headline: **5% + 50¢ per Checkout transaction**. No monthly fee on pay-as-you-go.
- Custom pricing for scale; products under **$10** or invoicing need a demo.
- Marketing comparison table: DIY PSP stack “~7% and above” once you add tax filing (0.5%), dunning add-ons, international cards (up to 4.4%), fraud (up to 0.4%), localized checkout (2.9%).
- Includes: branded checkout, subscriptions, multi-currency, tax remittance, fraud, **buyer billing support 24/7**, 93% CSAT claim, migration.
- Implicit GTM: **self-serve signup** at `login.paddle.com/signup` **and** sales-led custom.

This is the model Aura **already buys** for System A (salon pays Aura). It is the model Pay **explicitly rejected** for System B (guest pays salon) in ADR 019:

> Unlike Lemon Squeezy or Paddle (which act as MoRs, taking 5-8% of revenue and holding funds), Lazuar operates strictly as BYOK software.

**Lemon Squeezy** (https://www.lemonsqueezy.com/pricing; Stripe-acquired; 2026 page still 5% + 50¢):

- No monthly ecommerce fee; email marketing $0 to 500 subscribers then usage.
- Extra fees (docs): **+1.5% international (non-US)**, **+1.5% PayPal**, **+0.5% subscription payments**, abandoned-cart recovery cut in some writeups, affiliate-referred **+3%** in secondary sources.
- Payouts: **0% US bank**, **1% non-US bank** (cut after Stripe acquisition).
- Self-serve, no card to start, 24–48h support, MoR tax filing included.
- Affiliates are a **first-class marketing SKU** on the public nav (`/marketing/affiliates`). That is the opposite of Pay’s “refuse Wise MassPay for now.”

**Polar** (https://polar.sh/docs/merchant-of-record/fees):

| Plan | Monthly | Per txn | Notes |
|------|---------|---------|-------|
| Early Member (orgs before 27 May 2026) | $0 | 4% + 40¢ + 0.5% subs | Grandfathered; lost if you upgrade then downgrade |
| **Starter** (new orgs) | $0 | **5% + 50¢** | Matches Paddle/LS headline |
| Pro | $20 | 3.8% + 40¢ | Breakeven ~$1,379 / mo sales |
| Growth | $100 | 3.6% + 35¢ | ~$5,634 / mo |
| Scale | $400 | 3.4% + 30¢ | ~$19,048 / mo; Slack + SSO |

Add **+1.5% international cards**. Refunds do **not** return Polar’s cut. Disputes **$15**. Payouts are **Stripe pass-through** ($2/mo active + 0.25% + $0.25 + FX). Polar can refund buyers at its discretion for 60 days to protect the MoR chargeback rate.

Polar is the closest **indie-dev** cousin to Pay’s intended ICP — and it still takes 5% because **trust + tax + chargebacks** are the product.

**Gumroad** (https://gumroad.com/pricing):

- Direct: **10% + 50¢**.
- Discover marketplace: **30%**.
- $0 monthly. MoR tax “at no extra cost.”
- March 2026 writeups: payout minimum raised to **$100**.
- This is lever 1 **plus** lever 4. Pay must never grow a “Discover” to make credits feel cheap.

**Worked math (why Pay’s thesis exists):**

Assume a Malaysian creator sells a **RM 200** digital product, 200 sales / month = **RM 40,000 GMV**. Buyer pays FPX or card.

| Collector | Platform cut on GMV | Typical rail fee (merchant) | Creator keeps (order of magnitude) |
|-----------|--------------------:|----------------------------:|------------------------------------|
| Paddle / LS / Polar Starter | 5% + 50¢ ≈ **RM 10 + FX** per RM 200 (USD-denominated fee; MYR effective higher after FX) | bundled | ~**RM 37,000–38,000** before FX pain |
| Gumroad direct | 10% + 50¢ | + card 2.9% in some interpretations | ~**RM 35,000** |
| **Pay + Billplz Basic FPX** | **RM 0** | **RM 1.25 × 200 = RM 250** | **~RM 39,750** |
| Pay + HitPay domestic card | RM 0 | 1.2% + RM 1 = RM 3.40 × 200 = **RM 680** | **~RM 39,320** |
| Pay credits (say 200 LHDN submits × 3 cr × RM 0.10) | **RM 60** usage | rail as above | still ~RM 39k |

The 5% MoR bill on RM 40k is **~RM 2,000+**. Pay’s credit stack at current list is **two orders of magnitude smaller** if the tenant only buys LHDN/WhatsApp units. That is the entire commercial argument in ADR 019 §3.

The catch, which MoRs will say in every sales call: **Pay does not file the buyer’s SST/VAT, does not eat chargebacks, does not answer “where is my invoice,” and does not KYC the seller.** Those costs reappear as **trust, support, and legal** work — the rest of this file.

### 2. Processor / aggregator MDR (Billplz, HitPay, CHIP, Xendit, Stripe)

These vendors are **pipes**. They KYC the merchant, hold or settle funds, and charge per successful payment. They are **not** Checkout-as-a-Service (no multi-tenant vault + normalized `payment.*` + LHDN wallet). They are what Pay **wraps**.

**Billplz** (https://main.billplz.com/pricing, fetched):

| | Basic | Standard (RM 999 / year) |
|--|------:|-------------------------:|
| FPX B2C | **RM 1.25** | **RM 0.75** |
| FPX B2B | RM 3.00 | RM 2.00 |
| FPX payout | Next business day | Next business day |
| Card MYR | 1.8% | 1.5% |
| Card non-MYR | 3.8% optional | 3.5% optional |
| Auto-deduct MYR | 2.3% + 1.25 | 2% + 0.75 |
| Wallets (DuitNow QR, TnG, Boost, Grab) | 1.5% | 1.5% |
| Atome | 6% | 6% |
| DuitNow Transfer (payout product) | RM 1.25 | RM 0.75 |
| Shopify plugin | +0.3% | +0.3% |

Trust marketing on the same page: PCI DSS, bank-level encryption, next-day FPX “fully guaranteed,” logos (PDRM, Perodua, Boost, Wahed, LZS, Pandora, PTPTN).

Onboarding: **SSM + bank account** (Billplz FPX guide, Oct 2025). That **is** KYC. Time-to-first-live-FPX is **days**, not minutes, for a new Sdn Bhd. Sandbox is faster.

**HitPay Malaysia** (https://hitpayapp.com/pricing, fetched 14 Aug 2026):

- **No setup, no monthly** on the payments SKU.
- Domestic cards (online / recurring): **1.2% + RM 1**.
- International cards: **3% + RM 1** (+2% if foreign-currency presentment).
- Recurring extras: ShopeePay 2.2%, GrabPay 2%, Touch ’n Go **1.9%**.
- Software add-ons (links, invoicing, online store, POS, recurring): **+0.2%** each — a tiny take-rate on the *software*, not a MoR.
- Payouts: non-card T+2; cards from T+1, compliance-dependent.
- Chargebacks: amount + fee pulled; won disputes credited back. Fraud monitoring “included.”
- Reseller / refer-a-merchant programs on the public nav — **affiliate as GTM**, not as a payout-API product.
- **Sandbox** is a documented developer surface (`docs.hitpayapp.com` sandbox).
- License copy: BNM approved MSB agent / registered merchant acquirer. That sentence is a **trust badge Pay cannot copy** without becoming a payfac.

**CHIP Collect**: public site sells “no setup fee,” next-day, Shariah financing (CHIP Advance). **Fees live in the onboarding email** (`chip-in.asia/terms-of-service`). Sales-led rate card. Pay already has a CHIP adapter + “Brand ID + Secret Key; we fetch RSA and set webhooks” UX — commercially CHIP is a **BYOK target**, not a pricing peer.

**Xendit** (SEA, pricing calculator + 1 Jul 2026 help): processing fee + method fee; MY Alipay+ example **2.50% + MYR 0.90**; Indonesia VA/QR has IDR fixed legs + VAT; monthly maintenance / dormancy minimums appear in 2026 blog copy (USD 250 / USD 50 class). Xendit is the **regional aggregator** Pay’s README Phase 1 names (ID). Onboarding is KYC-heavy (KTP/NPWP, SSM in MY).

**Stripe** (global cards; MY has FPX): US headline still **2.9% + $0.30** online. Stripe’s own FPX explainer (Jan 2026) does not put a MYR number on the marketing page the way Billplz does; Airwallex’s 2026 MY fee guide still quotes Stripe FPX as **3% + RM 1** — treat as *indicative*, confirm on the live Stripe MY dashboard. **Connect**: Standard = Stripe bills the connected account (platform $0 processing markup unless you add an application fee); Express/Custom ≈ **$2 / active account / month + 0.25% + 25¢ per payout**. Connect is how Fresha-class products take a **second** cut. Pay’s vault is **BYOK secret paste**, not Connect onboarded accounts. That is why Pay has **no KYC form**.

**CardUp Collect** (useful only for SST honesty): domestic cards 2.25% (promo 1%), FPX 0.2% or RM 2–2.50, and in bold: **all fees + 8% SST**. This is the sentence Pay’s credit top-up is missing.

### 3. SaaS billing platforms (Chargebee, Recurly, Stripe Billing)

These **do not** want to be MoR. They sell **subscription state machines** and invoice UX. The merchant still has a Stripe/Adyen account.

**Chargebee** 2026 public shape (pricing page + 2026 explainers):

- **Starter:** $0 until **$250k cumulative lifetime billing**, then **0.75%** overage. Threshold never resets.
- **Performance:** ~**$599 / month** ($7,188/year, annual commit) covering ~$100k / month billed, 0.75% over.
- **Enterprise:** custom. CPQ / RevRec / Retention are **separate SKUs**.

This is lever 2 with a **usage kicker**. It is what Pay’s Commerce + Dunning module *aspires* to be, except Chargebee does not pretend to file LHDN and does not wrap Billplz.

**Stripe Billing / Tax** add 0.4–0.5% class add-ons on top of processing. That stack is the “~7% DIY” Paddle attacks.

Pay today is **not priced like Chargebee**. There is **no Hub Pro SKU**. The only sold unit in code is **credit packs**.

### 4. Marketplace take-rate (Fresha, Gumroad Discover, Treatwell)

Documented at length in `04-fresha.md` / `08` / `19`. Relevant here only as a **trap**:

- Fresha 2026: Independent **USD 19.95 / mo** (≈ MYR 67–95), Team **USD 14.95 / bookable**, marketplace **20% one-time on new** (min USD 6), processing **2.79% + $0.20** online.
- Gumroad Discover **30%**.

If Pay ever “helps creators get discovered” or “helps salons get clients,” the company shape flips and the prepaid-wallet thesis dies. **Refuse.** Tracker `XX-001`, `XX-003`, `ON-006` (partner portal) must not become a covert marketplace.

### 5. Informal MY stack (WhatsApp + Billplz link + Excel)

The real alternative for Pay’s *creator* ICP and Aura’s *salon* ICP:

- Software: **RM 0**.
- Rail: Billplz payment link or DuitNow sticker QR.
- Trust: the seller’s Instagram and a bank name on the receipt.
- Time-to-first-checkout: **already live**.
- Compliance: none. LHDN is a spreadsheet in December.

Pay cannot beat this on price. Pay beats it on **normalized webhooks, multi-tenant vault, dunning, and (later) LHDN**. If those are hidden (ADR 023) or broken (B2C consolidation filter, LHDN 3+1 credits), the informal stack wins.

### 6. Competitor onboarding + trust surfaces (what buyers see before they pay)

| Vendor | Signup | KYC | Legal | Status / SLA | Sandbox | Support |
|--------|--------|-----|-------|--------------|---------|---------|
| Paddle | Self-serve + demo | MoR underwrites the *buyer* risk; seller still KYC’d | Full TOS, privacy, DPA, tax docs | Status + 24/7 buyer support | Test transactions | In-product + buyer desk |
| Lemon Squeezy | Self-serve, no card | Seller KYC for payouts | Privacy, terms, **DPA download** | Help center; 24–48h | Test mode | Help + sales tour |
| Polar | Self-serve | MoR / Stripe KYC | Public fees page is itself a trust artifact | Docs-first | Test | Plan-gated Slack at Scale |
| Billplz | Self-serve dashboard | **SSM + bank** before live FPX | Pricing + PCI claim | No public status page found | **billplz-sandbox.com** | Email / chat; Enterprise AM |
| HitPay | Self-serve + sales | BNM-regulated KYC | TOS, privacy, AUP, MSA, license page | Docs sandbox | Yes | Help center + sales |
| Stripe | Self-serve | Full KYC / KYB | Massive legal center | status.stripe.com, SLAs on Enterprise | Test keys | Famous docs |
| Chargebee | Trial + sales | N/A (PSP does KYC) | Trust/security pages | Status page | Test site | Success managers |
| **Lazuar Pay (this repo)** | Ops **Sign up** (email + slug + password) | **None at Hub** | **Buyer-only** TOS/privacy/refund (June 2026) | **99.9% sentence, no status host** | `is_test_mode` + Billplz env **coupled to hostname** | `privacy@lazuar.com` on privacy page; **no tickets product** |

### 7. What “good” looks like that Pay should steal vs refuse

**Steal (pattern, not company-shape):**

- Polar’s **public fee table** (even if our number is “RM 0 GMV + credit packs”).
- CardUp’s **SST-on-our-fee** sentence.
- HitPay / Stripe **sandbox as a named product**.
- Lemon Squeezy **DPA** as a PDF, not a mailto.
- Billplz **PCI + next-day payout** badges **on the gateway page the buyer actually sees** (Pay should *relay* “You are paying Acme via Billplz,” not fake PCI).
- Paddle’s **explicit MoR vs PSP comparison** — inverted: “We are the PSP orchestrator; your Billplz *is* the merchant.”

**Refuse:**

- 5% GMV “to keep it simple.”
- Discover / Boost / affiliate network that pays itself from GMV.
- Stripe Connect application fees as a quiet take-rate.
- Wise MassPay as a 2026 SKU (`ON-006` later, spreadsheet first).
- National digital KYC as a checkout blocker before BYOK is loved.

---

## Our monetization thesis

### 1. The sentence in the README (still the north star)

From `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md`:

> We do not act as a Merchant of Record (MoR) and we do not take 8% transaction fees. You plug in your own Stripe, Billplz, or CHIP API keys. Money flows instantly to your merchant accounts.

> Automated compliance tasks (LHDN XML e-Invoicing) and retention actions (WhatsApp dunning messages) deduct micro-credits from a prepaid `TenantCreditBalance` wallet. This allows Lazuar to monetize infrastructure usage heavily without taxing the creator's gross sales volume.

ADR 019 repeats it: **flat SaaS fee + prepaid utility wallet**. ADR 021 then says the *long-term* price of that wallet is “replacing a $2,000/mo accounting department,” i.e. **high ARPU on compliance**, not on GMV.

ADR 023 postpones the compliance UI so the **first cash** is “checkout links + dunning email.” The wallet is therefore **currently monetizing a product the sidebar does not show**.

### 2. What is actually sold in code (2026-08-16)

There is **no** Hub plan catalogue equivalent to Aura `PRO_MONTHLY`. Grep of Ops + Billing endpoints shows **credits**, not seats.

**Starter grant** (`Credits:StarterGrant` = **50**, `StarterCreditSeederHandler`):

- Fires on `AppEntitlementGrantedIntegrationEvent` when `AppId == "BILLING"`.
- Public register grants entitlements `OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN` (`RegisterPublicUserCommand`).
- Idempotent: skip if a wallet row exists.
- Ledger reference: `"Starter credits (free grant)"`.
- **Not shown** on the signup form. The human does not know they have 50 credits unless they deep-link `/workspace/billing`.

**List packages** (`appsettings.json` → `GET /admin/billing/credits/packages`):

| Pay (MYR) | Credits | RM / credit | LHDN submit @ 3 cr | WA send @ 2 cr |
|----------:|--------:|------------:|-------------------:|---------------:|
| 50 | 500 | **0.100** | RM 0.30 | RM 0.20 |
| 100 | 1100 | **0.091** | RM 0.27 | RM 0.18 |
| 200 | 2500 | **0.080** | RM 0.24 | RM 0.16 |

Volume discount is real (~20% from smallest to largest pack). Minimum top-up **RM 50** (`AdminCreditsEndpoints`).

**Unit costs** (`Credits:Costs`):

| Action | Credits | Config key | Product reality |
|--------|--------:|------------|-----------------|
| WhatsApp send | 2 | `WhatsAppSend` | `Messaging:WhatsAppEnabled=false`. Dunning demotes WA steps to email (`DunningEngineJob.Dispatch`). **You cannot spend this SKU in production.** |
| LHDN submit | 3 | `LhdnSubmit` | Command path deducts 3 with idempotency. `LhdnDocumentSubmittedIntegrationEventHandler` **also deducts 1** with no idempotency if not test mode (`05-billing-module.md`). Live submit can cost **4**. UI for LHDN is **unrouted** (ADR 023). |

**Top-up path (platform money, not merchant GMV):**

```
Ops POST /admin/billing/credits/top-up { amount_myr, return_url }
  → GenerateSystemCheckoutSessionQuery
  → Platform gateway vault (admin /platform/payment-config)
  → Guest/ops browser pays Billplz/Stripe **as Lazuar**
  → GatewayPaymentCompleted { metadata.type = utility_credit_topup, tenant_id }
  → PlatformTopUpEventHandler
       wallet.TopUp(highest package with AmountMyr <= AmountPaid)
       ledger SYSTEM_CREDIT_TOPUP:
         EXPENSE_SOFTWARE_SUBSCRIPTION  +amount
         ASSET_CASH                     −amount
```

Implications:

1. Credit purchase is a **sale of software / prepaid utility by Lazuar**, not a pass-through of GMV.
2. Matching uses **≤ amount paid**. Overpay (RM 60) still grants the RM 50 pack (500 credits). Under-list amounts grant **0** (and still charged if someone bypasses the UI).
3. **No SST line** on the pack, the checkout description (`"Lazuar Utility Credits"`), or the ledger. Compare CardUp’s “+8% SST.”
4. Chargeback claws **wallet units** by re-running package match on `AmountDisputed`, not the originally granted credit count (`05-billing-module.md`).
5. `GatewayPaymentCompletedHandler` **also** posts a merchant-style `GATEWAY_PAYMENT` on the same event → **dual economic interpretation** of one cash movement. Financial summary can mix creator GMV with platform SaaS spend.

**SaaS flat fee:** named in ADRs, **not implemented** as a SKU. A tenant can run forever on 50 starter credits + BYOK checkouts **without paying Lazuar a sen**, because M2M cashier and Commerce checkout **do not debit the wallet**.

That is either:

- a **deliberate PLG wedge** (get them addicted to checkout, bill compliance later), or
- a **hole** (we forgot to charge for the thing people actually use).

ADR 023 says the former. A 2026 sales sheet that says “flat SaaS + credits” is **false** until a Hub plan exists or checkout itself consumes credits.

### 3. Three money desks (Pay-native restatement)

| Desk | Who pays whom | Collector in this repo | Exposed UI |
|------|---------------|------------------------|------------|
| **A1. Platform SaaS (Aura)** | Salon → Aura | **Paddle** in the Aura repo | Aura Settings → Plan. **Not Hub.** |
| **A2. Platform utility (Pay)** | Tenant → Lazuar | Admin **platform** BYOK (Billplz/Stripe) + credit wallet | Ops `/workspace/billing` (routed, **not in sidebar**) |
| **B. Guest / buyer GMV** | Buyer → Creator/salon | **Tenant** BYOK (`TenantPaymentConfiguration`) | Ops Payment Gateways + Portal checkout / M2M hosted page |
| **C. Desk cash** | Guest → salon in person | Aura POS | Never Hub |

Mixing A2 and B is the #1 bookkeeping bug (dual `GATEWAY_PAYMENT` + `SYSTEM_CREDIT_TOPUP`). Mixing B with a future take-rate is the #1 **company-shape** bug (`SA-008` / `PY-022`).

### 4. Why BYOK is the monetization, not just the architecture

BYOK is usually explained as engineering (adapters, vault, one webhook). Commercially it does four things:

1. **Keeps GMV off Lazuar’s balance sheet** — no payfac capital, no settlement delay as a business, no “where is my money” as *our* payout product.
2. **Pushes KYC, PCI, and chargebacks onto Billplz/Stripe/CHIP** — we do not staff a compliance ops team on day one.
3. **Lets us price below every MoR** on high-ticket MY FPX (RM 1.25 vs 5%).
4. **Creates a trust gap** the MoRs exist to fill: “If you don’t hold the money and you didn’t KYC me, why should my buyer trust this checkout?”

The prepaid wallet is the **intended** answer to (4) for B2B (“we file your tax”). Until LHDN is remounted, the honest answer is only: **localized FPX checkout + one webhook + dunning email.**

### 5. What we must not “fix” by copying competitors

| Temptation | Who does it | Why it breaks the thesis |
|------------|-------------|--------------------------|
| 2–5% application fee “just to cover Hub” | Stripe Connect, HitPay +0.2% software, Fresha processing | Becomes MoR-adjacent; 0% story dies; SST on *our* take of GMV |
| $0 Hub + processing markup | Old Fresha, Square | Same |
| Per-checkout credit (1 credit per pay) | Twilio-style | Taxes volume the way a take-rate does; punishes FPX micro-deposits (Aura RM 50) |
| Annual Billplz-like RM 999 membership as the only SKU | Billplz Standard | Fine as *optional* Hub Pro; must not be required before first sandbox pay |
| Affiliate % of GMV | LS affiliates, HitPay refer-a-merchant | Lever 4. Spreadsheet + INTERNAL comps only (`ON-006`) |
| Wise MassPay | ADR 020 §9 | Payout product + another KYC surface. **Refuse now.** |
| Escrow.com / BNPL / USDC | ADR 020 Phase 2/3 | High-ticket trust **or** become a money transmitter |

### 6. Recommended commercial shape (analysis, not a Paddle price change)

Keep **one public sentence**:

> **RM 0 on your sales. You pay Billplz/Stripe their rate. You buy Lazuar credits for tax filing and (when enabled) WhatsApp recovery. Optional Hub Pro later for SLA / SSO / extra workspaces — never a GMV tax.**

Until Hub Pro exists, the **true** sentence is:

> **RM 0 on your sales. Credits are sold for LHDN/WhatsApp; those products are mostly dark in the UI. Checkout itself is free software today.**

Publish that second sentence on a pricing page or do not claim the first.

**Credit list can stay.** It is the right meter for *asymmetric* cost (LHDN XML + IRBM + WhatsApp conversation). It is the wrong meter for `POST /checkouts`.

**SST on A2:** the day Lazuar’s taxable digital-service turnover crosses **RM 500,000** in 12 months (or earlier if already SST-registered for IT/digital), credit packs and any Hub Pro **must** show **8% SST** and an SST ID on the tax invoice. Build the line item **before** the threshold, even if the rate is 0, so we do not CardUp-blindside tenants.

### 7. Worked Pay P&L vs MoR (same RM 40k GMV creator)

Assumptions: 200 × RM 200 FPX; 30 LHDN B2B invoices / month; WhatsApp off; they already have Billplz Basic.

| Line | MoR (Paddle-class) | Pay + Billplz |
|------|-------------------:|--------------:|
| GMV | 40,000 | 40,000 |
| Platform take | ~2,000–2,400 (5%+fx+50¢) | 0 |
| Rail | bundled | 200 × 1.25 = 250 |
| Credits 30 × 3 × 0.10 | 0 (MoR invoice ≠ MyInvois) | **9** |
| **Creator net before COGS** | ~37,600 | **~39,741** |
| Who files buyer VAT in EU? | Paddle | Creator (or nobody) |
| Who files MyInvois? | Nobody | Pay *if* LHDN live; else creator |
| Who KYC’d the seller? | Paddle | Billplz |
| Who the buyer sues | Paddle + creator | **Creator only** (portal TOS §2) |

Pay wins **MY FPX margin**. Pay loses **global tax + buyer protection**. That split is the company. Do not blur it to close a Western SaaS deal.

---

## Onboarding path

Define clocks. They are different products wearing one logo.

| Clock | Meaning | Happy path owner |
|-------|---------|------------------|
| **TTFC-ops** | Register → first **Commerce** checkout URL that opens a gateway page | Human in `lazuar-ops` |
| **TTFC-m2m** | Integrator provision → first `POST /integrations/payments/checkouts` that returns `checkout_url` | Server + one human for BYOK |
| **TTFC-aura** | Aura Plan Connect/paste → first guest `/book` pay that **fulfills** | Aura owner + Pay workspace + Billplz |
| **TTFC-live** | First **non-sandbox** sen on a merchant account | Billplz/Stripe KYC, not Hub |

### 1. Surface map (what exists)

```
hub.lazuar.com / ops.lazuar.com     lazuar-ops     human tenant console
portal.lazuar.com                   lazuar-portal  buyer checkout + legal footer
admin.lazuar.com                    lazuar-admin   platform root (gateways for A2 only)
api.lazuar.com                      lazuar-api     register, provision, cashier, credits
…/docs                              lazuar-docs    VitePress integrator guides
…/docs (Scalar)                     lazuar-developers
```

ADR 016 locked this three-tier split. There is **no marketing site with a price card** in this monorepo. Portal `/` is a lock-icon dead-end: “use the magic link we emailed.” Acquisition is **docs + Aura + founder Slack**, not PLG landing.

### 2. Path S — Self-serve creator (Ops signup)

**UI:** `LoginPage` mode `signup`. Fields: workspace name, slug (3–63, `[a-z0-9]+(?:-[a-z0-9]+)*`, reserved set includes `api`, `admin`, `portal`, `billplz`, `stripe`, `lazuar`…), email, password, confirm. **No** TOS checkbox. **No** privacy checkbox. **No** card. **No** KYC. **No** phone. **No** company name.

**API:** `POST /one/public/register` → `RegisterPublicUserCommand`:

1. Reject duplicate email / taken slug.
2. Hash password (`PasswordWorkFactor` 11).
3. `GlobalUser` (`isSystemAdmin: false`).
4. `Organization(workspaceName, slug)`.
5. `TenantMembership(ADMIN)`.
6. Entitlements OPS, BILLING, PAYMENTS, CRM, LHDN → each publishes `AppEntitlementGrantedIntegrationEvent`.
7. BILLING handler seeds **50 credits**.
8. `IssueCookie` `lazuar_auth` (HttpOnly, Lax, 24h, Domain `.lazuar.com` in non-dev).
9. Browser → `/commerce/dashboard`.

**Email verification:** endpoints exist (`/auth/verify-email`, `/auth/resend-verification`, `/auth/forgot-password`, `/auth/reset-password`). **Ops UI has zero of them.** `Is_email_verified` is returned on `/auth/me` and ignored for access. Register does **not** enqueue a verify mail in the handler we read.

**What the sidebar then offers (ADR 023):**

- Commerce: Dashboard, Checkout Links, Subscribers, Transaction Logs, Promotions, Dunning Campaigns, Notification Templates.
- Developer: API Keys, Outbound Webhooks, Delivery Logs.
- Workspace: **General, Payment Gateways, Email Provider.**

**Not in the sidebar (but still routed):** `/workspace/billing`, `/workspace/ledger`.  
**Not routed:** Billing Profile (TIN/SST), Quotes, Tax Invoices, Credit Notes, Ops chat.

**TTFC-ops steps after cookie:**

| # | Action | Blocker if skipped |
|---|--------|--------------------|
| 1 | Create a Commerce product (checkout link) | No URL to share |
| 2 | Workspace → **Payment Gateways** → paste Billplz collection + API key + **128-char X-Signature** (or Stripe `sk_` + `whsec_`, CHIP Brand ID, Razorpay `KeyId:KeySecret`) and mark **active** | `422 PAYMENTS_NOT_CONFIGURED` |
| 3 | Optional: Email Provider (Resend) | May gate **Commerce** activation; **does not** gate M2M |
| 4 | Open `portal.lazuar.com/{slug}/checkout/{product}` as a guest | Needs public portal + product slug |
| 5 | Buyer pays on **Billplz/Stripe hosted page** | Sandbox vs live — see below |

**Honest TTFC-ops:**

- **If they already have Billplz sandbox keys in a password manager:** **15–40 minutes** (signup is 60 seconds; the 128-char key and collection ID are the slow human bits).
- **If they need a new Billplz account:** **hours to several business days** (SSM, bank, Billplz review). Hub cannot shorten this. Copy must say so.
- **If they only want a “pay at venue” style link:** Commerce still wants a gateway for non-zero prices. Zero-amount bypass exists (`is_zero_amount_bypass`) for 100% coupons — not a general “request only” product.

**Friction vs Polar / LS:** they take 5% so the seller **never pastes a 128-character hex string**. That paste **is** our onboarding. Improving the paste (CHIP’s “we fetch RSA and webhooks for you” is the right pattern) is worth more than a longer wizard.

### 3. Path I — Integrator M2M (Aura, sample cashier, any app)

Documented in `provision.md` + `payments-integration-quickstart.md`.

```
Integrator backend
  POST /api/v1/one/integrations/workspaces/provision
  Auth: X-Lazuar-Provision-Key | Bearer same | SUPER_ADMIN session
  Body: external_product, external_org_id, display_name, is_test_mode, webhook_url?, owner_email?
        → upsert (external_product, external_org_id)
        → first time: sk_test_/sk_live_ + scopes
             payments.checkouts:write|read, webhooks.endpoints:manage
        → optional whsec_ once
        → owner_email attaches existing user only (does not create users)
  Re-call: created=false, secrets null
```

Then **a human** still opens Ops on that workspace and pastes BYOK. Provision **does not** configure Billplz. `PAYMENTS_NOT_CONFIGURED` until they do.

Machine create:

```
POST /api/v1/integrations/payments/checkouts
Authorization: Bearer sk_test_…
Idempotency-Key
{ amount, currency, success_url, cancel_url, metadata }
→ checkout_url  (gateway hosted)
```

**TTFC-m2m** for a team that already has a Hub workspace + sandbox Billplz: **under an hour** (sample app `examples/hub-cashier-next` on :3020). For a new Aura salon using **Connect**: see Path A.

**Connect footgun (already in Aura docs and TODO):** Connect **creates a new empty workspace**. If Billplz already lives on workspace `lzr` and someone clicks Connect, Aura binds to `hello` (orphan). Docs say: **do not click Connect again; paste a key from the workspace that has the gateway.** This is the #1 time-to-first-*paid*-guest failure and it is an **onboarding product** bug, not a payments-adapter bug (`PY-005`).

`HubWorkspaceProvisioner` 503s with `CONNECT_NOT_AVAILABLE` if Aura’s `BaseUrl` / `ProvisionSecret` are empty — salon is told to contact support. Path A (manual paste) is the fallback.

### 4. Path A — Aura salon Connect / paste (first-party consumer)

From Aura’s Connect Lazuar Pay runbook (`apps/aura-docs/docs/ops/settings/lazuar-pay.md`):

**Preferred:** already have Pay + Billplz → mint **Payments integrator** key (no LHDN scopes) → paste `sk_*` on Aura Plan → Aura registers `https://aurabook.app/api/v1/webhooks/hub/payments` → rotate `whsec_` if Plan asks.

**First time:** signup on hub.lazuar.com **or** Connect → open Pay → add Billplz → Refresh status on Plan.

Two secret pairs, easy to swap:

| Pair | Where | What people paste by mistake |
|------|-------|------------------------------|
| Aura ← Pay | Plan + Pay Developer webhooks | Billplz X-Signature into `whsec_` |
| Pay ← Billplz | Pay Payment Gateways | Stripe `sk_live_` into Hub `sk_` field (both start with `sk_`) |

Aura docs already warn both. Pay’s API key UI should **label “Lazuar secret, not Stripe”** in the reveal dialog (today the copy is generic).

**TTFC-aura-staff (walk-in):** minutes (Aura sample catalog) — not Pay’s clock.  
**TTFC-aura-guest-pay:** **hours–days**, dominated by Billplz KYC + Connect/paste confusion + hop-1 public URL + hop-2 Aura webhook. Wave 0 soak (`PY-008`) is still the honesty gate; **do not advertise “online pay in 5 minutes.”**

### 5. Path P — Platform admin (A2 money)

`lazuar-admin` `/login`: email + password only. `POST /platform/auth/login`. No signup. Seeded local `admin@lazuar.com`. Lands on `/platform/gateways`.

This vault is **Lazuar’s own** Billplz/Stripe for **credit top-ups** (“Configure the root payment processors for utility credit top-ups across the ecosystem.”). If it is empty, tenants cannot buy credits even if their *merchant* BYOK works.

There is **no** admin UI in this app for tenant impersonation, credit grants, or KYC review. Superadmin can sign into Ops on the system workspace (README). Support is **SSH + SQL + Ops**, not a trust center.

### 6. Sandbox (named product vs hostname accident)

What exists:

- Provision `is_test_mode: true` → `sk_test_` bootstrap.
- API key toggle Test / Live in Ops.
- Docs: prefer test mode outside production.
- Billplz sandbox host `https://www.billplz-sandbox.com` **unless** `App:ApiBaseUrl` **contains `lazuar.com`**, in which case Hub calls **production** Billplz (`environments.md`, `TODO.md`, payments quickstart §8.3).

What that means commercially:

- A **production Hub** (`hub.lazuar.com`, `api.lazuar.com`, `pay.lazuar.com`) is one mis-set `App__BillplzEnvironment` (or a missing override) away from charging **live** bills while an integrator still thinks `sk_test_` means sandbox.
- Conversely, `sk_live_` against a non-`lazuar.com` Hub still hits **sandbox**. Aura is told to warn: “Billplz environment follows Hub base URL, not the key prefix.”
- Old Billplz bills **lock the callback URL**. Changing the tunnel does not retarget them.

**This is not a sandbox product.** Stripe’s test mode, HitPay’s sandbox docs, and Billplz’s dedicated dashboard are products. Pay has a **boolean and a hostname heuristic**. `PY-008` (three-book soak) cannot be an external customer ritual until this is a switch the tenant can see (“This workspace is TEST — bills go to billplz-sandbox.com”).

### 7. Self-serve vs sales-led

| Motion | Evidence | Verdict |
|--------|----------|---------|
| Self-serve Ops signup | Live, no card, no TOS | **Default.** Keep. Add TOS checkbox. |
| Self-serve integrator | Provision secret in env — **not** a public “Create app” console | **Sales/founder-led** for second apps. Sample app is the PLG. |
| Aura Connect | Button in Plan; 503 if unconfigured | **Assisted PLG.** Must not create orphan workspaces. |
| Admin | Invite-only | Internal |
| Enterprise AM / SLA / custom credits | No SKU, no page | **Do not pretend.** Polar Scale / Billplz Enterprise / Paddle custom are the templates *if* a whale appears |
| VIP onboarding RM 499 | Aura marketing only | Not a Pay SKU |

Do **not** add a mandatory demo wall (Boulevard-style) to look premium. The ICP that already has Billplz should finish TTFC-ops in one sitting.

### 8. Partner / affiliate (refuse Wise MassPay)

ADR 020 Phase 3 §9:

> **Mass Affiliate Payouts** — Wise MassPay, PayPal Payouts, Tremendous. Lazuar tracks affiliate links at checkout. On the 1st of the month, aggregate commissions and MassPay hundreds of affiliates.

`CommissionAccruedIntegrationEvent` has a Billing **handler and no publisher** (`05-billing-module.md`). The affiliate module is gone.

**Refuse for now**, because:

1. Payout APIs are a **second money transmitter** relationship (Wise KYC on *us*, plus affiliate KYC).
2. It invites a **marketplace-shaped** GTM (“invite 500 affiliates, we take 3% like Lemon Squeezy”).
3. Aura `ON-006` already says partner portal is Later Wave 10, **spreadsheet first**.
4. HitPay’s “refer a merchant” is a **sales bounty**, not a product. We can pay bounties on INTERNAL / bank transfer without MassPay.

If a creator asks “can my affiliates get auto-paid?” the answer is: **track in your app; pay from your Billplz/Wise; we will not be your MassPay.** Revisit only after TTFC-live and credit SST are boring.

### 9. Onboarding checklist we do **not** have

Aura has `ON-001` / `ON-002` (HQ form + widget). Pay has **none**:

- No “Add a gateway” empty-state on Dashboard that blocks the rest.
- No “Make a RM 1 sandbox bill” smoke button.
- No “copy your first checkout URL” celebration.
- Credits page not in the nav, so starter 50 is invisible.
- No email verify, so abandoned slugs and throwaway accounts are cheap.

Highest leverage is **not** a 12-step tour. It is: Dashboard empty state → Payment Gateways → “Create test checkout” → Delivery Logs. Three clicks. Gateway-first, LHDN never in v1 checklist (ADR 023).

### 10. Time-to-first-checkout scoreboard

| Path | Best case | Typical | Dominated by |
|------|-----------|---------|--------------|
| Ops + existing Billplz sandbox | **< 30 min** | 1–2 h | 128-char key, webhook mental model |
| Ops + new Billplz live | 1–3 **days** | 3–10 days | **Billplz KYC** |
| M2M + provision + existing workspace | **< 1 h** | half day | Secrets, public hop-1 URL |
| Aura Connect on empty Pay | 1 h to “configured” | **days** | Orphan workspace + Billplz + hop B |
| Polar / LS / Gumroad | **< 15 min** to first *test* card | same | They are the merchant |
| Informal Billplz link | **< 10 min** | already live | No Hub |

Pay will **never** beat Polar on TTFC-live for a US card seller. Pay can beat Polar on TTFC-ops for a **MY seller who already passed Billplz KYC** — that is the only race that matters.

---

## Trust/legal

Trust here is not “SOC 2 on the homepage.” It is: **will a Malaysian buyer complete FPX, and will a founder paste production gateway secrets into our vault?**

### 1. KYC — we avoid it because BYOK

**Fact:** Hub register collects email, password, workspace name, slug. No NRIC, no SSM, no bank, no liveness, no TIN (TIN UI is hidden). Provision attaches `owner_email` if the user already exists. Admin does not review tenants.

**Where KYC actually happens:**

| Actor | KYC? | When |
|-------|------|------|
| Billplz | Yes (SSM, bank, business) | Before **live** FPX |
| Stripe | Yes (KYB, bank, ID) | Before live charges / payouts |
| CHIP / Razorpay / Xendit | Yes | Live onboarding |
| Paddle (Aura System A) | Yes (MoR) | Before they pay *us* for Aura |
| **Lazuar Hub** | **No** | Never, by design |
| Buyer on Portal | No (name, email, optional phone/address) | TIN fields `[MVP-HIDE]` |

**Why this is correct commercially:**

- We are not a payfac. BNM e-KYC policy (PD-eKYC) applies to **licensed banks, insurers, MSB, designated payment-instrument issuers** — not to a software vault that never touches settlement.
- Forcing Singpass / MyDigital ID at *Hub* signup (ADR 020 §12) would **duplicate** Billplz and kill TTFC-ops for integrators who only need `sk_test_`.
- Chargebacks and fraud on GMV are **the merchant’s and the gateway’s**. Portal TOS §2 says so.

**Why this is a trust problem:**

1. **Buyers** see `portal.lazuar.com/{slug}/checkout/...` — a domain they do not know — then bounce to Billplz. If the slug is `pay-now-kl` registered 4 minutes ago, we look like a phishing kit. MoRs solve this by putting **their** well-known legal name on the card statement.
2. **Merchants** are asked to paste **live** `sk_live_` / Billplz secret into a console with no 2FA, no email verify, no SSO, no hardware key. Polar Scale sells SSO for a reason.
3. **Regulators / enterprise RFPs** will ask “what is your AML program?” The only honest answer is “we do not hold funds; processors KYC.” That answer **fails** some RFPs. **Win other RFPs.** Do not fake an AML program.
4. **Abuse:** self-serve + no verify + free 50 credits + LHDN entitlement = someone can use us as a document mill if LHDN UI returns. Fail-closed credits + rate limits are the control, not KYC theatre.

**Copy we should ship (not a badge):**

> Lazuar does not hold your customer’s money and does not replace your Billplz/Stripe onboarding. You complete KYC with your gateway. Buyers pay on that gateway’s page. We store encrypted keys and emit signed webhooks.

**Copy we must not ship:**

> “Bank-level KYC.” “BNM licensed.” “Zero-fraud checkouts via MyDigital ID” (README Phase 3). “PCI certified checkout” (PCI is the gateway’s AOC; our vault is a separate question).

**Later (not now):** optional “Verified merchant” flag that **reads** Billplz/Stripe account status via API (account is live, charges enabled) and shows a badge on Portal. That is **delegation**, not our KYC. National ID at checkout is Phase 3 and only for B2B TIN prefill when LHDN remounts.

### 2. Buyer legal pages (what exists)

Three Next.js articles, last updated **June 2026**, linked from portal footer and checkout.

**Terms** (`apps/lazuar-portal/src/app/legal/terms/page.tsx`):

- Audience: people who **purchase** via the portal.
- Lazuar is a technology platform; not a party to the creator transaction; not responsible for quality, delivery, legality.
- Claims and refunds **against the Creator**.
- **§3 Access and Uptime:** “strives to maintain **99.9%** platform uptime” but **not liable** for interruptions. No measurement window, no credits, no status URL.
- Access via **magic links**; user responsible for email security.
- **Governing law: Malaysia**; platform disputes in Malaysian courts; product disputes follow the Creator’s jurisdiction.

**Privacy** (`.../legal/privacy/page.tsx`):

- Names **PDPA 2010** and **GDPR**.
- Creator = **controller**; Lazuar = **processor**.
- Collected: name, email, phone (WhatsApp delivery), subscription/transaction history.
- Cards **never stored**; Stripe / Billplz.
- Sub-processors: **Resend**, **Meta (WhatsApp)**, **Cloudflare**. Does **not** name Billplz, Stripe, CHIP, R2/object storage, OpenRouter/AI, or the cloud region.
- Deletion: contact the Creator, or **privacy@lazuar.com** and we “formally forward.”
- No DPO name, no 72-hour breach clock (PDPA Amendment 2024 / 1 June 2025), no cookies table, no retention days, no cross-border clause, no children’s clause, no lawful bases list.

**Refund** (`.../legal/refund/page.tsx`):

- Title mentions “merchant of record information” then §1 says we are **not** MoR and **cannot issue refunds**.
- Default: **all sales final** unless the Creator promised otherwise.
- Subscriptions: cancel via magic-link Buyer Dashboard; stops **future** charges only.

**Checkout microcopy** (`CheckoutForm.tsx`):

> By proceeding, you agree to Lazuar's Terms of Service and Privacy Policy, and acknowledge that your purchase is a **direct transaction with the Creator**.

Good. No Visa/MC marks, no lock animation beyond “Securing Data…”, no Billplz logo, no “SSL Secure” badge. Order summary is price + coupon only — **no tax line** (TIN/company `[MVP-HIDE]`).

**Portal `/`:** padlock SVG + “Lazuar Secure Portal” + magic-link instruction. Fine for returning buyers; useless as a trust center.

### 3. Merchant legal pages (what does **not** exist)

Ops signup has **no** clickwrap. There is no:

- Merchant Terms of Service (AUP, prohibited businesses, we may freeze webhooks).
- Data Processing Agreement (processor terms, sub-processors, deletion, audit).
- Privacy notice **for account data** (the founder’s email, hashed password, vaulted keys).
- Acceptable Use (card-testing, adult, crypto, weapons — MoRs live and die by this).
- Security whitepaper / PCI responsibility matrix (“SAQ A because hosted fields are on Billplz”).
- Sub-processor register with change notification.
- Credit-pack terms (expiry? unused balance on account close? SST?).

HitPay publishes TOS + privacy + AUP + **Merchant Services Agreement** + license/registrations. Lemon Squeezy publishes a **DPA**. We have a **mailto**.

This is the largest **compliance-trust** gap that is fully inside our control (unlike BNM licensing).

PDPA 2010 + 2024 amendments (controller/processor duties, DPO thresholds, 72-hour breach, portability) apply to **Lazuar as processor** *and* as **controller of merchant account data**. The buyer privacy page does not cover the second role.

### 4. PDPA operationalization in product

| Need | Buyer path | Merchant path |
|------|------------|---------------|
| Notice at collection | Footer + checkout sentence | **Missing** at register |
| Consent (marketing) | Not collected; good (no blast product) | N/A |
| Access / correction | Creator CRM (if they use Commerce) | No self-serve export |
| Deletion | Forward via privacy@ | No tenant “delete workspace” product documented in this read |
| Portability | None | None |
| Security | JWT cookie, HttpOnly, TLS in prod, secrets masked in GET payment-config | **No 2FA**, no email verify, 24h cookie |
| Sub-processors | Partial, stale vs WhatsApp-off | Same page |
| Breach | Silent | Silent |
| Cross-border | Silent | Production region not productized (same honesty as Aura `19` §5) |

CRM module README claims GDPR/PDPA anonymization events — that is a **downstream hook**, not a public rights portal.

### 5. SLA, status, support

| Artifact | Reality |
|----------|---------|
| SLA | TOS 99.9% aspiration, **no credit**, no definition (API vs Portal vs webhook drain) |
| Status page | **None** in repo or docs (no status.lazuar.com) |
| Support product | No tickets in Ops. Aura has `/ops/support` (`ON-004`) — that is Aura, not Pay |
| Support identity | `privacy@lazuar.com` only on privacy page |
| Integrator debug | **Delivery Logs** (`/developer/logs`) — this is the real support surface and should be marketed as such |
| Uptime evidence | Workers exist (outbox 10s, dunning hourly). No public metrics |
| Severity / RTO | Not written |

Paddle sells **buyer** support so the creator never sees “I was double charged.” We **explicitly** push that to the Creator (refund policy). That only works if the Creator has a working email and we show **their** support address on checkout. Today we show **ours** in the legal footer and **theirs** nowhere on the form.

**Do not** publish 99.9% on a marketing site until there is a status page and a 30-day lookback. Either delete the number from TOS or define it.

### 6. Trust badges and checkout conversion

Conversion research (Baymard, Stripe, PayPal — industry folklore we should treat as directional): recognized **payment-method marks** (FPX banks, Visa, TnG) outperform generic padlocks. MoR checkouts show Visa/MC/Apple Pay because **they** are the merchant. Our hosted page is **ours** then **theirs**.

Current Portal checkout conversion stack:

| Element | Present? | Note |
|---------|----------|------|
| Creator name / logo | Product title; logo lives on **hidden** billing profile | After ADR 023, invoices/logo path is dark |
| Itemized total | Yes | No SST/tax |
| Coupon | Yes | |
| Guest checkout | Yes (`IdentityBanner`) | |
| TOS / privacy clickwrap | Link, not a required checkbox | |
| “You pay {Creator}, not Lazuar” | Yes, once | Should be **repeated** next to the CTA |
| Gateway marks (FPX, Visa, TnG) | **No** | Should be **dynamic** from active BYOK (“Pays with Billplz FPX”) |
| PCI / SSL badge farms | No | **Keep refusing** badge mills |
| Address / phone | Config flags | Phone labelled WhatsApp even if WA disabled |
| Company / TIN | Hidden | Correct until LHDN remount |
| 3-D Secure / bank UI | On the **gateway** page | Our job is to get them there with low anxiety |

M2M cashier (Aura `/book`) conversion is **Aura’s** guest UX (`10-guest-booking-ux.md`) plus Billplz’s page. Pay’s job is: correct amount, correct callback, no `Coming Soon`, no `PAYMENTS_NOT_CONFIGURED` mid-funnel.

### 7. SST on **our own** SaaS / credit fee

This is **not** tenant SST on beauty tickets (`CP-003` / `19` §2). This is **Lazuar charging Malaysian customers for software**.

Facts used (jurisdiction context, not advice):

- Service tax on most taxable services is **8%** (from 6% on 1 Mar 2024).
- **Digital services** (SaaS, software, payment processing, online platforms) are in the taxable set for **foreign** digital-service providers via SToDS at 8% once **RM 500,000** / 12 months to Malaysian consumers; domestic registrants follow the Service Tax Act / RMCD registration for their group.
- Threshold for many groups remains **RM 500,000** taxable turnover in 12 months.
- Stripe MY: **processing** fees no longer carry SST; **non-processing** Stripe products still may.
- CardUp: **all their fees + 8% SST** — the honest local pattern.
- Paddle as Aura’s MoR: they decide SST/VAT on **Aura Pro**. That does **not** cover **Pay credit packs**.

**What Pay does today on A2:**

- Description: `"Lazuar Utility Credits"`.
- Amount: pack face value **RM 50 / 100 / 200** with **no tax component**.
- Ledger: `EXPENSE_SOFTWARE_SUBSCRIPTION` / `ASSET_CASH` — a **tenant expense** view, not Lazuar’s output tax.
- Billing profile SST number field exists but is **unrouted** and is the **tenant’s** SST ID for *their* invoices, not ours.
- No Lazuar tax invoice PDF to the tenant for the credit purchase (QuestPDF path is for *their* customers; ADR 023 hid even that).

**What must be true later (product stance):**

1. Credit checkout and any future Hub Pro invoice show **subtotal, SST 8% or 0% with reason, total**.
2. If SST = 0, the reason is written: “Lazuar is not SST-registered / below threshold / this supply is out of scope” — pick the **true** one with an accountant, do not invent.
3. If SST = 8%, print **our** SST ID and issue a proper tax invoice (or MyInvois if we ourselves are in a mandatory wave).
4. Do **not** put SST on **Desk B** (buyer → merchant). That is the merchant’s Billplz receipt / their LHDN.
5. Do **not** tell tenants “Paddle handles all your tax” — Paddle handles **Aura’s** subscription tax.

Until (1)–(3) exist, do not sell credits to SST-registered agencies who will ask for a tax invoice in the first week.

### 8. Refunds (two desks)

| Desk | Policy today | Gap |
|------|--------------|-----|
| B — buyer → creator | Portal: we cannot refund; sales final; creator must refund via **their** gateway | Commerce has a refund mutation on transactions; M2M `payment.refunded` is **maturing**. Aura refunds are accounting-only (`12`) |
| A2 — tenant → Lazuar credits | **No** written policy. Chargeback claws units. No “unused credits refundable within 14 days” | Need a credit-pack refund sentence (even if “all credit sales final”) |

Aura’s 14-day first-subscription refund (`19`) does **not** apply to Hub credits.

### 9. Drift and leftover claims (trust debt)

| Claim | Where | Reality |
|-------|-------|---------|
| WhatsApp as live fulfillment / dunning | README diagram, privacy sub-processors, BillingSettings copy, checkout phone helper | `WhatsAppEnabled=false`; ADR 023 email path |
| LHDN at checkout / TIN | README pillars, ADR 021 | UI hidden; B2C job filter broken; double credit deduct |
| 99.9% uptime | TOS §3 | Unmeasured |
| “Merchant of Record information” | Refund `<title>` / description | Body says we are not MoR |
| Community / courses / downloads | Portal landing, TOS §1 | Vault/community **removed** (ADR 022) |
| National Digital KYC | README Phase 3 | Not built; must not appear on a pricing page |
| Wise MassPay | ADR 020 | Refuse |
| PCI DSS / bank-level | We do not claim it (good). Billplz does. | Do not borrow their logo without a contract |

Privacy still listing **Meta WhatsApp** while the flag is false is the same class of PDPA notice-quality bug `19` called out on Aura-web.

---

## Gap table

Verdict language matches `20-sequencing`: **Ours / Both / Partial / Later / Never**. Depth is Pay’s, not Aura’s.

### A. Commercial packaging

| ID | Job | Pay now | Peers | V | Why |
|----|-----|---------|-------|---|-----|
| LP-001 | Public pricing page (0% GMV + credit table + SST footnote) | **none** | Paddle/LS/Polar/Billplz/HitPay all have one | Later | Cannot sell self-serve without a number. Do not invent 5%. |
| LP-002 | Hub Pro flat SaaS SKU (optional) | **none** (ADR names it) | Chargebee, Billplz RM 999/yr, Polar $20–400 | Later | Only after checkout is loved; never required for sandbox |
| LP-003 | Prepaid credit packs sold in UI | **partial** — API + page, **not in sidebar** | Twilio-style; we invented this for LHDN/WA | Partial | Nav + starter-50 callout |
| LP-004 | Credits consume on LHDN / WA as marketed | **partial** — WA off; LHDN UI off; 3+1 bug | n/a | Partial | Honesty: don’t sell dark SKUs |
| LP-005 | Credits consume on checkout (per pay) | none | would mimic take-rate | **Never** | Taxes GMV in disguise |
| LP-006 | 0% platform take on GMV | **shipped** (BYOK) | inverse of Paddle/Fresha | **Ours** | Protect |
| LP-007 | MoR / payfac / hold funds | n/a | Paddle, LS, Polar, Gumroad | **Never** | ADR 019 |
| LP-008 | Marketplace / Discover / Boost | n/a | Gumroad 30%, Fresha 20% | **Never** | `XX-001` |
| LP-009 | Stripe Connect application fee | n/a | Connect 0.25%+ | **Never** | Same as take-rate |
| LP-010 | SST 8% line on **our** credit/SaaS invoice | **none** | CardUp; RMCD | Later | Build the line even at 0% |
| LP-011 | Published credit unit economics | config only | Polar fee table | Later | Part of LP-001 |

### B. Onboarding / TTFC

| ID | Job | Pay now | Peers | V | Why |
|----|-----|---------|-------|---|-----|
| LP-012 | Self-serve signup, no card | **shipped** | Polar, LS, HitPay, Billplz | **Ours** / Both | Add TOS checkbox |
| LP-013 | Email verify + password reset in Ops | API only | everyone | Later | Abuse + vault trust |
| LP-014 | TOS/Privacy clickwrap at register | **none** | everyone | Later | Cheap, legal |
| LP-015 | In-app setup: gateway → test pay → logs | **none** | Stripe onboarding, Aura widget | Later | Highest TTFC lever |
| LP-016 | BYOK vault UX (Billplz 128, CHIP auto-webhook) | **partial** | Stripe Connect hosted onboard is smoother | Partial | CHIP path is the template |
| LP-017 | Provision / Connect without orphan workspace | **partial** — works; Aura `hello` incident | Stripe Connect is idempotent on account | Partial | `PY-005`; paste-first copy |
| LP-018 | Named sandbox (visible env + Billplz host) | **partial** — hostname heuristic | Stripe/HitPay/Billplz sandbox | Partial | `PY-008` depends |
| LP-019 | Sales-led Enterprise / SLA SKU | none | Paddle custom, Billplz Enterprise | Later | Only if a whale asks |
| LP-020 | Partner / affiliate portal + MassPay | none | LS affiliates, HitPay refer, Wise | **Never** *now* | `ON-006` spreadsheet later; **no Wise** |
| LP-021 | National ID KYC at Hub signup | none | Singpass Phase 3 wishlist | **Never** now | Duplicates gateway KYC |
| LP-022 | Optional “gateway live” badge on checkout | none | “Verified by Stripe” class | Later | Delegate, don’t KYC |

### C. Trust / legal / support

| ID | Job | Pay now | Peers | V | Why |
|----|-----|---------|-------|---|-----|
| LP-023 | Buyer TOS / privacy / refund | **shipped** (thin, June 2026) | All | Both | Fix MoR title, WhatsApp sub-processor, 99.9% |
| LP-024 | Merchant TOS + AUP + DPA | **none** | HitPay MSA, LS DPA | Later | Blocks any serious B2B |
| LP-025 | PDPA merchant notice + export | **none** | Phorest-class GDPR tools | Later | Controller of account data |
| LP-026 | Sub-processor register (accurate) | stale list of 3 | Stripe/Paddle lists | Later | Honesty |
| LP-027 | Status page | none | Stripe, Chargebee, Paddle | Later | Before publishing 99.9% |
| LP-028 | Written SLA with service credits | none | Enterprise PSP | Later / Never for v1 | Do not sell what we cannot measure |
| LP-029 | Support intake (email + delivery-log first) | privacy@ only | everyone | Later | Delivery Logs are the product |
| LP-030 | 2FA on Ops | none | Polar Scale SSO; most banks | Later | Vault = production secrets |
| LP-031 | Checkout trust: “pay {creator} via {gateway}” + method marks | **partial** — sentence only | Billplz/HitPay/Stripe | Later | Conversion, not vanity badges |
| LP-032 | Badge mills / fake PCI/BNM marks | none | spammy gateways | **Never** | |
| LP-033 | Buyer support desk (we answer chargebacks) | none (by design) | Paddle 24/7 | **Never** | MoR job; we are not MoR |
| LP-034 | Credit-pack refund / expiry terms | none | prepaid telco norms | Later | Even if “final sale” |
| LP-035 | Align privacy with ADR 022/023 (no community, no WA) | **partial** | n/a | Later | Notice quality |

### D. Mapping onto the living Aura tracker (do not mint duplicates)

These rows already exist; this file **fills evidence**, it does not replace IDs.

| Existing ID | How this file changes the cell |
|-------------|--------------------------------|
| **SA-007** Replace Paddle with Hub Billing | Still **Never**. Pay credits ≠ Aura Pro. |
| **SA-008** $0 SaaS + processing take | Still **Never**. Pay’s 0% is the opposite of this trap. |
| **PY-005** Connect without SQL | **Partial**; orphan workspace is the onboarding hole. |
| **PY-006** Hub deep-link for BYOK | Still the right UX (don’t re-store secrets in Aura). |
| **PY-008** Sandbox three-book soak | Blocked on LP-018 honesty as much as on Aura hops. |
| **PY-022** / **XX-003** GMV take-rate | **Never.** Reaffirmed. |
| **CP-001** Privacy/terms/refund | Aura **shipped**; Pay buyer pages **shipped thin**; merchant pages **none**. |
| **CP-002** PDPA fields | Aura flag; Pay buyer notice only. |
| **CP-006** PCI vault in Aura | **Never**; Hub vault is the allowed place — still not a PCI *claim*. |
| **ON-001/002** Aura checklist | Do not copy into Pay as a 8-step clone; LP-015 is the Pay equivalent. |
| **ON-006** Partner portal | Later / spreadsheet; **no Wise MassPay**. |

### Anti-goals (do not implement to close a cell)

- Become MoR “just for international.”
- Charge 1 credit per checkout.
- Require MyDigital ID to `POST /register`.
- Publish 99.9% on aura-web/hub marketing without a status host.
- Restore community/vault copy on legal pages.
- Auto-charge VIP onboarding.
- Build MassPay to win a single creator.

---

## Tracker IDs

New family **`LP`** (Lazuar Pay commercial / onboarding / trust). Minted here so `00-checklist-tracker.md` can absorb them without colliding with `SA` / `CP` / `ON` (Aura-shaped).

Suggested wave bands (owner may slide ±1; Wave 0 remains Aura soak, not a Pay pricing launch):

| ID | Name | Depth | Class | Pain | W | V | Success metric | Anti-metric |
|----|------|-------|-------|-----:|---|---|----------------|-------------|
| LP-001 | Public pricing page | none | table-stakes | 4 | 1 | Later | Page live; 0% GMV + packs + SST footnote; URL on hub home | A 5% number “to look normal” |
| LP-003 | Credits in sidebar + starter grant visible | partial | table-stakes | 3 | 1 | Partial | New signup sees “50 credits” without guessing URL | Selling LHDN credits while UI hidden |
| LP-006 | 0% GMV take (keep) | shipped | differentiator | 5 | — | Ours | No applicationFee field ships | “Temporary 1% to pay servers” |
| LP-007 | MoR / payfac | n/a | trap | 0 | — | Never | — | — |
| LP-008 | Marketplace | n/a | trap | 0 | — | Never | — | — |
| LP-010 | SST on our fee | none | hygiene | 3 | 8 | Later | Credit invoice shows SST 0 or 8 with reason | Silent 8% added at checkout |
| LP-012 | Self-serve no-card signup | shipped | table-stakes | 4 | 1 | Both | TTFC-ops p50 measured | Demo-wall |
| LP-014 | Register clickwrap | none | hygiene | 3 | 1 | Later | Cannot register without TOS+Privacy | Pre-ticked box |
| LP-015 | Gateway → test pay empty state | none | differentiator | 5 | 1 | Later | 80% of sandboxes complete a RM1 bill in session 1 | 12-step academy |
| LP-017 | Connect idempotent / no orphan | partial | hygiene | 5 | 1 | Partial | Connect on existing `(aura, orgId)` never mints a second empty WS | “Click Connect again” as a fix |
| LP-018 | Visible sandbox env | partial | hygiene | 5 | 0–1 | Partial | Tenant can state “this WS hits billplz-sandbox.com” | `sk_test_` on hub.lazuar.com charging live |
| LP-020 | Wise MassPay / affiliate payouts | none | trap | 0 | — | Never | Spreadsheet bounties only | Building Wise to match ADR 020 |
| LP-021 | Hub-level national KYC | none | trap | 0 | — | Never | KYC stays at gateway | Singpass required to try docs |
| LP-023 | Buyer legal accuracy pass | partial | hygiene | 3 | 1 | Partial | Privacy matches shipped sub-processors; refund title not MoR | New badges |
| LP-024 | Merchant TOS + DPA | none | table-stakes | 3 | 8 | Later | PDF DPA; clickwrap on signup | “PDPA certified” badge |
| LP-027 | Status page | none | later-nice | 2 | 8 | Later | Public incidents; TOS 99.9% removed or defined | Fake 99.99% |
| LP-029 | Support + delivery-log runbook | partial | table-stakes | 3 | 1 | Partial | Docs: “paste delivery id”; mailbox exists | Zendesk suite |
| LP-030 | Ops 2FA | none | hygiene | 3 | 8 | Later | Optional TOTP before live key paste | Mandatory SMS to MY only |
| LP-031 | Checkout “via {gateway}” + method marks | partial | differentiator | 4 | 3 | Later | FPX/Visa marks match **active** BYOK | Generic Norton-style lock |
| LP-032 | Fake compliance badges | n/a | trap | 0 | — | Never | — | — |
| LP-033 | We run buyer billing support | n/a | trap | 0 | — | Never | Creator contact on checkout | Paddle-style 24/7 desk |
| LP-034 | Credit refund/expiry terms | none | hygiene | 2 | 8 | Later | One paragraph on LP-001 | Silent expiry |
| LP-005 | Per-checkout credit tax | n/a | trap | 0 | — | Never | — | — |

**Promotion rule:** a Later row needs `why_now` in the living tracker before it gets a build wave. LP-006/007/008/020/021/032/033/005 are **locks**, not backlog.

**Wave 1 Pay commercial (if the owner opens a Pay-trust slice after Aura Wave 0 soak):** LP-014, LP-023, LP-015, LP-017, LP-018, LP-003, LP-001 (can be a docs page, not a marketing site), LP-029.

**Wave 8+ (honesty / SST / DPA):** LP-010, LP-024, LP-027, LP-030, LP-034.

**Never this company:** LP-005, LP-007, LP-008, LP-009 (Connect fee), LP-020 MassPay, LP-021 Hub KYC, LP-032, LP-033.

---

## Index of decisions this file freezes

1. **Monetize infrastructure (credits) and optional future Hub Pro — never GMV.**  
2. **BYOK means we do not KYC; we must say that out loud and show the gateway’s name.**  
3. **Credit packs are a taxable software supply; SST is our problem, not Paddle’s and not the guest’s.**  
4. **Wise MassPay, marketplace, MoR, per-checkout credits, fake badges — refuse.**  
5. **Time-to-first-checkout is a Billplz KYC clock plus a paste UX; do not promise Polar’s 15 minutes.**  
6. **Legal pages today protect us from buyers, not merchants; DPA/AUP is the real gap.**  
7. **Do not sell LHDN or WhatsApp credits as if ADR 023 had not happened.**  
8. **Sandbox is not `sk_test_` until the Billplz host is a tenant-visible switch.**

---

*End of file. Competitor rates as of 2026-08-16 public pages. Re-fetch Paddle / Billplz / HitPay / Polar before any customer-facing price card.*
