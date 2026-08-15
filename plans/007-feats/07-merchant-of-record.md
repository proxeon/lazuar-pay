# 07 — Merchant of Record vs Lazuar BYOK

**Program:** competitor-feature research for **Lazuar Pay** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`), filed under `plans/007-feats` so the living tracker can promote rows.  
**File role:** Uncondensed analysis of Merchant of Record (MoR) platforms versus Lazuar’s locked **BYOK cashier**. Not a ship ticket. Not a recommendation to become a reseller. Not a rewrite of ADR 019 / 021 / PADDLE-BOUNDARY.  
**Date:** 2026-08-16  
**Status:** Full text. Do not condense this file in the tracker or evaluation.

**Standing locks (do not contradict):**

| Lock | Source | Meaning for this file |
|------|--------|------------------------|
| **BYOK over MoR** | ADR 019 §2 | Lazuar is software. Tenant plugs Stripe / Billplz / CHIP keys. Money never sits in a Lazuar merchant account. |
| **Compliance CaaS, not a feature factory** | ADR 021 | Own the transaction **and** the government tax filing. Do not own the legal sale. |
| **Hub is not MoR for guest GMV** | Glossary; ADR 019; 021-payment D0.1 | Platform GMV take-rate stays **0%**. |
| **Two money systems** | `PADDLE-BOUNDARY.md`; 021 DECISIONS; 007-feats README | **System A** = salon → Aura, **Paddle MoR**. **System B** = guest → merchant, **Hub BYOK**. **System C** = POS cash, never Hub. |
| **Do not replace Paddle for Aura Pro** | SA-007; PADDLE-BOUNDARY | Aura’s own SaaS fee is allowed to be MoR. Tenant GMV is not. |
| **Do not take Fresha-style processing cut** | SA-008; PY-022; XX-003; XX-004 | $0 SaaS + 5–9% of GMV is a different company. |

This file answers one product question:

> Which MoR *screens* should Lazuar copy as **software**, and which MoR *company-shape* must we refuse — especially in Malaysia, where LHDN seller-of-record, SST, and FPX settlement make “Paddle for everything” illegal-feeling and commercially stupid for B2B?

---

## Method

### What this analysis is

A legal-economic and product teardown of the five MoR (or MoR-adjacent) stacks that Western digital sellers actually name in 2026:

| Vendor | Role in this file | Why they are here |
|--------|-------------------|-------------------|
| **Paddle Billing** | Canonical full-stack MoR + billing OS | Aura already uses it for **System A**. Indie SaaS default for “I do not want VAT.” |
| **Lemon Squeezy** | Creator / indie MoR + storefront | Named in ADR 019 as the thing we are **not**. Stripe-owned; 2026 pivot onto Stripe Managed Payments. |
| **Polar.sh** | Open-source developer MoR | Closest *software* inventory (portal, license keys, webhooks, usage). Public price card. |
| **FastSpring** | Legacy enterprise digital-goods MoR | Sales-gated rate, consumer support, affiliate/reseller network, B2B quotes. |
| **Gumroad** | Historically **partial** MoR; full MoR from 1 Jan 2025 | Marketplace + 10% / 30% economics. Proof that “we collect tax” is not the same as “we are your billing OS.” |

It also states, without softening, why those products **win** for a solo US/EU creator selling digital goods into 40 countries, and why they **fail** as the money plane for a Malaysian B2B seller (agency, SaaS, mastermind, salon via Aura, consulting retainer) who must appear as **supplier** on an LHDN MyInvois document and settle **FPX** into a Malaysian current account.

### Primary sources (read 16 August 2026)

**Lazuar / Aura (repo, not marketing):**

| Source | Absolute path |
|--------|----------------|
| ADR 019 CaaS + BYOK | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` |
| ADR 021 Compliance CaaS | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` |
| ADR 020 integration wishlist | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` |
| ADR 014 Vault “Gumroad clone” (historical) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/014-apps.md` |
| Hub glossary (MoR = we are not) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/reference/glossary.md` |
| Payments cashier quickstart | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/payments-integration-quickstart.md` |
| Paddle boundary | `/Users/akmalfirdaus/Code/saas/aura/plans/001-backup/idea/021-payment/PADDLE-BOUNDARY.md` |
| 021 money locks | `/Users/akmalfirdaus/Code/saas/aura/plans/001-backup/idea/021-payment/DECISIONS.md` |
| Aura System A Paddle | `/Users/akmalfirdaus/Code/saas/aura/plans/001-backup/idea/021-payment/02-aura-saas-billing-paddle.md` |
| This program folder | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/` |
| Payments cashier + LHDN gaps | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/01-dunning-engine.md`, `05-billing-module.md`, `09-lhdn-module.md` |

**Vendor (official pages fetched 2026-08-16):**

| Vendor | URLs |
|--------|------|
| Paddle pricing | https://www.paddle.com/pricing |
| Paddle Billing | https://www.paddle.com/billing |
| Paddle tax | https://www.paddle.com/billing/tax-and-compliance |
| Paddle overlay checkout | https://developer.paddle.com/concepts/sell/overlay-checkout |
| Paddle customer portal | https://developer.paddle.com/concepts/sell/customer-portal |
| Paddle webhooks | https://developer.paddle.com/webhooks/overview |
| Paddle legal relationship | https://www.paddle.com/help/start/intro-to-paddle/the-legal-relationship-between-paddle-and-you |
| Paddle supported countries | https://www.paddle.com/help/start/intro-to-paddle/which-countries-are-supported-by-paddle |
| Lemon Squeezy pricing | https://www.lemonsqueezy.com/pricing |
| Lemon Squeezy MoR | https://www.lemonsqueezy.com/reporting/merchant-of-record |
| Lemon Squeezy fees | https://docs.lemonsqueezy.com/help/getting-started/fees |
| Lemon Squeezy portal | https://www.lemonsqueezy.com/features/customer-portal |
| Lemon Squeezy 2026 update | https://www.lemonsqueezy.com/blog/2026-update |
| Polar pricing | https://polar.sh/resources/pricing |
| Polar MoR docs | https://polar.sh/docs/merchant-of-record/introduction |
| Polar portal | https://polar.sh/docs/features/customer-portal/introduction |
| Polar license keys | https://polar.sh/docs/features/benefits/license-keys |
| Polar failed payments | https://polar.sh/docs/features/subscriptions/failed-payments |
| Polar webhooks | https://polar.sh/docs/integrate/webhooks/events |
| FastSpring pricing | https://fastspring.com/pricing/ |
| FastSpring product | https://fastspring.com/product-overview/ |
| Gumroad pricing | https://gumroad.com/pricing |

**Malaysia public / practitioner (2026):**

| Topic | Sources |
|-------|---------|
| LHDN MyInvois / e-Invoice | IRBM e-Invoice Specific Guideline PDF; ClearTax MY 2026 implementation page (Phase 4, RM1m exemption, RM10k individual invoice) |
| SST / SToDS | PwC Malaysia service-tax note; MySST (customs.gov.my); RMCD digital-services 8% / RM500,000 FRP threshold |
| FPX / PayNet | PayNet FPX personal page; Billplz FPX guide (T+1, Enterprise real-time); Xendit / HitPay MY fee surveys (FPX ~RM1.00–1.50 B2C) |
| Stripe MY e-invoice | Stripe support “Understanding e-invoicing requirements for Malaysia” (TIN, SST ID, BRN — Stripe is **not** the supplier) |

### What this analysis is not

- A legal opinion. LHDN, RMCD, BNM, and PayNet rules change. Treat tax paragraphs as **product constraints**, then have a Malaysian tax agent sign the live implementation.
- A recommendation to rip Paddle out of Aura. System A **stays** Paddle MoR.
- A recommendation to put salon GMV, agency retainers, or creator course sales through Paddle “because Aura already has an account.”
- A feature factory to rebuild Lemon Squeezy’s storefront, email marketing, or Discover marketplace (ADR 015 / 021 kill list).
- Coverage of Stripe Tax, Anrok, Vertex, or “MoR add-on on top of a PSP.” Those are **tax software**. They leave the seller of record as the merchant. Different category. Mentioned only to price the alternative stack.

### Two money systems (do not collapse)

This file will keep repeating the Aura/Lazuar split because every MoR teardown on the internet collapses it.

```
SYSTEM A — SaaS commercial (Aura Plan)
  Payer:  Malaysian salon / tenant
  Payee:  Aura (Lazuar sister product)
  Legal seller of the *software seat*: Paddle (MoR)
  Processor: Paddle Billing
  Price: RM 149 / mo or RM 1,490 / yr
  Tracker: SA-001 … SA-006. SA-007 (replace with Hub) = Never.

SYSTEM B — Guest / buyer money (Lazuar Pay / Hub)
  Payer:  End customer (salon guest, SaaS user, B2B buyer)
  Payee:  The tenant (salon, agency, creator, MY company)
  Legal seller of the *underlying good/service*: the tenant
  Processor: tenant’s Billplz / Stripe / CHIP via Hub BYOK
  Platform take: 0% of GMV
  Tracker: PY-*. PY-022 / XX-004 (Aura or Hub as MoR) = Never.

SYSTEM C — Desk cash / proof
  Never Hub. Never Paddle. Never MoR.
```

**Aura uses Paddle for System A on purpose.** That is the *correct* use of an MoR: a small software company selling a digital subscription into many card-issuing countries, where the product is the SaaS itself and the buyer does not need an LHDN invoice *from Aura as a Malaysian supplier of beauty services*. The salon’s *guests* must not flow through that same Paddle account.

**Lazuar Pay is the System B cashier.** If someone says “just use Paddle for checkout, like we do for Pro,” they have mixed the planes. Reject the PR.

### Vocabulary used below

| Term | Meaning in this file |
|------|----------------------|
| **Merchant of Record (MoR)** | The legal entity that **sells** to the buyer. Appears on the card statement, the tax invoice, the chargeback, and the VAT/GST/SST return. |
| **Seller of Record (SoR)** | Often used interchangeably with MoR in US sales-tax talk. In Malaysia, the **supplier** on the MyInvois document is the SoR that LHDN cares about. |
| **Payment service provider (PSP)** | Stripe, Billplz, CHIP, PayNet FPX acquirer. Moves money. Does **not** become the seller. |
| **Payfac / marketplace** | Platform holds a master merchant account and sub-merchants sit under it (Stripe Connect Standard/Express is adjacent). Still not full MoR unless the platform also remits tax as seller. |
| **BYOK** | Bring Your Own Key. Hub stores the tenant’s gateway credentials. Checkout and webhooks are Hub software. Settlement is the tenant’s merchant account. |
| **Take-rate** | Percentage of **gross transaction value** (often including tax) kept by the MoR. Headline 5% + 50¢; effective 5–9%+ after FX, international, PayPal, subscription, recovery, and affiliate adders. |
| **SaaS fee** | Flat (or seat) software subscription. Lazuar’s intended monetization for the cashier, plus prepaid credits for LHDN / WhatsApp. |
| **Partial MoR** | A platform that remits tax in *some* jurisdictions, or collects tax as agent, while the creator remains seller in others. Gumroad pre-2025. Some marketplaces still. |

---

## MoR economics

### 1. The legal move, in one paragraph

A PSP lets **you** sell. An MoR **buys the right to resell your product** (or is appointed as authorized reseller) and then sells it to the buyer under **its** name. Paddle’s own help centre (fetched 2026-08-16) is blunt: *“Paddle is an authorized reseller of their products, as opposed to a ‘payment provider’.”* Polar’s docs say the same with more engineering honesty: Polar is a layer on Stripe; Polar is the reseller; you are the supplier *to Polar*; Polar is the supplier *to the customer*.

That single assignment of “who sold this” determines four cash and liability pipes:

1. Who remits indirect tax.  
2. Who holds the funds between authorization and payout.  
3. Who is the respondent on a chargeback.  
4. What the take-rate is allowed to be.

Everything else — overlay checkout, customer portal, license keys, affiliates, webhooks — is **software** that can live on either side of that assignment.

### 2. Who remits tax

| Actor | MoR (Paddle / LS / Polar / FastSpring / Gumroad-2025) | PSP + tax software (Stripe + Stripe Tax / Anrok) | Lazuar BYOK (ADR 019/021) |
|-------|------------------------------------------------------|--------------------------------------------------|---------------------------|
| **Calculates rate at checkout** | MoR | You, via Stripe Tax / rules engine | **You (tenant)**, via Hub tax config + LHDN classification |
| **Collects tax from buyer** | MoR, often as part of the displayed total | You | **You** (SST line on the tenant invoice) |
| **Registers in the buyer’s country** | MoR (OSS VAT number, US state IDs, GST, SToDS if they bother) | **You** | **You** (SST / SToDS / foreign VAT if you actually sell there) |
| **Files the return and pays the authority** | **MoR** | **You** (Stripe Tax does **not** file; Polar’s docs say this out loud) | **You**, with Hub generating the MyInvois XML/JSON |
| **Appears as supplier on the tax invoice** | **MoR legal entity** | **You** | **You** (tenant TIN, SST ID, BRN) |
| **Income / corporate tax on the sale** | Still **you** (the vendor). MoR never takes your P&L tax. | You | You |
| **Can a MY B2B buyer claim the invoice?** | Usually **no** — the invoice is from a UK/US/IE reseller, not from the Malaysian supplier they contracted | Yes, if you are SST-registered and issue a proper invoice | **Yes, that is the point of ADR 021 Pillar 2** |

Polar’s MoR introduction is the most honest public explanation in this set, so it is worth quoting in product language rather than leaving it as a vibe:

- Most jurisdictions tax **digital** B2C even when the seller is foreign (EU VAT, UK VAT, AU GST, US economic nexus, Malaysia **SToDS**).
- **Capturing** (adding 25% Swedish VAT to a $10 price) can be automated by Stripe Tax.
- **Remitting** (registering, filing, paying Skatteverket) cannot. That is the job MoRs sell.
- Thresholds differ: UK/EU often want registration **before** the first digital B2C sale; Texas does not until $500,000. Malaysia’s **foreign** digital-services threshold is **RM 500,000** in 12 months for an FRP (Foreign Registered Person). A **resident** taxable person has a separate SST registration threshold (commonly RM 500,000 of taxable services; some categories moved in the 1 July 2025 SST expansion).
- If you sell **through** an MoR, *their* volume is what hits foreign thresholds, so **more of your buyers will be charged tax** than if you sold direct below threshold.
- You **cannot** deduct inbound EU VAT on an MoR invoice the way you could if you were VAT-registered yourself.
- You **always** remain liable for income tax in your residence country.

**Malaysia-specific overlay (expanded in “Why MoR fails / wins”):**

- LHDN e-Invoice is a **clearance** regime. The **supplier** on the document is a Malaysian TIN. A Paddle invoice that says the supplier is Paddle.com Market Ltd (or whichever Paddle entity) is **not** a MyInvois document for the Malaysian studio that actually delivered the mastermind.
- SST is **not** VAT. There is generally **no input-tax credit** the way EU VAT has. B2B buyers still demand a **tax invoice from the contractual supplier** for audit and for any SST-registered purchaser’s records. A Paddle PDF does not satisfy a KL finance manager buying a RM 12,000 retainer.
- Export of services can be **zero-rated** under Malaysian rules when the customer is foreign. The **Malaysian supplier** must classify that. An MoR will instead apply *destination* VAT/GST (Irish OSS, US sales tax, etc.) and keep that tax. The Malaysian books then have to explain why revenue landed net of a foreign reseller’s tax and fees, and still produce a zero-rated e-Invoice if LHDN expects one.

### 3. Who holds funds

This is the part founders forget when they only compare “5% vs 2.9%.”

| Stage | MoR | PSP (Stripe / Billplz) | Lazuar BYOK |
|-------|-----|------------------------|-------------|
| **Authorization** | Buyer pays **MoR** | Buyer pays **your** merchant account | Buyer pays **tenant’s** Billplz/Stripe/CHIP account |
| **Card statement** | `PADDLE.NET*PRODUCT` / `LEMON SQUEEZY` / `POLAR` / `FASTSPRING` / `GUMROAD` | Your DBA or Billplz descriptor | Tenant’s descriptor |
| **Float** | Days to weeks. LS: **twice a month**. Polar: Stripe Connect Express, **manual withdrawals**, Stripe payout fees. Paddle: scheduled vendor payouts (not instant). FastSpring: revenue-share withhold, then remit. Gumroad: weekly Fridays, country-dependent. | Stripe: typical 2–7 day rolling. **Billplz FPX: next business day**; Enterprise **real-time**. | Same as the tenant’s gateway. Hub never sees a balance of GMV. |
| **Reserve / rolling reserve** | Common. MoR is on the hook for 120-day card liability, so they hold 5–20% or delay first payouts. New accounts get reviewed (Polar has a public “account reviews” doc). | Stripe can reserve. Billplz/PayNet less so for FPX (bank transfer is not a chargeback rail). | Tenant’s problem. Hub must not invent a reserve. |
| **Refund source** | MoR balance. If you already received a payout, MoR claws back from the next payout or invoices you. | Your Stripe/Billplz balance | Tenant’s gateway. Hub emits `payment.refunded` (honesty first; automation later — PY-011). |
| **Insolvency risk** | If the MoR fails, **your customers’ prepaid subscriptions and your unpaid payouts** are in their estate. You are an unsecured creditor. | If Stripe fails, similar — but your merchant account is at least *yours*. | If **Hub** fails, the tenant still has the Billplz dashboard and the bank credits. That is the BYOK insolvency argument. |
| **Currency** | MoR converts to their payout currency. Hidden FX 0.25%–3%+ (Polar publishes Stripe’s 0.25% EU–1% other; FastSpring third parties report **2.5–5.5%** FX markups; Paddle marketing says “no hidden extras” but cross-currency still has a conversion margin in the wild). | You choose settlement currency. Billplz = **MYR** into a Malaysian bank. | MYR FPX never needs an FX story. |

**Worked float example (why B2B hates this):**

A KL agency closes a RM 20,000 onboarding invoice on Friday 14:00.

- **FPX via Billplz BYOK:** buyer’s Maybank debit, PayNet, Billplz bill paid. Merchant payout **next business day** (or real-time on Enterprise). Cash is in the agency’s CIMB current account Monday. LHDN e-Invoice issued in the agency’s TIN minutes after `payment.completed`.
- **Paddle MoR:** buyer’s Visa is charged to Paddle. Paddle holds for risk + payout cycle. Agency sees a **USD or GBP vendor balance**, minus 5% + 50¢, minus any FX. Payout lands mid-next week at best. The e-Invoice, if any, is **Paddle’s**. The agency’s Xero still needs a **revenue** line for “sales to Paddle” or “Paddle payout,” not “sales to Acme Sdn Bhd.” Acme’s procurement team has a PDF from a UK reseller for a Malaysian consulting job. That deal dies at the second procurement round.

ADR 019’s sentence *“Money flows instantly to the creator”* is the product. Instant is not marketing. Instant is **FPX + tenant merchant account**.

### 4. Who handles chargebacks

| Concern | MoR | PSP / BYOK |
|---------|-----|------------|
| **Respondent named by the card scheme** | MoR | Tenant (or tenant’s acquirer) |
| **Who writes the representment pack** | MoR’s risk team. Paddle and FastSpring sell this as “buyer support + fraud.” | Tenant, or Billplz/Stripe dashboard tools |
| **Who loses the money if representment fails** | MoR **first** (they already remitted tax and paid you). They then **debit your vendor balance**. You still lose the GMV. You also lose the tax that was remitted if they cannot reclaim it. | Tenant loses GMV + fee + chargeback fee |
| **Chargeback fee** | Polar: **$15 per dispute, regardless of outcome**, deducted from balance. Card networks still apply. LS/Paddle bake some of this into the take-rate and still pass scheme fees. | Stripe typically $15; local acquirers vary. **FPX has no card-scheme chargeback.** Disputes are bank-transfer / operational. |
| **Who gets the 0.70% chargeback-rate monitoring program** | **MoR’s** MID. Polar’s pricing page says they will **suspend you** to protect *their* network rate. This is the quiet kill-switch: your entire business can be paused because *their* portfolio is hot. | Tenant’s MID. Painful, but you are not collateral for a thousand other Gumroad creators selling AI ebooks. |
| **Fraud screening** | Included (LS “A.I. fraud,” Paddle “card attacks,” FastSpring risk suite). They will **block** sales you would have taken. | You configure Stripe Radar / Billplz. Hub must not pretend to be a risk desk. |
| **Buyer support** | MoR answers “what is this charge?” because **their name is on the statement**. Paddle lists 24/7 buyer support and 93% CSAT on the pricing page. | Buyer emails **you**. Statement says your brand or Billplz. |

**FPX implication:** the chargeback story that justifies 5% is a **card** story. Malaysia’s default online rail for SMEs is **FPX** (and increasingly DuitNow). There is no Visa reason-code 13.1 on an FPX debit. Selling “we absorb chargebacks” to a Billplz merchant is selling a Western insurance product they do not have the disease for.

Card volume still exists (Stripe BYOK, foreign buyers). For that slice, **the tenant** remains the merchant. Hub can *orchestrate* evidence (receipt, LHDN UUID, IP, delivery webhook) as **software**. Hub must not become the MID.

### 5. Take-rate 5–9% versus a SaaS fee

ADR 019 wrote “5–8%.” The 2026 public cards still cluster there. Effective rates run higher.

#### 5.1 Published headline rates (16 August 2026)

| Platform | Headline | Monthly platform fee | Official adders (not complete) |
|----------|----------|----------------------|--------------------------------|
| **Paddle** | **5% + 50¢** per checkout transaction | $0 | Custom if SKU **< $10** or invoicing. Volume custom. Marketing claims tax, fraud, buyer support, Retain-style recovery **included**. Third-party 2026 writeups still warn of **~2–3% FX** on cross-currency. |
| **Lemon Squeezy** | **5% + 50¢** | $0 ecommerce; email marketing billed on list size (free to 500) | **+1.5%** international (non-US); **+1.5%** PayPal; **+0.5%** subscriptions; **+5%** abandoned-cart recovered sales; **+3%** affiliate referred sales (merchant); **+2%** affiliate payouts; payout FX (1% non-US Stripe payout; PayPal 3% cap $30) |
| **Polar** | Starter **5% + 50¢**; Pro **3.8% + 40¢** at $20/mo; Growth **3.6% + 35¢** at $100/mo; Scale **3.4% + 30¢** at $400/mo | See left | **+1.5%** international cards. Early Member (orgs created **before 27 May 2026**): **4% + 40¢** + **0.5%** subscription, forever until they upgrade. Dispute **$15**. Stripe payout **$2/mo + 0.25% + $0.25**; FX 0.25–1%. |
| **FastSpring** | **Not on the page.** Sales call. Revenue share. | Opaque | Third-party 2026: baseline often cited **5.9% + $0.95** or **5.9% → 3.9%** by volume. FX markups **2.5–5.5%** in reviews. Paddle’s own comparison says FastSpring FX **2.5%** off USD payouts. |
| **Gumroad** | **10% + $0.50** direct links; **30%** Discover marketplace | $0 | MoR tax “included” since **1 Jan 2025**. Processing is inside the 10% claim on the marketing page; independent 2026 fee blogs still model **~13–19%** effective on small tickets once you treat tax-inclusive totals and FX honestly. |

Paddle’s pricing page contrast is the industry’s favourite slide: *Paddle 5% + 50¢ versus a stacked PSP at “~7% and above.”* The stack they mean is Stripe 2.9% + 30¢ + Billing + Tax 0.5% + Radar + the founder’s Saturday VAT returns. That slide is **true for a US/EU digital seller with no local cheap rail**. It is **false** for a Malaysian seller on FPX at **RM 1.10** a pop.

#### 5.2 Effective-rate worked examples

Assume a **$30 / RM 140** digital SKU, buyer in Sweden (25% VAT), international card, subscription renewal. Tax is **added**, and most MoRs take their % on the **gross including tax** (Polar’s own example does this).

**Polar’s published $30 + 25% VAT example (from polar.sh/resources/pricing, fetched 2026-08-16):**

| | Amount |
|--|--------|
| Product | $30.00 |
| VAT 25% | $7.50 |
| Gross charged | $37.50 |
| Starter 5% + 50¢ | $2.38 |
| International +1.5% | $0.56 |
| **Total Polar fees** | **$2.94 (7.84% of gross, 9.8% of net product)** |

Pro plan on the same sale: $2.39. Scale: $2.14. The VAT $7.50 is **not** yours; Polar remits it. You keep roughly $30 − fee.

**Lemon Squeezy’s published $20 + 20% French VAT example (docs.lemonsqueezy.com/help/getting-started/fees):**

| | Amount |
|--|--------|
| Product | $20.00 |
| VAT 20% | $4.00 |
| Gross | $24.00 |
| Platform $0.50 + 5% + 1.5% international | $2.06 |
| Net to seller | $17.94 |

If that $20 is a **subscription**, add 0.5%. If it came from an **affiliate**, add 3% of the referred sale (merchant side) plus 2% when LS pays the affiliate. If it was recovered from an abandoned cart, add 5%. It is entirely realistic to land at **8–11% of gross** on a non-US subscription with an affiliate cookie.

**Paddle headline, same $30 + 25% VAT if they take 5% + 50¢ on $37.50:**

| | Amount |
|--|--------|
| Gross | $37.50 |
| 5% + $0.50 | $2.375 |
| **If no FX adder** | **6.3% of gross** |
| **If 2% FX on a MYR payout** | **~8.3% of gross** |

Paddle marketing insists there are no international or subscription adders. Treat that as **the reason people pick Paddle over LS**, not as a promise that a Malaysian company’s *effective* cost versus FPX is 5%.

#### 5.3 Malaysian GMV versus take-rate (the only math that matters for Lazuar)

Take a professional MY digital business — ADR 021’s ICP, not a $9 indie widget.

**Case A — B2C course / low-ticket, RM 50,000 / month GMV, all FPX, no tax collected at gateway (SST-unregistered or tax-inclusive prices).**

| Path | Monthly cost of money movement | Notes |
|------|--------------------------------:|-------|
| **Billplz FPX BYOK** at RM 1.10 / txn, assume 500 txns of RM 100 | **RM 550** (1.10%) | T+1 MYR. Tenant is seller. |
| **Paddle 5% + 50¢** (50¢ ≈ RM 2.20) on 500 txns | **RM 2,500 + RM 1,100 = RM 3,600 (7.2%)** | Plus FX if Paddle pays USD. No FPX. Cards only. |
| **LS 5% + 50¢ + 1.5% intl** (if treated as non-US) | **~7–8%+** | Twice-monthly payout. |
| **Gumroad 10% + 50¢** | **~12%+** | Worse. |

**Case B — B2B retainers, RM 80,000 / month, 16 invoices of RM 5,000, FPX B2B.**

| Path | Monthly cost | What the buyer receives |
|------|-------------:|-------------------------|
| **Billplz B2B FPX** ~RM 3.00 / txn (public survey range) | **RM 48** | LHDN e-Invoice from **tenant TIN** + QR |
| **Paddle invoicing** (custom, not even on the 5% card) | Sales-quoted; still MoR | Invoice from **Paddle**. Buyer AP rejects it. |
| **5% take-rate** even if they accepted cards | **RM 4,000** | Same rejection. |

RM 4,000 / month is **26×** Aura Pro (RM 149). It is also **83×** the FPX cost. This is why “just charge 5% like Paddle” is not a pricing idea. It is a **different company** that cannot win the deal ADR 021 exists to win.

**Case C — Cross-border digital, $10,000 / month US/EU cards, no MY rail.**

Here the MoR slide becomes real.

| Path | Rough all-in | What you bought |
|------|-------------:|-----------------|
| Stripe 2.9% + 30¢ + Tax 0.5% + your accountant | ~4–6% + **your** VAT registrations | You are seller. You file. |
| Paddle 5% + 50¢ | ~5–7% after FX | They file 100–270 jurisdictions. You sleep. |
| Polar Starter + 1.5% intl | ~7–8% | They file. Open-source dashboard. |
| Lazuar BYOK Stripe | Stripe’s rate + **Lazuar SaaS + LHDN credits** | You are still the seller. Hub can **zero-rate the export** on the Malaysian e-Invoice (ADR 021 Pillar 3). Hub **cannot** file Irish OSS for you unless we become MoR. |

Pillar 3 is **Malaysian export classification**, not “we will be your Irish VAT registrant.” If a tenant’s *primary* pain is EU VAT, the honest product sentence is: *use Paddle (or Polar) as **their** MoR for that storefront, and use Lazuar for the MY-entity storefront.* Two checkouts. Two legal sellers. Do not smash them into one MID.

#### 5.4 SaaS fee versus take-rate (Lazuar monetization)

ADR 019 / 021 monetize:

1. **Flat SaaS** for the checkout / ledger / dunning software.  
2. **Prepaid utility wallet** (`TenantCreditBalance`) for LHDN submissions and (roadmap) WhatsApp dunning.

That is the **anti-MoR** business model. Consequences:

| | Take-rate MoR | Lazuar SaaS + credits |
|--|---------------|------------------------|
| **Aligns with merchant** | You make more when they sell more — also when they **refund**, you already took a cut of tax-inclusive gross | You make more when they **use compliance** |
| **Punishes high GMV / low margin** | Yes. A 70% COGS info-product still pays 5% of top line | No |
| **Punishes B2B large invoices** | Catastrophically | A RM 50,000 invoice costs one LHDN credit, not RM 2,500 |
| **Requires money-transmitter / payfac licensing** | Yes, or a sponsor bank, in every payout corridor | **No** — we never hold GMV |
| **Chargeback capital** | Need a balance sheet | Need none for GMV |
| **Churn** | Hard to leave (all historical invoices are in their TIN) | ADR 021 wants *compliance* lock-in, not *payout* lock-in |
| **Aura System A** | Paddle already takes ~5% of **RM 149**, i.e. ~RM 7.50 / month. That is the correct plane. | Do not move Pro onto Hub to “save 5%.” The VAT/compliance Paddle removes for Aura-the-company is worth RM 7.50. |

**Never** run a hybrid “2% platform fee because Fresha does.” That is PY-022 / SA-008. It forces the licensing, the float, the chargeback desk, and the LHDN identity crisis, while still being too cheap to fund them.

### 6. The four-party diagram

```
BUYER                    LEGAL SALE                         MONEY
-----                    ----------                         -----
                         MoR world:
Buyer -----------------> Paddle/LS/Polar/FS/Gumroad ------> MoR bank
                         (tax invoice = MoR)
                         MoR ---------------- payout -----> Vendor (tenant)
                         MoR ---------------- VAT --------> Tax authorities

                         BYOK world (Lazuar):
Buyer -----------------> Tenant’s Billplz/Stripe/CHIP ----> Tenant bank
                         (tax invoice = Tenant TIN)
                         Hub is not on this line.
                         Hub ---------------- XML --------> LHDN MyInvois
                         Hub ---------------- events -----> Tenant app
```

If Hub ever appears on the **money** line, we have become a payfac or an MoR. That is the line ADR 019 drew.

### 7. Why the 5–9% band exists (it is not greed alone)

An MoR’s P&L has to fund:

- Card interchange + scheme + acquirer (2–3%+ internationally).
- FX spread.
- Tax registration, software, and Big-4 filing in dozens of jurisdictions.
- Fraud and 3DS.
- Chargeback working capital and representment staff.
- Buyer-facing support (because the statement says PADDLE).
- PCI scope (they take the card).
- Account review / acceptable-use (they are the reseller of record; a sanctions hit is *their* banking problem).

That bundle is **rational** at 5% for a $29/mo global SaaS with 80% card and 40 countries of buyers.

That bundle is **irrational** when:

- The rail is FPX at RM 1.10.
- The invoice must carry a Malaysian TIN.
- The buyer is a company that will not pay a UK reseller for a KL-delivered service.
- The “tax” that hurts is LHDN e-Invoice + SST, which an Irish OSS filing does not touch.

---

## Dossiers (Paddle, LS, Polar, FastSpring)

Gumroad is included as a fifth dossier because the brief asked for it and because ADR 014’s dead Vault app literally said “Inspiration: Gumroad, Lemon Squeezy.”

Each dossier uses the same skeleton: company-shape, money physics, feature inventory (checkout, overlays, customer portal, tax, affiliates, license keys, webhooks), 2026-specific notes, and the Lazuar steal / refuse line.

---

### Dossier A — Paddle Billing

**Company-shape.** London-born, now the default **billing OS + MoR** for serious indie and mid-market SaaS (Tailwind, n8n, Laravel-adjacent ecosystem, GeoGuessr, Letterboxd, MacPaw). Not a storefront. Not a marketplace. Not an email platform. The product they want you to install is **Paddle Billing** (successor to Paddle Classic): products, prices, subscriptions, transactions, adjustments, checkout.js, customer portal, Retain-ish dunning, ProfitWell Metrics.

**Legal relationship.** Authorized **reseller**. Paddle’s help centre lists what they manage: order processing and payment; VAT/sales-tax collection, filing, and payment; order/billing support (invoices, receipts, lost licenses); fulfillment via download, license key, or webhook. They “carefully monitor the quality of the products we are reselling.” That sentence is the AUP kill-switch. You are not a Stripe customer; you are a vendor in a reseller program.

**Supported geographies.** Sellers: “anywhere except” a sanctions list (AF, BY, MM, CU, IR, KP, RU, SY, VE, several UA regions, etc.). **Malaysia is not on the seller-block list.** Buyers: 200–300 markets in marketing language; tax “100+ jurisdictions” on the Billing tax page, “270+” on some 2026 evaluation essays. **FPX, DuitNow, and MY bank transfer are not Paddle payment methods.** Local methods they do advertise are the usual card-network set: cards, PayPal, Apple Pay, Google Pay, plus some regional APMs where they have an acquiring story. A Malaysian *buyer* can often pay with a Visa debit. A Malaysian *buyer who only has FPX* cannot complete a Paddle checkout.

**Money physics.**

| Item | Paddle |
|------|--------|
| Headline | **5% + 50¢ / checkout transaction**, $0 monthly |
| What it claims to include | Processing, tax calc + remit, fraud, buyer support, subscription billing, migration, Retain-style recovery |
| What is custom | SKUs **under $10**; invoicing; high volume |
| Who holds funds | Paddle. Vendor payout on their schedule. |
| Who remits VAT/GST/sales tax | **Paddle** |
| Who is on the invoice | **Paddle** (with your product name as the line item) |
| Chargebacks | Paddle fights; vendor balance takes the loss |
| FX | Marketed as all-in; treat cross-currency MYR payout as a real cost |

**Feature inventory (the software we might copy).**

#### Checkout

- **Overlay checkout** (Paddle.js): any element becomes a button; one-page or multi-page; HTML `data-` attributes for CMS sites (Framer / WordPress) with no custom JS. Overlay contains items, totals, tax, payment methods. Success page configurable. Subscription created automatically on completion; payment method vaulted for renewals.
- **Inline checkout:** branded, embedded in the page; more engineering; higher conversion for SaaS pricing tables.
- **Localized** currency, language (17+ languages claimed on the portal; checkout localization is a Billing pillar).
- **Prefill** email, country, postal code.
- **One-page vs multi-page** toggle.
- **Intelligent routing** across acquirers (“best chance of success”).
- **3-D Secure 2** included.
- **Not** a hosted multi-product storefront. That is LS / Gumroad.

#### Customer portal

- On by default. No build required.
- Magic-link email auth. API can mint **authenticated session URLs** so the SaaS app does not force a second login (`Customer portal sessions (Write)` key permission).
- Payments list + **PDF invoices**.
- Saved payment methods: view, delete, update expired cards.
- Subscriptions: see, manage, cancel. Canceled history kept.
- **Cancellation Flows**: survey + dynamic offer to stay (Retain family). This is software + psychology, not MoR.
- Multilingual, 200+ markets.
- Transactional emails from Paddle already deep-link here (update method, cancel).

Aura **already** uses this for System A (`SA-003`). That is the correct reuse. Hub must grow a **tenant-buyer** portal that is *not* Paddle-hosted, because Paddle must not be the seller.

#### Tax

- Destination tax at checkout from geo + postal.
- **Tax / VAT numbers** on the customer / business object. Valid EU VAT → reverse charge (B2B $10 stays $10).
- **Revise** a billed/completed transaction **once** to add name, address, `tax_identifier`. If a valid VAT number is added after the fact, Paddle issues an **adjustment that refunds the tax**. You cannot remove a valid VAT number, only replace it. You cannot change items or amounts.
- Automatic registration + filing + payment “so you don’t have to.”
- Taxable categories on products (why the dashboard makes you classify).
- SOC2 Type 2, PCI SAQ-A (they do not store PAN on their web checkout in a way that expands your PCI), GDPR, CCPA, 3DS.

**What Paddle tax does *not* do:** emit a Malaysian MyInvois UBL with the **tenant** as supplier. The invoice is Paddle’s. SST ID of the KL studio does not appear as supplier SST.

#### Affiliates

- Paddle is **not** LS. There is no first-class “affiliate platform +3%” product on the current Billing marketing surface the way LS has. PartnerStack and similar attach via checkout metadata / customer_key. Treat native affiliates as **weak / external**.
- ADR 020 Phase 3 “Wise MassPay affiliates” is a *tenant growth* feature, not an MoR feature. If we ever build it, we track cookies and pay from the **tenant’s** Wise account. We do not sit in the money.

#### License keys

- Classic Paddle fulfilled license keys and downloads as the reseller. Billing-era docs still describe digital-product flows: webhook `transaction.completed` → **your** handler sends the key or unlocks the download. Paddle emails a receipt. Portal holds purchase history.
- They are **not** Polar: no first-class activation-limit / usage-quota license API as a core Billing object in the 2026 developer IA. Fulfillment is “webhook + you” or Classic leftovers.
- ADR 020 already pointed at **Keygen.sh / Cryptlex** for this, and ADR 021 killed Vault as a product. Correct.

#### Webhooks (Paddle Billing, 2026)

Notification destinations, signature verification, 30+ event types. Entity catalog from the official overview:

| Entity | Events |
|--------|--------|
| Products / prices / discounts / discount groups | created, imported, updated (groups: created, updated) |
| Customers / addresses / businesses | created, imported, updated |
| Payment methods | saved, deleted |
| **Transactions** | created, ready, billed, paid, completed, past_due, payment_failed, canceled, revised, updated |
| **Subscriptions** | created, activated, trialing, updated, past_due, paused, resumed, canceled, imported |
| Adjustments | created, updated |
| Payouts | created, paid |
| API keys / client tokens | created, updated, revoked, expiring, expired, exposure |
| Reports | created, updated |

Provisioning path they document: `customer.created` → `transaction.completed` → `subscription.created` → `subscription.updated` as catch-all.

This catalog is **deeper than Hub today** on subscription lifecycle and adjustments. Hub’s honest cashier events are `payment.completed` / `payment.failed` / (refund honesty later). Commerce has `subscription.*` internally. Outbound customer webhooks are a known gap (`docs/001-gaps/18-outbound-customer-webhooks.md`). **Steal the catalog shape. Do not steal the “we are the seller” payload.**

#### Other Billing software worth naming

- Trials, proration, seats, one-time + recurring on one transaction.
- Invoicing (sales-gated).
- ProfitWell Metrics (SaaS analytics).
- Retain / dunning / cancellation offers.
- Upsell insights (same-domain inbound subscribers).
- Sandbox, SDKs, MCP-era “install Billing from Cursor” marketing (2026).
- Lovable Payments powered by Paddle (AI-builder distribution).

**Why creators pick Paddle (preview of the Malaysia section).**  
One vendor, one rate, VAT/GST/sales tax **filed**, buyer support so “PADDLE.NET” chargebacks do not hit the founder’s Gmail, overlay that a Framer site can ship on a Saturday, portal so you never build billing settings, Retain so failed cards come back. Tailwind’s published quote is the catechism: Stripe would have been ~3.5%, PayPal ~4.5%, and *any* saving was not worth tax-agency scrutiny.

**Why Paddle is the wrong MoR for MY tenant GMV.**  
No FPX. No tenant TIN on the invoice. No T+1 MYR. 5% on a RM 5,000 B2B bill is RM 250 of pure waste plus a procurement reject. Paddle will happily **onboard a Malaysian vendor** (not sanctioned) and then **sell their product as Paddle** — which is exactly the identity LHDN and the corporate buyer cannot use.

**Lazuar line.**  
Keep Paddle forever on **System A**. Copy overlay UX, portal IA, tax-ID revise, webhook taxonomy, cancellation flow, failed-payment email. Never become Paddle for System B.

---

### Dossier B — Lemon Squeezy

**Company-shape.** Launched 2021 as the pretty, indie, “digital products only” MoR. Storefront + overlays + licenses + email marketing + affiliates. Acquired by **Stripe (announced July 2024)**. Legal footer on 2026-08-16: *©2026 Sold through Link, LLC f/k/a Lemon Squeezy LLC*, Stripe badge in the footer. January 2026 CEO post: they have been dark-shipping **Stripe Managed Payments** (Stripe’s own MoR product). Support and product velocity suffered during the rebuild. Read LS in 2026 as **a UI + creator toolkit on top of Stripe’s MoR**, not as an independent reseller balance sheet.

**Money physics.**

| Item | Lemon Squeezy |
|------|----------------|
| Headline | **5% + 50¢**, $0 / mo for ecommerce |
| Real rate | Headline **plus** international, PayPal, subscription, recovery, affiliate, payout FX (see §5.1) |
| Payouts | **Twice a month**, bank wire or PayPal, 200+ countries claimed; 110+ payout countries on the “Paddle alternative” page |
| Tax | Full MoR: “if a tax authority has any issues, we're on the hook — not you.” |
| Funds | LS / Stripe Managed Payments holds, then remits |
| Chargebacks | Their fraud stack + their MID; you still lose the sale |

**Feature inventory.**

#### Checkout

- **Hosted checkout** links (share anywhere).
- **Checkout overlays** (`embed=1` — the product they taught a generation of indie hackers).
- No-code buttons.
- PayPal + cards + “up to 21 payment methods.”
- 95–130+ currencies (marketing disagrees with itself; treat as “a lot, USD-centric”).
- Tax-inclusive pricing toggle (2024+).
- Checkout localization (2025 post).
- Usage-based / consumption billing (2023+).
- Pay-what-you-want, lead magnets, bundles, upsells.
- Abandoned-cart emails (**+5%** of recovered sales — this is a take-rate, not a feature gift).

**Not FPX.** A Malaysian consumer without a Visa/Mastercard/PayPal is not their customer.

#### Customer portal

First-class marketing surface (unlike Paddle, where the portal is “included,” LS *sells* the screenshot):

- Update payment methods (explicitly sold as chargeback reduction).
- Billing history + **generate invoices**.
- Pause / upgrade / downgrade / cancel.
- Access files, downloads, **licenses**.
- Embeddable portal in React/Vue.
- Dashboard toggles for what customers may do.

#### Tax

Same MoR story as Paddle: they calculate, collect, file, remit. PCI DSS “baked into checkout.” They are the legal seller. Card statement says Lemon Squeezy (or, as Managed Payments lands, possibly a Stripe MoR descriptor — watch this in 2026 migrations).

They do **not** issue MyInvois. They do **not** put the tenant SST number on a Malaysian tax invoice as supplier.

#### Affiliates

**Native.** Merchant-facing affiliate platform (launched ~2023). Fees: **+3%** on referred sales (merchant pays LS), **+2%** on affiliate payouts. This is a second take-rate dressed as growth. Tracking, cookies, merchant dashboard, affiliate portal.

Steal the **attribution model** (coupon / link / first-touch). Do not steal the **3% + we pay the affiliate from money we hold**.

#### License keys

Native, heavily marketed to WordPress / Mac / font / plugin sellers:

- Issue on sale.
- Deactivate / re-issue.
- Variant-level controls, key variables.
- Portal access for the buyer.

This is the feature that made LS the default for “I sell a zip and a key.” Polar later out-executed them on activation limits and usage quotas.

#### Webhooks / API

Public API + webhooks (since early Lemon Drops). Laravel package (Dries Vints). Test mode. Zapier. Not as finely documented as Polar’s 2026 event taxonomy, but table-stakes: order paid, subscription created/updated/cancelled, license events, affiliate events.

#### Storefront + email (do not copy as Lazuar product)

- Hosted store, custom domain, SSL.
- Email marketing priced on subscribers.
- Segmentation from purchase data.

ADR 015 / 021: we are not a CMS and not marketing software. LS’s all-in-one is the **vitamin trap** we already killed.

**2026 Stripe Managed Payments note.**  
Stripe’s MoR is **narrower** in country coverage than Paddle’s 10-year filing footprint (FastSpring’s 2026 Paddle-alternative post claimed Managed Payments preview coverage “around 80 countries”). If LS merchants are moved from “Lemon Squeezy the reseller” to “Stripe the reseller,” **descriptors, invoice legal entities, and tax registrations change**. Tenants who thought they had “one MoR forever” will do a tax-identity migration. That is a gift to BYOK: *your* Billplz legal identity does not change when your software vendor does.

**Lazuar line.**  
ADR 019 named LS as the antagonist. Copy overlay, portal, license-key *fulfillment hooks*, dunning email, invoice download. Refuse storefront, email blasting, affiliate take-rate, and the reseller identity. Do not build a “Lemon Squeezy for Southeast Asia” — that sentence is how you accidentally become a payfac.

---

### Dossier C — Polar.sh

**Company-shape.** Open-source (`polarsource`), developer-native MoR. Positioning in 2026: “billing platform for the intelligence era” — usage meters, LLM ingestion strategies, seats, credits, cost insights — plus the older GitHub/Discord/file/license **benefits**. Built on **Stripe** today (“+ more PSPs in the future”). Most transparent MoR in the set: public fee card, public dispute fee, public payout fees, public retry schedule, public “you can do this yourself” tax essay.

**Money physics.**

| Plan | Monthly | Rate | Support |
|------|--------:|------|---------|
| Starter | $0 | 5% + 50¢ | Standard |
| Pro | $20 | 3.8% + 40¢ | Prioritized |
| Growth | $100 | 3.6% + 35¢ | Prioritized |
| Scale | $400 | 3.4% + 30¢ | Slack + prioritized |
| Early Member (created **before 2026-05-27**) | $0 | 4% + 40¢ + **0.5% sub** | Grandfather; lost forever if they upgrade |

Breakeven vs Starter (their numbers): Pro ~$1,379 / mo sales; Growth ~$5,634; Scale ~$19,048.

Adders: **+1.5% international cards**; **$15 / dispute**; Stripe payout **$2 / active month + 0.25% + $0.25**; FX 0.25–1%. Manual withdrawals. Polar says they add **no markup** on Stripe payout fees.

Funds sit in Polar’s Stripe Connect Express structure. That is still **their** float, not the tenant’s Malaysian current account.

**Feature inventory (richest software catalog).**

#### Checkout

- **Checkout links** (persistent URLs → session).
- **Checkout sessions API** (full control).
- **Embedded checkout** on your site.
- **Embedded payment-method** flow (PCI stays with them).
- Localization.
- Custom fields.
- Tax-inclusive or exclusive.
- Discounts.
- Seat-based pricing.
- Usage-based: event ingestion, meters, credits, LLM/S3/stream/delta-time strategies.

Still card-network / Stripe APMs. **Not FPX.**

#### Customer portal

Polar’s portal is the best-specified in this set, and they **will not let you turn it off**. Reasons they give (all stealable as *product requirements*, none requiring MoR):

1. Invoices and receipts must always be reachable (tax/bookkeeping).  
2. Self-serve cancel is **law** in some places (California Automatic Renewal Law — cancel as easily as you signed up).  
3. Update payment method without the merchant touching PAN.

What buyers can do:

- View subscriptions + purchase history.
- Download and **edit invoices** (company name, **VAT number**, billing address) — this is the “I forgot my VAT ID at checkout” loop Paddle solves with `transaction.revised`.
- Download **payment receipts** (method + refunds).
- Access **benefits** (keys, files, Discord, GitHub).
- Cancel.
- Update default payment method (hosted only — custom portal API **cannot** take cards; they keep PCI).
- Optional toggles: change email, switch plans, manage seats, pause/resume, view meters.

Auth: email OTP, or pre-authenticated link from the app. Polar already injects portal links into order, renewal, and **failed-payment** emails.

#### Tax

Stripe Tax for capture. Polar for remittance. OSS VAT number `EU372061545` (they even document accounting-software that cannot parse the `EU` prefix). Coverage: “registered in jurisdictions around the world” + accounting firms for thresholds. They will introduce you to those firms if you want to **leave** and self-file.

They are explicit that **income tax remains yours** and that selling through an MoR can **increase** the number of buyers who pay tax versus selling direct under threshold.

#### Affiliates

**Not native.** Docs point at **Affonso** as an integration. Do not treat Polar as an LS-style affiliate take-rate machine.

#### License keys (best in class among MoRs)

Benefit type, not a bolted ZIP:

- Brandable prefixes (`POLAR_…` / `MYAPP_<uuid>`).
- Expiry after N days/months/years.
- Activation limits (devices) + customer self-deactivate.
- Custom validation conditions (version, IP, …).
- Usage quotas; increment on validate.
- **Automatic revocation on cancelled subscription.**
- Customer-portal activate / validate / deactivate API (`/v1/customer-portal/license-keys/*`).
- Org-scoped (`organization_id` required so keys cannot be replayed across Polar orgs).

This is a **fulfillment engine**. It does not require Polar to be the seller. Keygen.sh does it as BYOK software. ADR 020 already chose that direction. Polar proves the UX buyers expect.

Other benefits (Discord roles, private GitHub repo, file downloads, feature flags, Slack Connect, credits) are the same pattern: **entitlement hooks**. Steal the hook model. Do not steal Discord bouncer as a Lazuar product (ADR 021 killed Community DRM).

#### Failed payments / dunning (copy the state machine)

Documented, not hand-wavy:

1. Renewal: Polar **advances the period first**, then charges.  
2. Failure → `past_due`, `past_due_at` stamped, email + portal link, order stays open with `next_payment_attempt_at`.  
3. Retries from first failure: **+2d, +5d, +7d, +7d** (four retries; last attempt at day 21).  
4. Hard declines (`lost_card`) skip the schedule and revoke.  
5. Success → `active`. Exhaustion → `canceled`, benefits revoked.  
6. **Grace period** (org setting): Immediately / 2 / 7 / 14 / 21 days before benefit revocation. Grace does **not** change retries or keep status `active`.  
7. Updating the payment method in the portal **retries immediately**.

Hub’s Commerce dunning (`docs/001-gaps/01-dunning-engine.md`) promised this and currently **misses the primary online-failure path**. Steal Polar’s table. Deliver it as **email (and later WhatsApp) software** on the tenant’s gateway, not as Polar-the-seller.

#### Webhooks

Among the cleanest 2026 catalogs. Billing: `checkout.{created,updated,expired}`; `customer.{created,updated,deleted,state_changed}`; `subscription.{created,active,updated,canceled,uncanceled,cycled,past_due,revoked,paused,resumed}`; `order.{created,paid,updated,refunded}`; `refund.{created,updated}`; `benefit_grant.{created,cycled,updated,revoked}`; seats; plus org-level product/discount/benefit/organization events.

They document **sequences** (end-of-period cancel vs immediate revoke; renewal `cycled` → `order.created` pending → `order.paid`; pause flags). This is the quality bar for Hub outbound webhooks.

Sandbox, local forwarding, signature verification, delivery dashboard, OAuth2 for partners, typed TS/Python SDKs, adapters for Next/Laravel/Hono/etc.

**Lazuar line.**  
Polar is the **syllabus** for cashier software. It is not the syllabus for Malaysian tax identity. Copy portal requirements, invoice-edit-VAT, dunning schedule, webhook sequences, license-key *API shape* (or integrate Keygen). Refuse their MID, their Connect float, their $15 dispute desk, their “we file OSS.”

---

### Dossier D — FastSpring

**Company-shape.** Pre-Paddle-Billing incumbent. Santa Barbara / US enterprise digital-goods MoR. G2 ~4.5; Trustpilot much worse (~2.8) — typical of a company whose **buyers** (end users charged by FastSpring) review the support desk. That split is the MoR tell: your customers are **their** customers.

Sales-led. **No public rate card.** FAQ: revenue share, no monthly, no minimum volume, cannot use them “as a processor only,” discounts by volume, talk to an AE.

**Money physics.**

| Item | FastSpring |
|------|------------|
| Rate | Opaque. Market chatter 2026: **5.9% + $0.95** entry, **5.9% → 3.9%** (Vendr), custom below that |
| FX | Repeatedly called out as **expensive** (2.5% off non-USD payouts on Paddle’s comparison page; 3.5–5.5% in independent reviews) |
| Funds | Commission withheld at sale; remainder remitted on their calendar |
| Tax | Full MoR, 240+ jurisdictions in marketing, 21+ languages |
| Consumer support | First-class. There is a public “I have a question about a charge” form. **Your buyers talk to Santa Barbara.** |

Effective all-in for a non-USD vendor is often **the high end of 5–9%**, sometimes worse than Paddle, with less developer love.

**Feature inventory.**

#### Checkout

- Popup / branded checkout library **or** API-built checkout.
- Localization, many APMs (still not Malaysian FPX as a native story).
- Cart-abandonment tooling.
- One-time, recurring, subscriptions.

#### Customer portal / subscriptions

- Full subscription OS: trials, proration, custom intervals, pause, upgrade/downgrade, managed plans.
- Consumer account / order lookup (the support form is the portal for angry buyers).
- Digital invoicing for B2B **on FastSpring paper**.
- **Interactive Quotes** (CPQ-lite): proposals, CRM merge, pricing breakdowns. This is the enterprise cousin of Paddle invoicing.

#### Tax

Collect + remit VAT/GST/sales tax. Audit response is *their* lawyer. Same supplier-identity problem in Malaysia.

#### Affiliates

**Native global affiliate / reseller-store network.** Third-party partners sell your SKU. FastSpring sits in the money and the tax. This is closer to a **distributor** than to LS’s self-serve affiliate links.

Do not copy the network. If a Malaysian ISV wants resellers, that is a **contract + Hub coupon + their own payout**, not Lazuar becoming a distributor.

#### License keys / fulfillment

Classic digital-goods: keys, downloads, cross-sell. Older than Polar’s API, less loved by indie hackers, still what desktop ISVs used in the 2010s.

#### Webhooks / API

Mature but dated compared with Polar/Paddle Billing. Enough to provision. Not the template we copy.

**Lazuar line.**  
FastSpring is what “become MoR for SEA ISVs” looks like in year five: sales team, opaque rate, FX trap, consumer call centre, Trustpilot filled with buyers who do not know who you are. Refuse the company-shape even if a desktop vendor asks for “FastSpring but cheaper.”

---

### Dossier E — Gumroad (partial MoR → full MoR)

**Company-shape.** Creator marketplace + checkout links + “share your work.” Not a billing OS. Discover feed is a **traffic** product that costs **30%**.

**Partial → full.**

- For years Gumroad was the textbook **partial MoR**: they collected sales tax / VAT in some US states and some countries as the marketplace, while creators remained seller of record elsewhere, toggling tax settings per product. Creators still filed income tax on deposits. Some jurisdictions treated Gumroad as marketplace facilitator (a statutory MoR-like role) and others did not.
- **1 January 2025:** Gumroad’s pricing page (still live 2026-08-16) says they became MoR for **all** sales: *“Gumroad handles ALL your tax obligations.”* Creator tax settings disabled. They collect where **they** have obligations. They offer a **California resale certificate** so creators who were already filing can treat Gumroad sales as wholesale-for-resale.
- That last PDF is the smoking gun that even “full MoR” does not erase the creator’s **income-tax and existing-registration** life. It only moves **indirect tax on the consumer sale**.

**Money physics.**

| Item | Gumroad 2026 |
|------|----------------|
| Direct / profile / link | **10% + $0.50** |
| Discover marketplace | **30%** |
| Tax MoR | Included in the 10% (their claim) |
| Payouts | Weekly, Fridays; method varies by country |
| Statement | GUMROAD |
| Chargebacks | Their desk; fees often **kept on refunds** (independent fee blogs) |

10% is already above the 5–9% MoR band. 30% is a **marketplace** tax. XX-001 / Fresha-Discover energy. Never.

**Features (creator, not billing).**

- Product pages, memberships, courses, wishlists.
- Checkout on Gumroad-hosted pages.
- Limited subscriptions (not Paddle Billing).
- Audience / email (vitamin).
- Discover (trap).

License keys and overlays exist in a creator-grade way, not a Polar-grade way.

**Lazuar line.**  
ADR 014’s Vault “Gumroad clone” is **dead** (ADR 021/022). Do not resurrect a marketplace to “be Gumroad with LHDN.” If we ever deliver files, it is **signed R2 URLs after a BYOK payment**, not a Discover feed.

---

### Comparative matrix (software features × legal physics)

| Capability | Paddle | Lemon Squeezy | Polar | FastSpring | Gumroad | Lazuar Hub today (honest) | Steal as software? | Become MoR to have it? |
|------------|:------:|:-------------:|:-----:|:----------:|:-------:|---------------------------|:------------------:|:----------------------:|
| Overlay / embed checkout | Y | Y | Y | Y (popup) | P | Hosted portal checkout; no Paddle-like overlay widget | **Y** (UX) | N |
| Hosted checkout URL | Y | Y | Y | Y | Y | **Y** (`portal.lazuar.com`) | already | N |
| Customer portal | Y | Y | Y | P | P | Thin magic-link after pay; not a billing OS | **Y** | N |
| Magic-link / OTP portal auth | Y | Y | Y | P | P | Partial | **Y** | N |
| Update payment method | Y | Y | Y | Y | P | Only if tenant gateway vaults (Stripe yes, Billplz **no**) | **Y** (Stripe path) | N |
| PDF invoices + history | Y | Y | Y | Y | P | QuestPDF / LHDN path exists, buyer self-serve weak | **Y** | N |
| Edit invoice VAT/TIN after pay | Y (revise once) | P | Y (portal edit) | P | N | TIN on B2B checkout form (portal); no “revise + refund SST” | **Y** | N |
| Tax calc at checkout | Y (their rates) | Y | Y | Y | Y | Tenant SST % / zero-rate export — **our** rules | **Y** (our rules) | N |
| Tax **remittance** as seller | **Y** | **Y** | **Y** | **Y** | **Y** (2025+) | **Must stay N** | **N** | would require MoR |
| LHDN MyInvois as **tenant** supplier | N | N | N | N | N | Backend pipeline **yes**; UI lobotomized (ADR 023) | **Y** (finish) | N |
| FPX / PayNet | N | N | N | N | N | **Y** via Billplz BYOK | already | N |
| Affiliates | External | **Y** (+3%) | Affonso | **Y** network | P | N (ADR 020 Phase 3 wishlist) | attribution later | N |
| License keys | Webhook-ish | **Y** | **Y** (best) | Y | P | N (Keygen on roadmap, Vault killed) | later fulfillment | N |
| Failed-pay email + portal link | Y | Y | **Y** (spec’d) | Y | P | Email templates exist; online-fail → dunning **gap** | **Y** | N |
| Retry schedule | Retain / Billing | Y | **Y** 2/5/7/7d | Y | P | Hourly jobs; primary fail path broken | **Y** | N |
| Webhook catalog | Excellent | Good | Excellent | OK | OK | Cashier `payment.*`; Commerce `subscription.*`; outbound incomplete | **Y** | N |
| Usage / seats / meters | Y | Y | **Y** | P | N | Credits wallet is **platform** utility, not buyer usage | later | N |
| Buyer support as legal seller | **Y** | Y | P | **Y** | Y | Tenant supports their buyers | **N** | would require MoR |
| Holds GMV | **Y** | **Y** | **Y** | **Y** | **Y** | **Must stay N** | **N** | would require MoR |
| Take-rate on GMV | 5%+ | 5%++ | 3.4–5%+ | ~4–9% | 10–30% | **0%** | **N** | would require MoR |

---

## Why MoR fails / wins in Malaysia

This section is the heart of the file. Western Twitter has one story (“just use Paddle”). Malaysian B2B has the opposite story. Both are true on **different planes**.

### 1. Why creators pick Paddle (VAT/GST globally) — the win

The win is real. Do not sneer at it.

**The pain they actually have**

A solo founder in Austin, Berlin, or Singapore ships a $19/mo SaaS. Day one, a buyer in Germany appears. EU VAT on B2C digital services is due **from sale one** (no useful de minimis). UK VAT the same. Then Norway. Then Australia GST. Then India equalization / GST on OIDAR. Then six US states after Wayfair nexus. Stripe Tax will *add the percent*. It will not *file the return*. Anrok / Vertex will file if you pay them and you register. The founder now has:

- 15–40 tax IDs,
- a quarterly calendar,
- reverse-charge rules for B2B VAT IDs,
- evidence of buyer location (OSS requires it),
- digital-services place-of-supply logic,
- and a personal risk of assessments plus penalties (Paddle’s own 2026 VAT-penalties blog exists because this fear sells).

**What Paddle sells into that fear**

- They are the **seller**. The German invoice is Paddle’s. The OSS return is Paddle’s. The founder’s company invoices *Paddle* (or receives a vendor payout statement), not 40 tax authorities.
- Overlay + portal + dunning so the founder never builds billing.
- Buyer support so “PADDLE.NET*ACME” does not become a 2 a.m. email.
- One 5% + 50¢ number that is **higher than Stripe** and **lower than the fully loaded cost of doing VAT correctly** once you count accountant hours, missed filings, and the emotional load Tailwind’s CEO described.

**When this win applies to *our* world**

| Persona | Paddle/LS/Polar is rational? | Why |
|---------|------------------------------|-----|
| US/EU indie, digital, card, no MY entity | **Yes** | Classic MoR ICP |
| SG/HK holding company, global SaaS, no MY ops | **Often yes** | Same VAT story |
| **Aura the company**, selling Pro RM 149 | **Yes — we already do this (System A)** | We are a digital SaaS. Salons are not asking Aura for a MyInvois of a facial. They are paying for software. Paddle invoice is acceptable. |
| MY creator selling $9 Notion templates to Twitter | **Maybe**, if they have no SST registration, no LHDN phase, and only cards | They are the “beginner creator” ADR 021 **explicitly declined** |
| MY Sdn Bhd selling to MY companies | **No** | Rest of this section |
| MY Sdn Bhd selling to EU consumers **and** MY companies | **Split storefronts**, not one MoR | EU storefront may use Paddle; MY storefront must be BYOK + LHDN |

**Aura System A is the win, on purpose.**  
Salon → Aura Pro is a digital subscription sold by a small software firm. Paddle MoR is the correct *company* decision. SA-007 (move Pro onto Hub Billing) remains **Never** unless a new legal review reopens PADDLE-BOUNDARY. This file does not reopen it.

### 2. Why Malaysian B2B cannot use that win — the fail

Four independent systems all assume the **same** legal person: the Malaysian business that contracted, delivered, invoiced, and received the money.

#### 2.1 LHDN seller-of-record (MyInvois)

Malaysia’s e-Invoice is **clearance**, not “email a PDF.”

Facts as of the 2026-08-16 public guidance (IRBM + practitioner summaries; confirm against the live Specific Guideline before coding):

- Phased mandate from Aug 2024. **Phase 4** (turnover RM 1–5 million) from **1 January 2026**, with a relaxation window (practitioner pages in mid-2026 described extension of Phase 4 relaxation; exemption floor raised to **RM 1 million** — Phase 5 cancelled).
- Below RM 1 million: currently **exempt**, but once in, you stay in.
- From **1 January 2026**, transactions **above RM 10,000** cannot hide inside a monthly consolidation — they need an **individual** e-Invoice. That is exactly ADR 021’s high-ticket B2B band (RM 5k–50k).
- 55 fields. Supplier **TIN**, supplier **SST ID** (or `NA`), supplier **BRN**, buyer TIN (or general public TIN for B2C), classification codes, tax type, digital signature, UUID + QR after validation.
- B2C may consolidate **until** the RM 10k rule bites; ADR 021 Pillar 1 is the 28th-of-month `ConsolidatedInvoice` job.
- Buyer can reject within 72 hours. Supplier can cancel within 72 hours.
- Non-issue is an Income Tax Act offence (fines / jail in the statute books).

**The identity constraint:** the **supplier TIN on the MyInvois document must be the Malaysian taxpayer who made the supply.**  

If Paddle is MoR:

- The *consumer* tax invoice is Paddle’s foreign entity.
- That document **cannot** be submitted to MyInvois as the studio’s supply — the TIN would be wrong, the SST ID would be wrong, the digital cert would be Paddle’s (they do not have an IRBM cert for your company).
- If the studio **also** submits a MyInvois invoice as themselves, LHDN now sees a supply that **does not match** a bank receipt (the bank receipt is a Paddle payout, not a customer FPX). The buyer has **Paddle’s** PDF. The government’s UUID is the studio’s. Procurement and audit both choke.
- If the studio **does not** submit, a Phase-4 taxpayer has simply **not invoiced** their largest sales. That is the offence.

ADR 021 Pillar 2 is written against this: *checkout collects TIN, validates against LHDN, takes payment, submits UBL, returns QR to the corporate buyer.* The supplier is the **tenant**. There is no sentence in which Paddle’s TIN is an acceptable substitute.

Stripe’s own MY e-invoice support article is aligned with BYOK, not MoR: Stripe will help generate a government invoice only if **your** MY TIN, SST ID, and BRN are on file. Stripe is not the supplier. That is the correct PSP posture. Hub should be the same, except Hub actually **files**.

**E-commerce / marketplace special cases** exist in IRBM industry FAQs (self-billed, marketplace operator). Those are for Shopee/Lazada-shaped operators who are **statutory** intermediaries. Becoming that person is a **licensing and tax-identity project**, not a feature. It is XX-004.

#### 2.2 SST (Sales and Service Tax)

Malaysia does not have VAT. It has **SST**, administered by **RMCD** (Customs), not LHDN — two authorities, two numbers, two returns.

2026 facts used here:

- Service tax standard rate **8%** for most taxable services since **1 March 2024** (was 6%). Some categories remain 6%; 1 July 2025 expansion added goods/services at 5%/8% bands. Do not hard-code a single rate in product copy.
- Registration threshold commonly **RM 500,000** of taxable services in 12 months (some 2025 changes lifted certain categories toward RM 1 million — implement as config, not folklore).
- **Imported taxable services:** a Malaysian business *buying* foreign digital services may have to self-account SST.
- **SToDS** (Service Tax on Digital Services): **foreign** providers to Malaysian **consumers** register as **FRP** once MY digital sales exceed **RM 500,000** / 12 months, charge **8%**, file DST-02 quarterly.

What this does to MoR:

| Situation | Who RMCD thinks sold the service | What an MoR does | What the MY tenant still owes |
|-----------|----------------------------------|------------------|-------------------------------|
| MY Sdn Bhd, SST-registered, sells to MY consumer | The Sdn Bhd | If MoR is foreign, they may *also* think they have an SToDS obligation, or they may ignore MY because they are “the seller” in Ireland | SST 8% on the **Malaysian** supply, on **their** SST-02, plus e-Invoice |
| MY Sdn Bhd sells to MY company | The Sdn Bhd | MoR issues a foreign invoice, sometimes with EU VAT reverse charge logic that is **meaningless** under SST | Tax invoice + e-Invoice + SST if taxable |
| Foreign MoR sells to MY consumer (true MoR ICP) | The MoR (SToDS FRP if over threshold) | They charge 8% SToDS if they comply | Tenant still has **income tax** on the payout; LHDN may still want an e-Invoice of the *export to MoR* or of the underlying supply — this is the double-document mess |
| MY Sdn Bhd exports to EU consumer | MY export (possibly zero-rated) **and** EU VAT on B2C digital | MoR charges German VAT and remits to OSS | Tenant must still classify the export correctly for LHDN; they do **not** get the German VAT back |

SST invoices must show the **supplier’s SST number**. Paddle’s invoice shows Paddle’s tax IDs. A Malaysian SST-registered buyer (or an auditor) looking at a Paddle PDF for a KL-delivered workshop will treat it as **not a valid Malaysian tax invoice**.

**Input tax:** SST is not creditable like VAT. The B2B demand for “your SST invoice” is about **audit trail and contractual form**, not about reclaiming 8%. That makes people assume “PDF is enough.” It is not enough once e-Invoice clearance is in force for that taxpayer.

#### 2.3 FPX settlement (PayNet / BNM)

FPX is not “another card brand.” It is PayNet’s **account-to-account** rail:

- Buyer logs into **their** bank (Maybank, CIMB, PBB, RHB, HLBB, …).
- Debits **MYR**.
- PayNet settles to the **merchant’s acquiring bank / PI**.
- Typical PSP payout to merchant: **T+1** (Billplz). Enterprise: **real-time**.
- Ticket: RM 1 to RM 30,000 (bank withdrawal caps).
- Fees: **flat** ~RM 1.00–1.50 B2C; B2B bills often ~RM 3. Public Billplz-class numbers used in 2026 surveys: ~RM 1.10 B2C.

To be an FPX merchant you need a **Malaysian business + a participating acquirer / PI** (Billplz, CHIP, iPay88, HitPay, etc.) under BNM’s payments orbit. Paddle, LS, Polar, FastSpring, and Gumroad are **not** PayNet members selling “Paddle FPX.” They cannot debit Maybank and land MYR in *their* UK/US account under the FPX scheme rules the way Billplz can for *your* account.

Consequences:

- **Conversion.** A MY SMB checkout without FPX is a hobby project. ADR 019 said this. ADR 020 said this. File `13-payments-refunds-rails.md` in this program will say this again.
- **Settlement identity.** The credit on the tenant’s bank statement is **Billplz / CHIP / merchant name**, matching the e-Invoice supplier and the SST registrant. An MoR payout is a **foreign inward remittance** with a different counterpart.
- **Chargebacks.** FPX is not a scheme-chargeback rail. The 5% “we handle chargebacks” premium does not apply.
- **B2B FPX.** Corporate buyers pay large invoices by bank. They will not open Paddle overlay and put the company’s Visa on a UK reseller for a RM 20,000 SOW.

**DuitNow** is the same family (PayNet). Same conclusion.

#### 2.4 Corporate procurement and “who did we pay?”

A Malaysian company’s AP checklist for a RM 8,000 software/retainer bill:

1. PO / contract names **Acme Sdn Bhd**.  
2. Bank transfer / FPX beneficiary is **Acme Sdn Bhd** (or its Billplz).  
3. e-Invoice QR validates on MyInvois as **Acme’s TIN**.  
4. SST number on the invoice is **Acme’s** if they are registered.

Paddle fails (1)(2)(3)(4) simultaneously. The buyer’s finance manager has been trained by two years of LHDN webinars to reject this.

ADR 021’s line about buyers hesitating to pay $10k on a “standard checkout link” is about **trust and contract**, not about needing an MoR. The mitigation there is **TIN + e-sign + escrow + instant QR**, not “make Paddle the seller so the buyer feels safer.” A foreign reseller in the middle makes a high-ticket MY B2B deal **less** safe.

#### 2.5 Bank Negara, withholding, and “is this even our revenue?”

Even if LHDN and SST were ignored:

- Paddle payouts look like **export of services to a related/unrelated foreign reseller**. Transfer-pricing and “substance” questions appear once numbers are large.
- Some MoR contracts are **buy-sell** (they take title). Your statutory revenue may be the **net payout**, not the list price. Your SST and e-Invoice bases may legally be different from your Stripe-dashboard-imaginary GMV. CFOs hate this.
- Withholding tax can appear on cross-border services the other way (you paying a foreign MoR for “marketing/resale”). This is accountant territory; product takeaway is: **do not force every tenant into a cross-border reseller contract** just to get a customer portal.

#### 2.6 The “Paddle works in Malaysia” confusion

Paddle’s country list **allows Malaysian vendors**. That is how people get stuck:

1. Founder signs up for Paddle from KL.  
2. Overlay works. A US card pays.  
3. They announce “we support Malaysia.”  
4. First SST-registered customer asks for an e-Invoice.  
5. First FPX-only customer bounces.  
6. First RM 10k invoice hits Phase-4 individual e-Invoice.  
7. They come to Lazuar.

Our job is to be the product at step 7, not to become a worse Paddle at step 2.

### 3. When MoR still wins *inside* Malaysia

Be precise. MoR is not “always wrong in MY.”

| Scenario | Verdict | What to tell the tenant |
|----------|---------|-------------------------|
| Aura collecting Pro | **Use Paddle** | Already done. System A. |
| MY creator, no SSM, no SST, no e-Invoice phase, only foreign card buyers | MoR is **tempting** | ADR 021 declined this ICP. If we serve them, still BYOK Stripe, not Hub-as-MoR. |
| MY company, EU B2C digital as **primary** revenue, no MY buyers | MoR for **that** storefront is rational | Integrate Paddle **as a gateway adapter** only if we ever need it — still **their** Paddle account, not ours. Prefer: “use Paddle over there, Hub here.” |
| MY company, mixed MY B2B + EU B2C | **Split** | Two legal sellers. Two checkouts. One Hub ledger can still *record* both if the Paddle adapter is BYOK (keys in vault) and we never remit tax as Lazuar. |
| MY company, MY B2B / MY B2C / FPX | **MoR fails** | This file. |
| Foreign buyer of a MY export | **BYOK + zero-rate e-Invoice** | ADR 021 Pillar 3. Do not charge them Irish VAT via Paddle unless they *want* an MoR storefront. |

### 4. Side-by-side: same RM 12,000 MY B2B sale

| | Paddle MoR | Lazuar BYOK + LHDN |
|--|------------|--------------------|
| Buyer checkout | Overlay, card, maybe PayPal | Portal / hosted, **FPX first**, card optional |
| Statement | PADDLE.NET*… | Tenant / Billplz |
| Tax collected | Whatever Paddle thinks KL deserves (often **none**, or a wrong VAT mental model) | SST 8% **if** tenant is registered and the service is taxable; else 0 with the right tax type code |
| Invoice | Paddle PDF, Paddle TIN | MyInvois UUID + QR, **tenant TIN**, SST ID, BRN |
| Buyer AP | Rejects or parks in “foreign” | Accepts, claims if applicable, archives QR |
| Money in tenant bank | Net payout in 7–14 days, FX | T+1 MYR (or real-time) |
| Platform cost | ~RM 600 + 50¢ | FPX ~RM 3 + Lazuar SaaS + 1 LHDN credit |
| Chargeback | Paddle desk; rare on a willing B2B card; FPX not offered | FPX: none. Card: tenant’s Stripe. |
| Xero | “Paddle payout” | Invoice + receipt matching bank |

The right-hand column is the company ADR 021 described. The left-hand column is the company ADR 019 refused.

### 5. Sister-product reminder (do not confuse the two Aura money systems)

People reading this folder will mix Aura-the-salon-OS with Lazuar-the-cashier. One more time, with the 2026 production names:

| Flow | Product | Processor | Legal seller | File to read instead if you are lost |
|------|---------|-----------|--------------|--------------------------------------|
| Salon pays for Aura Pro | Aura | **Paddle** | Paddle (MoR) | Aura `02-aura-saas-billing-paddle.md`, SA-* |
| Guest pays a deposit for a facial | Aura booking + **Lazuar Hub** | **Billplz** (BYOK) | **The salon** | `13-payments-refunds-rails.md`, PY-* |
| Guest pays cash at the desk | Aura POS | none | The salon | PS-* |
| Agency buyer pays a RM 8,000 Hub commerce invoice | Lazuar Commerce + Payments | Billplz/Stripe BYOK | **The agency** | ADR 021 Pillar 2 |
| Aura engineering wants “Pro on Hub” | — | — | — | **SA-007 Never** |

Paddle appearing in Aura’s codebase is **not** a precedent for Hub becoming MoR. It is a precedent for “use the right legal seller for the *software seat*.”

---

## Features to steal without becoming MoR

Split every MoR screenshot into **physics** (never) and **software** (steal). ADR 019 already did this in one sentence; this section is the implementation-shaped inventory.

### Never become (physics)

These are company-shape. They do not get a wave. They get a trap ID.

| Never | Why | What people will say | Reply |
|-------|-----|----------------------|-------|
| **Hub / Aura as MoR for tenant GMV** | LHDN supplier TIN, SST, BNM, float, chargebacks, 5–9% | “Paddle does it, why don’t we?” | Paddle does it for **Aura Pro**, not for guest money. |
| **Hold merchant funds** | Money-transmitter / payfac; insolvency; delayed payout vs FPX T+1 | “We’ll pay out T+1, relax” | Then you are a PI. Billplz already is. |
| **Remit SST / VAT / OSS / SToDS as reseller** | IRBM cert is per TIN; RMCD FRP is a different person | “We’ll just file for them” | That is an accounting firm + legal entity in 40 countries. |
| **Absorb chargebacks on our MID** | Capital + 0.70% network monitoring | “It’s a feature” | FPX has no chargebacks; cards stay on **their** Stripe. |
| **GMV take-rate (any %)** | Forces the four rows above | “2% is friendlier than 5%” | 2% of RM 80k B2B is still RM 1,600 for software that must not touch the money. |
| **Single Paddle account for System A and System B** | Mixes Aura Pro with guest/tenant GMV | “One billing stack” | Two planes is the product. |
| **Marketplace / Discover** | Gumroad 30%; Fresha | “Growth” | XX-001. |
| **Consumer billing support in our name** | Statement would have to say LAZUAR | “White-glove” | Support **software** (macros, receipts). The legal respondent is the tenant. |
| **PCI vault in Hub** | CP-006 | “We’ll tokenize for Billplz” | Billplz cannot vault. Stripe Customer + PaymentMethod stays in **their** Stripe account via BYOK. |
| **Payfac / Stripe Connect master** | Adjacent to MoR; still holds or controls funds | “Connect is BYOK-ish” | Connect Standard is closer to BYOK. Connect Express/Custom is our name on the platform. Do not. |

### Steal as software (the actual backlog)

Each item: what the MoR taught buyers, what Hub has, what to build, what not to grow into.

#### 1. Customer portal (steal — MOR-011, MOR-012, MOR-020, MOR-021)

**Taught by:** Paddle (default, magic link, invoices, methods, cancel + Cancellation Flows), LS (embeddable, licenses, pause), Polar (cannot disable; OTP; edit VAT on invoice; benefits; meters).

**Buyer jobs (one breath each):**

- “Show me what I paid and download the invoice.”  
- “Update the card so the subscription does not die.”  
- “Cancel without emailing a founder.”  
- “Add my company TIN / SST / VAT so finance shuts up.”  
- “Where is my license / file / access.”

**Hub today:** `lazuar-portal` is a **cash register** (checkout, quote, TIN field on B2B). It is not a billing OS. Post-purchase is a thin magic link. Aura uses **Paddle’s** portal for System A only.

**Build (software):**

- Hosted `/portal` session for the **buyer of a tenant**, scoped to that tenant’s org.
- Magic link / OTP to the email on the order (Polar/Paddle).
- List orders, subscriptions, receipts.
- Download **tenant** invoice PDF **and** the MyInvois QR/UUID when present.
- Update payment method **only** when the BYOK gateway can (Stripe Customer). Billplz path: “pay this past-due bill via a new FPX link,” not “update card.”
- Self-serve cancel / pause **if the tenant enables it** (Polar-style toggles). Hard-require a cancel path where the tenant sells auto-renew to jurisdictions that require it.
- Authenticated session URL API so the tenant’s app (Aura, a Next.js SaaS) can deep-link without a second login.

**Do not build:** a portal that charges **our** card; Paddle Retain offers funded by **our** margin; buyer chat with Lazuar support about a salon deposit.

#### 2. Tax IDs at checkout and after (steal — MOR-013, MOR-014, MOR-022)

**Taught by:** Paddle `tax_identifier` + one-shot revise + tax refund adjustment; Polar portal “edit invoice / add VAT number”; ADR 021 Pillar 2 TIN-before-pay.

**Hub today:** QuoteView / CheckoutForm collect **TIN** for LHDN. Good instinct. Incomplete: no LHDN validate-before-pay wired as a productized B2B default; no SST ID of **buyer**; no post-hoc revise; no reverse-charge logic for foreign VAT IDs on *tenant-as-seller* invoices.

**Build:**

- Buyer fields: email, name, country, postal, **TIN**, **SST ID** (optional), **BRN**, **foreign VAT/GSTIN** (optional).
- Validate MY TIN against LHDN before taking a Pillar-2 payment (ADR 021).
- Persist tax IDs on the customer record for renewals.
- Post-pay “fix my invoice” for B2C→B2B upgrades: regenerate / credit-note + reissue e-Invoice (72-hour cancel window vs credit note after — already sketched in LHDN refund handler).
- Display tax-inclusive vs exclusive (Polar toggle) using **tenant** SST rate, not Irish VAT tables.

**Do not build:** a global tax engine that files OSS. If a tenant needs destination VAT remittance, they remain the filer (Anrok, accountant) or they use **their own** Paddle account for that storefront.

#### 3. Failed-payment emails + dunning state machine (steal — MOR-015, MOR-016, MOR-017)

**Taught by:** Polar’s 2/5/7/7-day table + portal deep link + immediate retry on method update; Paddle Retain; LS recovery (do not copy the +5% tax); ADR 021 “keep WhatsApp dunning.”

**Hub today:** `DunningEngineJob`, email templates, Commerce states. Gap analysis is explicit: **online failed charges often never enter PAST_DUE.** That is a correctness bug, not a missing MoR.

**Build:**

- On `payment.failed` / off-session decline: flip subscription `past_due`, stamp time, enqueue email (Resend BYOK).
- Email contains: amount, last4 if any, **one button** to portal or to a fresh Billplz bill.
- Retry schedule **configurable**, Polar’s table as default for cards; FPX path is “send a new bill,” not “retry a token” (Billplz does not vault).
- Grace period before entitlement revoke (Polar’s Immediate/2/7/14/21).
- WhatsApp later, as **credits**, not as a reason to become Meta-of-record.

**Do not build:** a recovery take-rate; Retain-style “we’ll give them 20% off and eat it from our 5%.” We have no 5%. Discounts come from the **tenant**.

#### 4. Overlay / embed checkout (steal UX, not the reseller — MOR-018)

**Taught by:** Paddle.js overlay; LS `embed=1`; Polar embed; FastSpring popup. This is how Framer/Astro sites “just work” (ADR 019 mitigation).

**Hub today:** redirect to `portal.lazuar.com` hosted checkout. Fine for honesty (PCI, FPX bank login needs a full page anyway).

**Build:**

- Scriptable **overlay** that can collect email/TIN and then **redirect** to the gateway hosted page (Billplz/Stripe Checkout). Do not put PAN in the overlay.
- Deep links for Linktree / Instagram (Pay, not a consumer app).

**Do not build:** an overlay that posts cards to `api.lazuar.com`.

#### 5. Invoice PDF, receipts, and history (steal — MOR-011)

**Taught by:** every MoR portal.

**Hub today:** Billing QuestPDF, LHDN human-readable after clearance. Buyer self-serve is the hole.

**Build:** list + download in the customer portal; include SST breakdown; include MyInvois QR; email receipt on pay.

#### 6. Webhook catalog and sequences (steal — MOR-019)

**Taught by:** Paddle entity events; Polar documented sequences (`cycled` then `order.paid`; cancel vs revoke).

**Hub today:** cashier `payment.completed` is the law for fulfillment (never the browser). Outbound HMAC dispatcher exists; catalog and reliability are gappy.

**Build:**

- Public, versioned events: `payment.{completed,failed,refunded}`, `subscription.{created,active,past_due,canceled,revoked}`, `invoice.{cleared,rejected}`, `order.completed`.
- Document sequences the way Polar does.
- Idempotency, signatures, retries, delivery log (Svix-grade, or good enough).

This is how we sell to “Indie Hackers and SaaS developers” (ADR 019 §4) **without** becoming their MoR.

#### 7. License keys as fulfillment (later steal — MOR-024)

**Taught by:** Polar (best), LS, FastSpring, Classic Paddle.

**Hub today:** none. Vault killed. ADR 020 says Keygen/Cryptlex.

**Build later:** a fulfillment type that calls Keygen (or a thin internal key table) on `payment.completed`, revokes on `subscription.revoked`. Show keys in the **customer portal**.

**Do not build:** a DRM business; Discord bouncers (killed).

#### 8. Affiliate *attribution* (later, software only — MOR-025)

**Taught by:** LS native (+3%), FastSpring network, Polar via Affonso.

**Build later:** click IDs, coupons, “this order came from partner X.” Payouts via **tenant’s** Wise/PayPal, not our float (ADR 020 Phase 3).

**Do not build:** LS’s extra take-rate; FastSpring’s reseller store.

#### 9. Cancellation flows (steal lightly — MOR-026)

**Taught by:** Paddle Cancellation Flows (survey + offer).

**Build:** tenant-configurable “why are you leaving?” + optional coupon **funded by tenant**. Required easy cancel.

**Do not build:** dark patterns that violate the same auto-renew laws Polar cites.

#### 10. Tax-inclusive display and B2B reverse-charge *display* (steal — MOR-022)

**Taught by:** Polar/LS tax-inclusive toggles; Paddle reverse charge when VAT ID validates.

**Build:** for MY SST, inclusive vs exclusive is a **display + line** problem. For a tenant who is EU VAT-registered **themselves**, validate VAT ID and zero the **VAT line** (reverse charge). That is software on **their** registration, not ours.

#### 11. Finish LHDN (this is our actual MoR-killer — MOR-023)

The competitive answer to Paddle in Malaysia is not a cheaper reseller. It is **Pillars 1–3 shipping**.

- Pillar 1: B2C consolidation job → single monthly e-Invoice.  
- Pillar 2: TIN validate → pay → instant UBL → QR.  
- Pillar 3: foreign buyer → export classification / zero-rate.

Paddle cannot do this as the tenant. If we do this as software, the 5% pitch dies in every MY B2B demo.

### Steal versus never (one-page)

| MoR screenshot | Copy? | Because |
|----------------|:-----:|---------|
| Overlay checkout | **Software** | Conversion. Still BYOK underneath. |
| Customer portal | **Software** | Table-stakes billing UX. |
| Tax ID / TIN / VAT fields | **Software** | LHDN + SST + foreign VAT. |
| “We file VAT in 270 countries” | **Never** | Different company. |
| Failed-payment email + retry table | **Software** | Revenue recovery. ADR 021 keep. |
| “We absorb chargebacks” | **Never** | MID + capital. |
| License keys | **Software / Keygen** | Fulfillment. |
| Affiliates +3% | **Never the %**; maybe the cookie | Take-rate. |
| Discover / marketplace | **Never** | Gumroad 30%. |
| Hosted storefront / email suite | **Never** | CMS trap. |
| Usage meters | **Later software** | Polar-grade; not v1. |
| Payout dashboard of **our** balance | **Never** | That balance must not exist. |
| Paddle for Aura Pro | **Keep** | System A. |
| Paddle for guest GMV | **Never** | System B. |

### Sequencing hint (not a commitment)

If this family is promoted into the Lazuar Pay tracker, a sane order that does not become MoR is:

1. Honest `payment.failed` → PAST_DUE → email (unblocks dunning promises).  
2. Buyer portal: invoices + MyInvois QR + magic link.  
3. TIN/SST fields + LHDN validate-before-pay on high-ticket.  
4. Webhook catalog / sequences.  
5. Overlay widget (redirect, no PAN).  
6. Stripe-only “update payment method.”  
7. License keys via Keygen when a paying ISV asks.  
8. Affiliate attribution when a paying creator asks.

Never in that list: hold funds, take 5%, file OSS, replace Paddle for Aura.

---

## Tracker IDs

Promotion rule: these IDs are proposed for the living checklist (`00-checklist-tracker.md`) in this `007-feats` program. Existing IDs that already cover the trap (from the sister Aura program, still useful as pointers) stay authoritative where they apply. New `MOR-*` IDs are the Pay-specific family this analysis owns. Do not mint a second scheme.

### Existing IDs this file must not fork

| ID | Name | Verdict this file reaffirms | Note |
|----|------|-----------------------------|------|
| **PY-022** | Platform GMV take-rate / Aura-as-MoR | **Never** | Same physics as Hub-as-MoR. |
| **XX-003** | Platform take-rate on salon GMV | **Never** | Fresha economics. |
| **XX-004** | Aura as MoR for guest charges | **Never** | Extend mentally to **Hub as MoR**. |
| **SA-003** | Customer portal (Paddle) | **Both / keep** | System A only. |
| **SA-007** | Replace Paddle with Hub Billing | **Never** | PADDLE-BOUNDARY. |
| **SA-008** | $0 SaaS + processing take | **Never** | MoR business model. |
| **CP-004** | LHDN e-invoice | **Later** on Aura; **core** on Hub | Do not claim Aura does LHDN. |
| **CP-006** | PCI / vault in Aura | **Never** | Same for Hub. |
| **PY-010** | `payment.failed` honesty | **Later / P0 software** | MoR dunning emails sit on top of this. |
| **PY-011** | `payment.refunded` rules | **Later** | MoRs refund from *their* balance; we must refund from *theirs*. |
| **ON-006** | Partner / affiliate portal | **Later** | Attribution only; no LS +3%. |

### New family `MOR` — Merchant-of-Record software vs physics

Use `job_class` = `trap` for Never rows; `table-stakes` / `hygiene` / `later-nice` for steal rows. `money_plane` = `B` unless noted. `hub_or_aura` = `hub` unless noted.

#### Traps (Never — do not wave)

| ID | Name | Class | V | Why |
|----|------|-------|---|-----|
| **MOR-001** | Hub/Lazuar as Merchant of Record for tenant GMV | trap | Never | ADR 019 §2. LHDN supplier TIN cannot be ours. |
| **MOR-002** | Hold merchant funds / operate a payout balance | trap | Never | Payfac. Breaks FPX T+1 identity. Insolvency. |
| **MOR-003** | Remit SST / VAT / GST / OSS / SToDS as reseller | trap | Never | RMCD + 40 foreign authorities. Not software. |
| **MOR-004** | Absorb chargebacks on a Lazuar MID | trap | Never | Capital + network monitoring. FPX does not need it. |
| **MOR-005** | GMV take-rate 5–9% (or “friendlier” 2%) | trap | Never | Duplicate of PY-022; economics section. |
| **MOR-006** | Single Paddle account for System A **and** System B | trap | Never | Mixes Aura Pro with guest/tenant GMV. |
| **MOR-007** | Gumroad-style Discover / marketplace cut | trap | Never | 30% Discover. XX-001. |
| **MOR-008** | FastSpring-style consumer support as legal seller | trap | Never | Requires our name on the statement. |
| **MOR-009** | Stripe Connect Express/Custom platform (we are payfac) | trap | Never | Adjacent MoR. BYOK keys only. |
| **MOR-010** | “Lemon Squeezy for SEA” company-shape | trap | Never | Storefront + email + MoR. ADR 015/021 kill list. |

#### Steal as software (promote when Hub productizes)

| ID | Name | Class | Suggested wave band | V | Ours depth (2026-08-16) | Success metric | Anti-metric |
|----|------|-------|---------------------|---|-------------------------|----------------|-------------|
| **MOR-011** | Buyer customer portal (invoices, history, cancel) | table-stakes | after PY-010 | Later | partial (checkout portal only) | Buyer downloads invoice unaided | Building Paddle’s hosted portal by reselling |
| **MOR-012** | Magic-link / OTP portal auth + session URLs | hygiene | with MOR-011 | Later | partial | Deep link from tenant app works | Passwords for buyers |
| **MOR-013** | Tax ID capture (TIN, SST, BRN, foreign VAT) | table-stakes | Pillar 2 | Partial | partial (TIN field) | B2B pay blocked without valid TIN | Collecting IDs we never put on MyInvois |
| **MOR-014** | Post-pay invoice revise (add TIN / VAT, credit-note) | later-nice | after MOR-013 | Later | none | 72h window or credit note used correctly | Editing amounts |
| **MOR-015** | Failed-payment email with portal/bill link | table-stakes | **now-ish** | Later | partial (templates; path broken) | Every card fail emails once | Email with no PAST_DUE state |
| **MOR-016** | Dunning retry schedule (card) + new-bill (FPX) | table-stakes | with MOR-015 | Later | partial (jobs exist) | Polar-like table honored | Retrying unvaultable FPX as if it were a card |
| **MOR-017** | Entitlement grace period while `past_due` | hygiene | with MOR-016 | Later | none | Configurable 0–21d | Silent access after revoke |
| **MOR-018** | Overlay / embed widget that **redirects** to gateway | later-nice | after hosted soak | Later | none | Framer button → FPX without PAN at Hub | Hub-hosted card fields |
| **MOR-019** | Webhook event catalog + documented sequences | hygiene | with outbound webhooks | Later | partial | `payment.failed` and `subscription.past_due` delivered | Browser `?success=` fulfillment |
| **MOR-020** | Self-serve cancel / pause (tenant-toggled) | table-stakes | with MOR-011 | Later | none | One-click cancel when enabled | Dark pattern retain |
| **MOR-021** | Update payment method (Stripe BYOK only) | table-stakes | after Stripe canary | Later | none | Portal updates PM → immediate retry | Fake “update card” on Billplz |
| **MOR-022** | Tax-inclusive vs exclusive display (tenant SST) | table-stakes | with SST lines | Partial | partial | Display matches e-Invoice | Using Paddle VAT tables for MY |
| **MOR-023** | LHDN supplier = tenant (Pillars 1–3) | differentiator | Compliance CaaS | Partial | backend partial | UUID returned to B2B buyer | Submitting with a platform TIN |
| **MOR-024** | License-key fulfillment hook (Keygen) | later-nice | demand-gated | Later | none | Key issued on `payment.completed` | Rebuilding Vault / DRM suite |
| **MOR-025** | Affiliate attribution (no take-rate) | later-nice | demand-gated | Later | none | Partner ID on order | LS +3% or holding affiliate funds |
| **MOR-026** | Cancellation survey + tenant-funded offer | later-nice | after MOR-020 | Later | none | Offer uses tenant coupon | We fund discounts from a take-rate we do not have |

### Mapping: MoR vendor feature → ID

| Vendor feature | ID | Steal / Never |
|----------------|-----|---------------|
| Paddle overlay | MOR-018 | Steal UX |
| Paddle customer portal | MOR-011, SA-003 (System A) | Steal on B; keep Paddle on A |
| Paddle tax remit | MOR-003 | Never |
| Paddle tax ID revise | MOR-013, MOR-014 | Steal |
| Paddle webhooks | MOR-019 | Steal shape |
| Paddle 5% + 50¢ | MOR-005, PY-022 | Never |
| Paddle buyer support | MOR-008 | Never |
| LS overlay + hosted | MOR-018 | Steal UX |
| LS portal | MOR-011 | Steal |
| LS affiliates +3% | MOR-025 / MOR-005 | Attribution later; % never |
| LS license keys | MOR-024 | Later software |
| LS storefront + email | MOR-010 | Never as product |
| Polar portal (mandatory) | MOR-011–012, MOR-020–021 | Steal requirements |
| Polar failed-pay table | MOR-015–017 | Steal |
| Polar license API | MOR-024 | Steal shape / use Keygen |
| Polar usage meters | (no ID yet) | Later; do not mint until an ICP asks |
| Polar Connect float | MOR-002 | Never |
| FastSpring opaque take-rate | MOR-005 | Never |
| FastSpring affiliate network | MOR-025 / MOR-007 | Never network |
| FastSpring interactive quotes | — | Nice; not a tracker row until B2B quote ICP |
| Gumroad 10% / 30% | MOR-005, MOR-007 | Never |
| Gumroad Discover | MOR-007, XX-001 | Never |

### Verdict summary for the parent evaluation

| Question | Answer |
|----------|--------|
| Should Lazuar Pay become a Merchant of Record? | **No.** ADR 019/021. Tracker MOR-001, PY-022, XX-004. |
| Should we copy Paddle/LS/Polar **screens**? | **Yes**, as software: portal, tax IDs, failed-pay email, webhooks, overlay redirect. |
| Should we copy their **economics**? | **No.** SaaS + credits, 0% GMV. |
| Why do creators pick Paddle? | Destination VAT/GST filed, one overlay, one portal, sleep. Rational for System A and for non-MY digital ICPs. |
| Why can MY B2B not pick Paddle for GMV? | LHDN supplier TIN, SST invoice identity, FPX settlement into the tenant’s bank, procurement matching. |
| What is Aura’s Paddle account? | **System A only.** Not a template for Hub. |
| First steal that pays for itself? | **MOR-015/016** (failed-pay honesty + email) sitting on **PY-010**, then **MOR-011** portal + **MOR-023** LHDN-as-tenant. |

### Evidence pointers (do not drop)

- ADR 019 §2: *“Unlike Lemon Squeezy or Paddle (which act as MoRs, taking 5-8% of revenue and holding funds), Lazuar operates strictly as BYOK software.”*  
- ADR 021 pillars: B2C consolidation, B2B TIN+instant LHDN, cross-border zero-rate — all assume **tenant** is supplier.  
- Paddle pricing 2026-08-16: **5% + 50¢**, custom under $10 / invoicing.  
- LS fees doc: +1.5% intl, +1.5% PayPal, +0.5% sub, +5% recovery, +3% affiliate.  
- Polar pricing 2026-08-16: 5% / 3.8% / 3.6% / 3.4% tiers; +1.5% intl; $15 dispute.  
- Gumroad pricing 2026-08-16: 10% + $0.50 direct; 30% Discover; MoR since 1 Jan 2025.  
- IRBM / ClearTax 2026: Phase 4, RM 1m exemption, RM 10k individual e-Invoice.  
- RMCD / PwC: SST 8%, SToDS FRP RM 500k.  
- Billplz / PayNet: FPX T+1, flat sen-to-ringgit fees, not a % MoR.

---

*End of 07 — Merchant of Record vs Lazuar BYOK. Do not summarize this file in `00-evaluation.md`; point at sections. Do not promote a Never row to a wave because a competitor screenshot was pretty.*
