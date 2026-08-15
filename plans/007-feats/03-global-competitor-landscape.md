# 03 — Global competitor landscape

**Program:** `007-feats`  
**Document:** Global competitor categories, dossiers, and feature-bar setters for **Lazuar Pay**  
**Product:** Lazuar Pay is **BYOK Compliance CaaS / headless checkout** for Asian creators and B2B SaaS. It is **not** a salon app, **not** a Merchant of Record, **not** a website builder, and **not** an acquiring bank.  
**Date researched:** 2026-08-16  
**Pricing rule:** published vendor pages first; quote-only products labelled as such; secondary reviews never presented as official prices  
**Status:** Full uncondensed analysis — no product code

---

## Agent scope

This document maps the **global** payments, billing, tax, creator-checkout, and embedded-finance field so Lazuar Pay can locate itself without treating every logo as a peer.

It does five jobs:

1. **Classify** the market into categories that buy, price, and win differently. A name-list without classification produces false comparisons (Stripe Checkout vs Paddle vs Chargebee vs Gumroad vs Avalara are not substitutes).
2. **Dossier** the category winners and the named products in the brief: typical buyer, typical feature set, published pricing model, and moat.
3. **Name the relation** Lazuar should take to each category: **compete / integrate / ignore / partner**. Most global logos are **rails or adjacent utilities**, not enemies.
4. **Name the feature bar** Malaysian and SEA founders will use as a checklist because they have already seen Stripe Checkout, Stripe Billing, Paddle, Gumroad, and Chargebee — even when those products cannot do FPX-at-local-cost, SST + LHDN UBL 2.1, or WhatsApp-first recovery.
5. **Cite sources** so later product and pricing work can re-verify numbers. Payment and billing list prices moved in 2025–2026 (Stripe Malaysia published 3% + RM1.00 including FPX; Chargebee’s public page now leads with a Flow 0.80% plan; Polar posted a four-tier MoR grid; Lemon Squeezy is being folded into Stripe Managed Payments; Metronome closed into Stripe in January 2026). Treat every dollar figure as dated.

Out of scope (covered or to be covered elsewhere):

- SEA / Malaysia **local** rails and PSP brands as a market-structure paper (Billplz, CHIP, Fiuu/RMS, Xendit, HitPay, SenangPay, ToyyibPay, Midtrans, PayMongo). Those names appear here only when a **global** product fails against them or when Lazuar must **integrate** them as BYOK.
- AuraBook salon-ops features. This paper is for **Lazuar Pay**, not Aura’s floor OS. Guest money on Aura may *use* Lazuar; that does not make Fresha a Lazuar competitor.
- Lazuar’s own SKU, credit-wallet unit economics, or implementation plan.
- Feature-by-feature parity matrices for every dunning step or every UBL field. Those belong in product specs. This paper only says **which global products set the bar** for those jobs.

How to use this paper:

- Product: do not copy a category. Copy the **jobs** that category taught buyers to expect, then decide which jobs Lazuar owns in MY/SEA v1.
- Pricing: do not copy a US list price into MY. Copy the **shape** (bps of GMV vs flat SaaS vs prepaid utility) and the **objection** it creates.
- Fundraising and partnership: investors will benchmark Lazuar against Stripe (gravity), Paddle / Polar (MoR alternative), Chargebee (billing engine), and “just use Billplz + Xero + a spreadsheet.” Those four stories are written out below.

Standing product constraints this paper must not contradict:

- Lazuar is **BYOK**. Money settles into the merchant’s own Billplz / CHIP / Stripe / Xendit / Razorpay account. Lazuar does not take 5–8% of GMV and does not become the seller of record.
- Lazuar is **headless**. Marketing lives on Framer, Webflow, Astro, WordPress, or a custom Next.js app. The portal is a cash register, not a CMS (ADR 015, ADR 019).
- Lazuar is **Compliance CaaS**. The moat is compliance at the point of sale — LHDN / GSTN / Coretax class work — plus a double-entry ledger and recovery, not a 15-app creator suite (ADR 021, ADR 023).
- Honest capability as of the product watermark: BYOK gateways + commerce subscriptions + double-entry billing ledger + email dunning templates + LHDN **backend** pipeline. WhatsApp dunning and full compliance UI are Phase D, not guaranteed demoable surfaces on every deploy.

---

## Method and sources

### Research window

Primary research was run on **2026-08-16** against live vendor pages, vendor docs, vendor newsrooms, IRBM / ClearTax e-invoice explainers, and (where vendors hide prices) named secondary reviews. Currency is as published. USD is the default reporting currency unless the vendor publishes MYR or GBP.

Stripe was fetched on the **Malaysia** pricing surface (`stripe.com` geo-routed to `en-my`), because that is the price a Malaysian founder actually sees. Adyen, Paddle, Polar, Gumroad, Payhip, Chargebee, Anrok, Quaderno, FastSpring, Lago, and Stripe Tax country support were fetched from official pages the same day.

### Evidence classes

| Class | What it is | How this paper treats it |
| --- | --- | --- |
| **A — Official published price** | Vendor `/pricing` or help article with a number | Quoted as fact, dated, linked |
| **B — Official quote-only** | Vendor says “contact sales” / “get a quote” / “talk to an AE” | Model described; no invented dollar figure |
| **C — Official scale / positioning** | Vendor about, press, pricing FAQ, newsroom | Quoted as **vendor claim**, not independently audited |
| **D — Reputable secondary** | Tech press, vendor-on-vendor blogs that screenshot a live page, Lago/Chargebee/Orb competitive posts | Used for funding, acquisition, or to corroborate a number; labelled |
| **E — Not used as price** | Competitor attack pages, Reddit, “true cost of Stripe” calculators | Context only (what founders *complain* about), never as a price table |

If a number is not class A or a clearly labelled D, it is not in a price table.

### What “wins” means here

A category winner is not “best software.” It is the product that **sets default expectations** for that buyer:

- largest or most talked-about installed base in that motion,
- the pricing story other vendors are forced to answer,
- the feature set a buyer uses as a checklist even when they buy someone else.

For Lazuar’s ICP (Asian professional creators, agencies, and B2B SaaS founders who already have a site and a tax problem), the default expectation-setter is **Stripe** for checkout/API gravity, **Paddle** for “I do not want to think about VAT,” **Chargebee** for “serious subscription ops,” and **Gumroad** for “just give me a link.” None of those four own **Malaysian legal survival at the cash register**.

### Pricing shapes (the actual market, not a slogan)

Almost nobody is “subscription XOR take-rate” anymore. The 2026 stack is usually **four layers**:

1. **Access fee** — monthly/annual software (flat, per seat, or usage-tiered).
2. **Money-movement fee** — card / APM processing, FX, instant payout, 3DS, disputes.
3. **Compliance fee** — tax calculation (bps or per-market), filings, e-invoicing credits, identity.
4. **Recovery / comms fee** — dunning emails (cheap), WhatsApp / SMS (metered), Smart Retries (often bundled).

Lazuar’s designed shape is **(1) flat SaaS + (4/3) prepaid utility wallet**, with **(2) passed through to the merchant’s own gateway** (BYOK). That is the opposite of Paddle/Polar/Gumroad (they monetize 2+3 as one blended take-rate) and the opposite of Stripe’s upsell ladder (2 is the core; 1 is “free”; 3 and 4 are add-on bps).

Aura-style salon owners who “saw Fresha” have been trained to ask “is it free?” SaaS founders who “saw Stripe” have been trained to ask “is it 2.9% + 30¢?” Malaysian founders who have actually run a Billplz collection have been trained to ask “does FPX cost 3% or 80 sen?” Those three questions are not the same buyer.

### Limits of this method

- Regional list prices differ (Stripe US vs Stripe MY vs Stripe GB). This paper quotes the page fetched (MY for Stripe; vendor English default otherwise).
- Chargebee’s public page on 16 August 2026 leads with **Flow at 0.80% of monthly billing value**. A large body of 2026 secondary blogs still describe the older **Starter free-to-$250k lifetime / Performance $599** grid. Both are recorded; the official page is treated as current commercial truth.
- FastSpring, Zuora, Recurly Growth/Enterprise, Adyen IC++, Checkout.com, Finix platform deals, and Lago Premium are quote-only. Secondary “typical %” figures are labelled D and must not be treated as a contract.
- Lemon Squeezy’s public 2026 blog states they have been building Stripe’s **Managed Payments** MoR product post-acquisition; treat LS as a brand in transition, not a stable independent competitor.
- WhatsApp Business Platform rates vary by market–category pair and changed to per-template-message billing on 1 July 2025. This paper does not invent a MY utility-template unit price; it treats WhatsApp as a **metered recovery rail** whose unit economics belong in Lazuar’s credit wallet, not in a GMV take-rate.
- There is no public census of “who Malaysian SaaS founders actually bill with.” Deal-loss claims below are structured as **likely substitution patterns**, not counted win/loss.

### Primary sources fetched 16 August 2026

| Source | URL | Used for |
| --- | --- | --- |
| Stripe Pricing (MY) | https://stripe.com/pricing (geo `en-my`) | Cards, FPX, GrabPay, Alipay, Billing 0.7%, Invoicing 0.4%, Radar, Connect, Payment Links, Checkout, Metronome allotment |
| Stripe Tax country support | https://docs.stripe.com/tax/supported-countries | MY = digital products / Service tax; **business location not supported** |
| Stripe Tax APAC | https://docs.stripe.com/tax/supported-countries/asia-pacific | Remote-seller-only calculation outside AU/HK/JP/NZ/SG/AE; Stripe does not file |
| Stripe MY SST explainer | https://stripe.com/resources/more/malaysia-sst-rate | SST rate structure 5/10 goods, 6/8 services; registration thresholds |
| Stripe Malaysia e-invoice help | https://support.stripe.com/questions/understanding-e-invoicing-requirements-for-malaysia | Stripe issues e-invoices for **its own fees**, not merchant-to-buyer UBL |
| Stripe Metronome close | https://stripe.com/newsroom/news/stripe-completes-metronome-acquisition | Acquisition completed 14 Jan 2026 |
| Adyen Pricing | https://www.adyen.com/pricing | $0.13 + method fee; MY FPX $0.13+$0.52; DuitNow 1.5%; GrabPay MY 1.5%; Boost 2.5% |
| Paddle Pricing | https://www.paddle.com/pricing | **5% + 50¢** per checkout transaction; custom at volume; sub-$10 SKUs quote |
| Polar Pricing | https://polar.sh/resources/pricing | Starter 5%+50¢; Pro/Growth/Scale lower bps; +1.5% intl cards; Early Member 4%+40¢ |
| Polar Why / MoR | https://polar.sh/resources/why | Polar is MoR; payouts via Stripe Connect Express |
| Lemon Squeezy 2026 update | https://www.lemonsqueezy.com/blog/2026-update | LS + Stripe Managed Payments; brand in transition |
| Gumroad Pricing | https://gumroad.com/pricing | 10%+$0.50 direct; 30% Discover; MoR since 1 Jan 2025 |
| Payhip Pricing | https://payhip.com/pricing | $0+5% / $29+2% / $99+0%; processor extra |
| Chargebee Pricing | https://www.chargebee.com/pricing/ | Flow **0.80%** of monthly billing value; $99+0.65% commit; Enterprise Plus custom |
| Lago Pricing | https://getlago.com/pricing | Premium quote-only; OSS deploy remains |
| Anrok Pricing | https://www.anrok.com/pricing | $100 / market / month (SaaS); $50 ecom; Custom for 10+ markets / e-invoicing |
| Quaderno Pricing | https://quaderno.io/pricing/ | $29 / $49 / $99 / $149 by transaction volume |
| FastSpring Pricing | https://fastspring.com/pricing/ | Revenue-share, quote-only, all-in MoR |
| Checkout.com Pricing | https://www.checkout.com/pricing | Custom flat-rate or IC++; no public grid |
| ClearTax MY e-invoice | https://www.cleartax.com/my/en/e-invoicing-malaysia | Phase 4, RM1m exemption, RM10k no-consolidation, UBL 2.1, 55 fields |
| IRBM SDK | https://sdk.myinvois.hasil.gov.my/documents/invoice-v1-0/ | Invoice is UBL 2.1 |
| Autumn | https://useautumn.com / https://docs.useautumn.com/welcome | Open-source layer **on top of** Stripe Billing |
| Finix vs Stripe | https://finix.com/finix-vs-stripe | Finix from $99/mo or IC+; Connect $2 + 0.25%+$0.25 (Finix’s June 2026 comparison) |
| WhatsApp Business Platform | https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing | Per-message template rates by market–category |

Secondary (class D) used only where labelled: Flexprice Stripe 2026 fee stack; Swell / Baremetrics Chargebee historical Starter/Performance; Vendr FastSpring median ACV; Lago blog on Metronome ~$1B; ThriveCart monthly-vs-lifetime 2026 posts (official page not fully public); Recurly/Zuora/Maxio quote ranges from competitor pages.

---

## Category map

### The eight named categories (and three that research added)

These are **not one TAM**. A founder can — and often does — stack three of them. Lazuar’s job is to sit in a **ninth place**: the compliance-and-ledger layer above rails, not inside any one of the eight.

```text
                         MONEY MOVEMENT                         COMMERCE LOGIC
                         (who touches the card / bank)          (who owns the subscription)

  Global payment OS      Stripe  Adyen  Checkout.com            Checkout / Payment Links /
                         Braintree·PayPal  Square               Billing / Radar live here
                                                                but are sold as the OS

  Merchant of Record     Paddle  Lemon Squeezy (Stripe)         They ARE the merchant.
                         FastSpring  Polar.sh  Gumroad-as-MoR   Tax + payout + support
                                                                bundled into take-rate

  Subscription engines   (any PSP underneath)                   Chargebee  Recurly  Maxio
                                                                Zuora  Lago  Orb  Metronome

  Creator checkout       Gumroad  Payhip  ThriveCart            Storefront + upsells +
                         SamCart  (Stan / Whop adjacent)        file delivery. Often MoR
                                                                or BYO Stripe.

  Invoicing + tax        Stripe Tax  TaxJar  Avalara            Calculation + filing.
                         Quaderno  Anrok  Vertex                Almost never MyInvois UBL.

  Open-source / self-host Lago  Kill Bill  Solidus              You run the box.
                         BTCPay (crypto adjacent)

  Embedded finance/BaaS  Finix  Stripe Connect                  Split payouts, sub-merchants,
                                                                PayFac-as-a-service

  Developer-first billing Polar  Autumn  Lago  Orb              Usage, credits, entitlements
                         (schemaless / AI-metering tools)       as the product
```

Research warranted three extra rows. They are not in the original brief but they show up in the same buying conversation:

| Extra category | Names | Why it matters to Lazuar |
| --- | --- | --- |
| **Payment orchestration** | Spreedly, Primer, Basis Theory, Vault-and-Forward | Enterprises already decided “we will never be single-homed on Stripe.” Lazuar is a **thin orchestrator for Asian APMs + compliance**, not a Primer competitor. |
| **New-wave MoR for indie SaaS** | Creem, Dodo Payments, InflowPay, Polar (already named) | They attack Paddle’s 5%+50¢ from below. They still do **not** do LHDN. They will steal “I just want a link” founders if Lazuar looks like work. |
| **CTC / government e-invoice specialists** | ClearTax, Storecove, Sovos, Avalara (e-invoicing SKU), local MyInvois middleware | They own **submission**, not checkout. Partner or integrate; do not become a standalone e-invoice bureau that forgot the Buy button. |

### Where Lazuar sits (category strategy)

**We are not trying to be Stripe. We sit above rails.**

Stripe, Adyen, Checkout.com, Billplz, CHIP, Xendit, and Razorpay move money. They are **vendors our merchants already have**, or will have. Lazuar’s product is the layer that:

1. **Normalizes** those rails into one checkout session, one ledger, one subscription state machine, one webhook out.
2. **Complies** at the moment of sale (SST classification, LHDN UBL 2.1 / MyInvois validation, consolidation vs individual, export zero-rating).
3. **Recovers** failed money on the channel the buyer actually reads (WhatsApp first in MY/SEA, email as fallback).
4. **Monetizes** the software and the compliance/comms compute — **not** the GMV.

That is a different company from:

- Stripe (the rail + the gravity well),
- Paddle (the merchant of record),
- Chargebee (the billing OS that still needs a rail and a tax product),
- Gumroad (the creator storefront),
- Avalara (the tax engine with no cash register),
- ClearTax (the e-invoice pipe with no checkout).

If Lazuar starts acquiring, holding funds, building a website builder, or becoming a marketplace, it has left this seat.

### Relation cheat-sheet (one line each)

| Category | Default relation | One-line reason |
| --- | --- | --- |
| Global payment OS | **Integrate** (Stripe, Billplz, CHIP, Xendit, Razorpay). **Ignore** as a peer (Adyen, Checkout.com, Square). **Do not compete** on acquiring. | They are BYOK targets or enterprise-only. |
| Merchant of Record | **Compete on ICP that must stay the seller of record. Ignore / lose on “I never want a tax ID.” Never become one.** | MoR is a different legal product. |
| Subscription billing engines | **Compete on mid-market Asian SaaS that will not pay Chargebee bps. Integrate as a possible upstream later. Do not chase Zuora RFPs.** | Own simple-to-medium billing + local compliance; lose on CPQ/RevRec. |
| Creator checkout | **Compete on professional Asian creators who outgrew Gumroad fees and need legal invoices. Ignore hobby $0 GMV.** | Headless + BYOK + tax, not storefront themes. |
| Invoicing + tax | **Partner / integrate** Anrok or Quaderno for *outbound* US/EU VAT if we ever sell that motion. **Compete** on MY/ID/IN government e-invoice at POS. | Stripe Tax does not file MyInvois. |
| Open-source / self-host | **Ignore as a sales competitor. Steal ideas. Do not open-core the compliance moat on day one.** | Self-hosters are not the ICP. |
| Embedded finance / BaaS | **Ignore as a product. Do not become a PayFac. Integrate Connect only if a platform tenant needs it.** | Platforms are a different company. |
| Developer-first billing | **Compete on DX of the checkout + webhook. Partner conceptually with Autumn-shaped thinking (sit on rails). Do not become an AI metering company.** | Usage billing is a feature, not the company. |

### Category strategy, written as a rule

Stripe taught the world that **payments, billing, tax, fraud, and payouts can live in one brand**. That is the gravity well. Every founder’s first architecture is “just use Stripe.” Lazuar’s strategy is not to unbundle Stripe for Americans. It is to **re-bundle the jobs Stripe refuses to finish in Malaysia and the rest of CTC-Asia**: local bank rails at local cost, government e-invoice in UBL 2.1, SST that is not VAT, WhatsApp as the recovery channel, and a ledger that still balances when the rail is Billplz.

Paddle taught indie SaaS that **tax fear has a price: 5% + 50¢**. That price is rational for a US/EU SaaS selling $29/mo globally with no finance hire. It is irrational for a Malaysian Sdn Bhd that already has an LHDN TIN, already must issue MyInvois documents, and already has a Billplz account. Lazuar exists for the second founder.

Chargebee taught finance teams that **billing is a system of record**. Lazuar must respect that gravity — hosted checkout, customer portal, dunning, entitlements, invoices, webhooks — without trying to become Zuora.

The rest of this paper is the evidence for those three sentences.

---

## Category dossiers

Each dossier answers the same four questions, then expands.

1. What **job** the category owns.
2. Who the **category leader** is (expectation-setter, not “best”).
3. How **Lazuar should relate**.
4. A **feature table vs the Lazuar thesis**.

---

### 1. Global payment OS

#### Job they own

Move money, vault cards, raise authorization rates, issue payouts, and — increasingly — sell the **adjacent** products (Billing, Tax, Radar, Connect, Issuing) so the merchant never leaves. The OS is not “a checkout page.” The OS is **the default financial backend of the internet**, plus a design language (PaymentIntents, Customers, Webhooks, Dashboard) that every developer has already learned.

The jobs inside the OS that founders actually name:

- **Accept**: cards, wallets, and local APMs at the highest possible auth rate.
- **Optimize**: 3DS, network tokens, Adaptive Acceptance, retries.
- **Protect**: Radar / Ethoca / Verifi / 3DS liability shift.
- **Payout**: standard and instant; multi-currency; connected accounts.
- **Productize**: Checkout, Payment Links, Invoicing, Billing, Tax, Sigma, Revenue Recognition.
- **Platformize**: Connect / Adyen for Platforms / Checkout.com platforms.

They do **not** own: being the merchant of record; filing MyInvois; speaking Bahasa to a buyer on WhatsApp; consolidating B2C receipts on the 7th of the month.

#### Category leader

**Stripe.** Not because Adyen cannot beat them on enterprise auth rates in Europe, and not because Checkout.com is not a Forrester 2026 “Leader.” Stripe is the leader because it is the **default answer** a developer, a YC partner, a Malaysian indie hacker, and a Series B CFO will all say out loud. Feature gravity in later sections is almost entirely Stripe-shaped.

Adyen is the leader for **global unified-commerce enterprises** (online + in-store + in-app, local acquiring, IC++). Checkout.com is the leader-adjacent **enterprise online acquirer**. Braintree/PayPal is the leader for **“we must show the PayPal button.”** Square is the leader for **US/Western SMB in-person**, not for Asian headless SaaS.

#### How Lazuar should relate

- **Integrate** Stripe as a first-class BYOK rail (already in the product thesis). Treat Checkout, Payment Links, and Elements as **the UX bar**, not as a product to clone pixel-for-pixel.
- **Integrate** local OS-equivalents the same way: Billplz, CHIP, Fiuu, Xendit, Razorpay. These are the actual money pipes for MY/ID/IN.
- **Do not compete** with Stripe on card acquiring, Radar ML, network tokens, Issuing, Treasury, or Atlas.
- **Ignore** Adyen and Checkout.com as sales competitors for v1. Their sales motion is RFP, minimums, and a solutions engineer. Our ICP cannot buy them.
- **Ignore** Square. Different continent, different buyer, hardware gravity.
- **Partner** with Stripe only in the boring sense: listed integration, well-tested webhooks, honest “this is your Stripe account” copy. Do not become a Connect platform that wraps Stripe and takes bps unless we explicitly decide to become a platform company (we should not).

#### Feature table vs Lazuar thesis

| Job | What the OS sells | Lazuar thesis |
| --- | --- | --- |
| Card acquiring | Stripe 3% + RM1.00 domestic MY; +1% intl; +2% FX. US still marketed as 2.9% + 30¢ elsewhere. | **Not our job.** Merchant’s Stripe key. We never mark up the card. |
| FPX / local bank | Stripe MY: **3% + RM1.00** for FPX (same as cards). Adyen: **$0.13 + $0.52**. Local PSPs: sen-level. | **Orchestrate the cheap rail.** Default MY checkout should not be Stripe FPX. |
| Hosted checkout | Stripe Checkout / Payment Links; Adyen Drop-in; PayPal Smart Buttons | **Own the cash register UX**, rail-agnostic. One session, many rails. |
| Payment Links | Stripe: included with Payments; custom domain **US$10/mo**; post-pay invoice 0.4% cap $2 | **Core product.** Headless Buy URL. No CMS. |
| Subscriptions | Stripe Billing **0.7% of Billing volume** (MY page, 16 Aug 2026) | **Own simple–medium subscriptions** on our ledger. Do not charge 70 bps. |
| Tax calc | Stripe Tax 0.5% no-code / 50¢ API (global US page); MY = digital + service tax only; **MY business location unsupported** | **Own MY SST + LHDN.** Do not pretend to be Avalara for 50 US states. |
| Fraud | Radar Lite included; Radar teams from ~RM0.23 / screened tx (MY) | **Pass through** Stripe Radar when the rail is Stripe. Do not rebuild ML. |
| Connect / platforms | $2 / active account + 0.25% + 25¢ payout (global Connect page); MY Connect “included” or 0.25% if platform sets price | **Ignore.** We are not a marketplace PayFac. |
| Disputes | MY: **RM90** received + RM90 countered; Smart Disputes 30% of won amount | Merchant’s problem on their MID. We surface status; we do not underwrite. |
| Payouts / holding | Stripe holds and pays out on its schedule; Instant Payouts 1% (min RM2) | **We never hold funds.** Instant settlement is the BYOK promise. |
| Developer API | The industry dialect (PaymentIntents, webhooks, Customers) | **Speak Stripe-shaped JSON** so integrators feel at home, without pretending we are Stripe. |
| Government e-invoice | Stripe issues **its own** fee e-invoices to MY accounts. Does not emit merchant UBL 2.1 to MyInvois for the merchant’s sales. | **This is the company.** |

#### Stripe (Checkout, Payment Links, Billing, Tax, Connect, Radar)

**What it is.** The programmable financial services company. In Malaysia it is a live acquirer with a public price, not a rumour. The 16 August 2026 MY pricing page states:

- **3% + RM1.00** per successful card charge or successful **bank transaction**.
- Same **3% + RM1.00** for **FPX**.
- **+1%** international cards; **+2%** if currency conversion is required.
- Alipay **2.9% + RM1.00**; GrabPay **3%**.
- Dispute received **RM90**; dispute countered **RM90**; Smart Disputes **30%** of won amount.
- Instant Payouts **1%** (min RM2).
- Payment Links and Checkout **included** with Payments.
- Custom domain on Checkout / Customer Portal **US$10 / month**.
- Post-payment invoices **0.4%** of total, **US$2 cap**.
- Billing **0.7% of Billing volume** (includes Billing transactions on and off Stripe; excludes one-off invoices).
- Invoicing **0.4% per paid invoice**.
- Revenue Recognition **0.25% of volume**.
- Data Pipeline **RM0.10 / transaction**; Sigma from **RM50 / month** + per-charge.
- Radar pay-as-you-go from **RM0.23** per screened transaction.
- Metronome (now a Stripe product) Starter: **US$100,000 billing allotment**, 10 million usage events, then overage; Enterprise custom.
- Terminal in-person **2.8% + RM0.50** domestic, plus hardware SKUs in RM.
- Connect on the MY page: Stripe can set pricing for users (“included”); platforms that set their own price start at **0.25%**.

**Why founders pick it.** Documentation, brand trust, Link one-click, Radar, the Billing object model, and the fact that every code sample on the internet is Stripe. For a US-incorporated SaaS selling globally, “just Stripe” is still the correct default.

**Where it fails Lazuar’s ICP.**

1. **FPX at 3% + RM1 is a bad joke next to Billplz/CHIP.** A RM100 course that should have cost ~RM1 of rail cost costs RM4. That single number is why Malaysian creators bounce off Stripe Payment Links.
2. **Stripe Tax does not treat a Malaysian business as a supported business location.** The official country table (16 Aug 2026) lists Malaysia as: product type **Digital products**, tax type **Service tax**, **your business location ❌ Not supported**, customer location ✓. Stripe can help a *foreign* digital seller collect Malaysian service tax. It cannot be the domestic SST + LHDN operating system for an Sdn Bhd.
3. **Stripe does not file.** APAC docs are explicit: “You’re responsible for filing and remitting your taxes. Stripe doesn’t file taxes on your behalf.” Filing partners (Taxually, Marosa, HOST) are VAT/GST/sales-tax shaped, not MyInvois-shaped.
4. **Stripe’s Malaysia e-invoice help article is about Stripe invoicing *its own fees* to the account holder**, collecting the merchant’s TIN / SST / BRN so *Stripe Payments Malaysia Sdn Bhd* can stay legal. It is not a merchant-to-buyer UBL 2.1 pipeline.
5. **Billing is 70 extra basis points**, and usage at AI scale was so weak that Stripe **bought Metronome** (close announced 14 January 2026; press ~US$1B, class D) instead of finishing Billing’s 2018 data model.
6. **WhatsApp is not a Stripe channel.** Dunning is email / in-app / Smart Retries. MY buyers do not live in email.
7. **SST is not VAT.** Stripe’s mental model (destination VAT, OSS, economic nexus, product tax codes) maps poorly onto Malaysia’s sales-tax-at-manufacture + listed service tax + export zero-rating + LHDN income-tax e-invoice (which is a *different* document from the SST tax invoice).

**Lazuar implication.** Stripe is the **BYOK card rail** and the **feature-bar teacher**. It is not the product. Never advertise “we are cheaper Stripe.” Advertise “your Stripe key still works; your FPX does not cost 3%; your LHDN document actually exists.”

#### Adyen

**What it is.** Unified-commerce acquirer (online, in-app, in-store) with local acquiring licences and **Interchange++** as the honest card price. Public method table (16 Aug 2026): **US$0.13 processing + method fee**, no setup fee, but a **minimum invoice** “depending on industry or business model” (official FAQ). Malaysia methods on that table include:

- Online banking Malaysia (FPX): **$0.13 + $0.52**
- DuitNow: **$0.13 + 1.5%**
- GrabPay Malaysia: **$0.13 + 1.5%** (in-person same); some GrabPay SKUs at 6%
- Boost Wallet: **$0.13 + 2.5%**
- Touch ’n Go Digital MY: **$0.13 + 1.60%**
- 7-Eleven Malaysia: **$0.13 + 4%**
- Atome SG/MY: **$0.13 + 5%**
- Alipay / Alipay+: **$0.13 + 3%**

Adyen launched local acquiring in Malaysia in 2020. FPX docs say they offer FPX via **Razer Merchant Services (RMS / formerly MOLPay)** — i.e. even Adyen is sitting on a local partner for the rail Lazuar can talk to directly.

**Why enterprises pick it.** Auth rates, IC++ transparency, one platform for store + web, Uplift / revenue-optimization tooling, settlement-currency choice.

**Why Lazuar’s ICP will never see a sales call.** Minimums, integration weight, no self-serve “paste a key and get a Payment Link.”

**Relation.** **Ignore** as a competitor. **Do not** build an Adyen adapter until a paying tenant asks. If we ever do, it is just another BYOK rail.

#### Checkout.com

**What it is.** Enterprise online payments. Forrester 2026 Merchant Payments Providers names them a Leader alongside Stripe and Adyen (vendor-circulated claim). Official pricing page is **quote-only**: fully flat-rate or interchange++, free for charities. Vendor homepage claims **$300bn** ecommerce volume processed in 2025, local acquiring in **50+** countries, **150+** processing currencies, 19 offices.

**Relation.** **Ignore.** Same buyer as Adyen. No MY SMB motion. No MyInvois story.

#### Braintree / PayPal

**What it is.** PayPal’s developer gateway plus the PayPal wallet. US Braintree fee page (updated 7 May 2026) lists cards/wallets at **2.89% + $0.29**, +1% non-USD presentment, +1% non-US issued card, **$15** chargeback. PayPal remains the “I do not have a card / I do not trust this site” button, especially for US/EU consumers buying from unknown Asian sellers.

**Where it matters to Lazuar.** Cross-border B2C into the US/EU sometimes **requires** a PayPal button or conversion dies. That is a **rail**, not a strategy.

**Relation.** **Integrate later** as a BYOK PayPal / Braintree button if a tenant’s US conversion data demands it. **Do not** become “PayPal for Asia.” PayPal’s merchant onboarding, holds, and 21-day rolling reserves are the opposite of the BYOK instant-settlement promise.

#### Square

**What it is.** SMB unified commerce: hardware POS, payments, payroll, banking, booking add-ons. Gravity is **in-person US/AU/UK/JP**. Not a headless CaaS for Asian digital sellers.

**Relation.** **Ignore.** If a tenant is a café, they should use Square or Qashier, not Lazuar. Do not add a card reader roadmap to “match Square.”

#### What this category teaches buyers (even when they buy us)

- A hosted checkout that looks like Stripe Checkout.
- Payment Links you can paste into Instagram / WhatsApp.
- A customer object, a subscription object, a webhook you can trust.
- Test mode + workbench + request logs.
- Dashboard graphs that match the bank.
- Idempotent APIs and signed webhooks.

Lazuar must offer those **jobs**. Lazuar must not offer Stripe’s **P&L**.

---

### 2. Merchant of Record

#### Job they own

**Be the legal seller.** The MoR is the merchant the card network, the tax authority, and the chargeback department see. The software vendor (the “seller of the product”) is a supplier to the MoR. In exchange for 5–8% of GMV, the founder is promised:

- no VAT/GST/sales-tax registrations in 50–100 jurisdictions,
- no PCI,
- buyer support on payment failures (“billing support” in Paddle’s language),
- fraud and chargeback handling,
- a checkout and a subscription object,
- payouts on the MoR’s schedule, in the MoR’s supported countries.

This is a **legal product** wearing a checkout UI. It is not “Stripe with tax included.” The tax included is **the MoR’s tax**, because the sale is the MoR’s sale.

#### Category leader

**Paddle** for SaaS / software (expectation-setter on price: **5% + 50¢**, the number every comparison post repeats). **Lemon Squeezy** was the leader for “beautiful digital-goods MoR” until Stripe bought it in 2024 and, by January 2026, redirected the roadmap into **Stripe Managed Payments**. **FastSpring** is the older enterprise digital-goods MoR (quote-only, historically ~5–8%). **Polar.sh** is the 2025–2026 developer-native challenger with a **public** rate card that undercuts Paddle once you pay $20–$400/mo. **Gumroad** became an MoR on 1 January 2025 and is the leader for *hobby* digital goods, not for B2B SaaS.

#### How Lazuar should relate

- **Compete** for Malaysian / SEA / India founders who **must remain the seller of record** (LHDN income is *their* income; SST is *their* SST; corporate buyers need *their* TIN on the invoice; banks want *their* MID).
- **Lose honestly** to Paddle/Polar when the founder is US/EU-incorporated, has no TIN, sells $19–$49 digital globally, and has explicitly said “I will never register for VAT.”
- **Never become an MoR.** The moment we take title to the sale we inherit chargebacks, sanctioned-goods policy, adult-content policy, payout KYC, and 5% economics we already rejected in ADR 019.
- **Do not partner** as a reseller of Paddle. Aura can keep Paddle as **System A** (the salon paying Aura). Lazuar is **System B** (the guest paying the merchant). Mixing those stories is how you accidentally become a MoR in marketing copy.

#### Feature table vs Lazuar thesis

| Job | Typical MoR | Lazuar thesis |
| --- | --- | --- |
| Legal seller | MoR | **Merchant.** Always. |
| Take-rate | Paddle / Polar Starter / LS: **5% + 50¢**. Polar Scale: 3.4% + 30¢ + $400/mo. Gumroad: 10% + 50¢ (30% Discover). FastSpring: quote, often cited 3.9–5.9% (D). | **0% of GMV.** SaaS + prepaid credits. |
| Global VAT/GST/sales tax | MoR files as itself | We file **the merchant’s** LHDN / SST. We do not absorb EU OSS. |
| Payout timing | MoR schedule, extra KYC, reserves | Instant to merchant MID |
| Chargebacks | MoR handles (and can suspend you) | Merchant’s MID, merchant’s risk |
| Buyer support | Paddle sells 24/7 billing support as a feature | Merchant’s brand. We give tools, not a call centre. |
| Restricted products | Aggressive AUPs (adult, some info-products, crypto, some medical) | Merchant’s acquirer decides. We are software. |
| Local rails | Weak or expensive (card-centric) | **First-class FPX / DuitNow / QR / Xendit VA / UPI** |
| LHDN / MyInvois | Absent. Sale is often a foreign MoR → export / out-of-scope mess for the founder’s income-tax file | **Native** |
| High-ticket B2B | Painful (MoR invoice is the wrong legal document for a RM30k enterprise buyer) | Pillar 2 of ADR 021: TIN at checkout, their invoice, their QR |

#### Paddle

**Official price (16 Aug 2026):** **5% + 50¢ per Checkout transaction**, no monthly fee, “no hidden extras.” Custom pricing for scale. Sub-$10 products and invoicing are **quote-only**. Includes checkout, subscriptions, tax and compliance, fraud, reporting, customer/billing support, migration. Invoicing, advisory, and implementation are extra SKUs behind a demo.

Paddle’s own comparison table claims a typical PSP stack lands at “~7% and above” once you add tax, fraud, FX, and invoicing — that is **marketing math** (class E as a number, class C as a positioning claim). The real insight is the **job**: Paddle sells **sleep**. Tailwind’s published quote on that page is the catechism: Stripe/PayPal might be 3.5–4.5% cross-border, but “is any saving worth the admin burden and opening yourself up to extra scrutiny from the tax agencies?”

**When we lose to Paddle.** US/EU indie SaaS; founders who have already been burned by VAT letters; anyone whose finance hire is “none”; anyone whose bank is not Malaysian and whose customers are not Malaysian. Aura itself uses Paddle as SaaS billing — that is the correct use of an MoR (the *software vendor* selling to the salon).

**When we should win.** Sdn Bhd / Pte Ltd / PT / Pvt Ltd that already has a tax identity, already must e-invoice, already hates 5% of GMV, already has a local MID, and sells a mix of MY and export.

**Paddle’s MY failure mode.** Paddle is the seller. The Malaysian founder’s LHDN file then has to explain a pile of **export / MoR / agency** documentation that most small accountants do not enjoy. Corporate buyers who need an LHDN-validated invoice from *the consultant they hired* will not accept a Paddle Ltd invoice. FPX is not the native motion. WhatsApp recovery is not the product.

#### Lemon Squeezy (Stripe)

Stripe acquired Lemon Squeezy in 2024. The 28 January 2026 CEO post says they spent a year building Stripe’s **Managed Payments** MoR product, and admits support and product velocity suffered. The public LS storefront still exists; the strategic identity is **Stripe’s MoR skin**.

**Price (still widely published, confirm on LS pricing before quoting in a contract):** **5% + 50¢**, same headline as Paddle.

**Relation.** Treat LS as **Stripe’s future MoR SKU**, not as an independent company we out-feature. If Stripe Managed Payments ships as a one-click “make Stripe the MoR,” it will steal US/EU hobby and indie volume from Paddle *and* from us if we ever pitch those founders. It will still not issue MyInvois documents for an Sdn Bhd.

#### FastSpring

Official pricing is **revenue-share, quote-only, all features included, no “processor-only” SKU.** Vendr (class D) reports median contract ~US$21k/year and published starting rate **5.9% of gross**, negotiating toward 3.9–5.4%. FastSpring is the 2000s–2010s digital-goods MoR (desktop software, games, established ISVs). Sales-led, not indie-self-serve.

**Relation.** **Ignore** for v1. If we meet them, it is in an ISV RFP we should lose.

#### Polar.sh

Polar is the honest 2026 MoR for developers: public rates, usage billing, GitHub-native heritage, open-source adjacent, payouts via **Stripe Connect Express**. Official grid (16 Aug 2026):

| Plan | Monthly | Per transaction | Support |
| --- | --- | --- | --- |
| Starter | Free | **5% + 50¢** | Standard |
| Pro | **$20** | **3.8% + 40¢** | Prioritized |
| Growth | **$100** | **3.6% + 35¢** | Prioritized |
| Scale | **$400** | **3.4% + 30¢** | Slack + prioritized |

Breakeven vs Starter (Polar’s own math): Pro ~$1,379/mo sales; Growth ~$5,634; Scale ~$19,048. Early Member orgs created before **27 May 2026** keep **4% + 40¢** (+0.5% subscriptions) forever until they upgrade. Extra: **+1.5% international cards**; Stripe payout pass-through **$2/mo active + 0.25% + $0.25**; FX 0.25–1%; disputes **$15**.

Polar is also a **developer-first billing** product (usage meters, seats, credits, benefits). It sits in two categories on purpose.

**Relation.** **Compete** for the same indie-SaaS Twitter mindshare with a *different* promise (BYOK + Asia compliance, not cheaper MoR). **Do not** race Polar on published bps — they will always be allowed to cut MoR premium because they *are* the merchant. **Steal** their documentation tone and public-rate honesty.

#### The MoR trap (why we will be tempted anyway)

Every support ticket that says “can you just handle VAT for my US sales?” is an invitation to become Paddle. The correct product answer is:

- For **export** from a MY entity: classify as export / zero-rated where the law says so, emit the correct LHDN document, and let the *buyer’s* jurisdiction be the buyer’s problem — or integrate a **tax engine** (Anrok/Quaderno) as a module, still with the merchant as seller.
- For founders who refuse to be the seller: **send them to Paddle/Polar** with a blessing. That is not a lost customer; that is a customer we would have hated.

---

### 3. Subscription billing engines

#### Job they own

Be the **system of record for recurring money**: product catalog, price points, subscriptions, invoices, credit notes, dunning, entitlements, usage meters, quotes, amendments, revenue recognition, collections. The rail is pluggable (Chargebee advertises **40+ gateways**). The billing engine is the product.

This category exists because Stripe Billing is “good enough until it is not”: usage at AI scale, multi-entity, account hierarchy, CPQ, ASC 606, gateway independence, finance-grade amendments.

#### Category leader

**Chargebee** is the mid-market expectation-setter (the name a Series A finance lead puts on the shortlist). **Zuora** is the enterprise / Quote-to-Cash ceiling. **Recurly** is the older subscriber-management specialist. **Maxio** (Chargify + SaaSOptics) is billing + monetization analytics / RevRec. **Lago** is the open-source / modern usage leader by developer conversation. **Orb** is the developer usage specialist. **Metronome** was the AI-scale metering leader and is now **a Stripe product** (acquisition closed 14 January 2026).

#### How Lazuar should relate

- **Compete** on the slice Chargebee used to call “Starter”: SaaS and creator subscriptions with a few plans, seats, coupons, trials, a customer portal, dunning, and invoices — **plus** local rails and LHDN.
- **Integrate** Chargebee only in the unlikely event a tenant already runs Chargebee and wants Lazuar solely as the MY compliance cashier. That is a niche; do not build it speculatively.
- **Ignore** Zuora RFPs, Maxio RevRec bake-offs, and Orb/Metronome event-per-second wars.
- **Steal** Lago’s lesson: usage and credits are table-stakes *language* even if v1 only has flat + per-seat.
- **Do not** take 80 bps of billing volume. That is how Chargebee Flow prices; it is the opposite of BYOK + utility wallet.

#### Feature table vs Lazuar thesis

| Job | Category leader behaviour | Lazuar thesis |
| --- | --- | --- |
| Product catalog | Plans, prices, addons, charges, entitlements | **Need this.** Keep it smaller than Chargebee’s 2.0 catalog. |
| Hosted checkout / payment links | Chargebee hosted + drop-in; Recurly.js | **Core.** |
| Customer portal | Self-serve update card, cancel, invoices | **Need this.** Phase-gated, not Kajabi. |
| Dunning | Chargebee Smart Dunning / retries; Recurly; Stripe Smart Retries | **Need this**, WhatsApp-first. Honest about today’s email-only. |
| Multi-gateway | Chargebee 40+; Recurly multi; Stripe = Stripe | **BYOK Asian + Stripe.** Quality over 40 logos. |
| Usage / meters | Lago, Orb, Metronome, Chargebee Flow 100M events | **Later.** Do not become an AI billing company. |
| CPQ / quotes | Chargebee CPQ, Zuora CPQ | Thin **B2B quote + TIN** (Pillar 2). Not Salesforce CPQ. |
| RevRec / ASC 606 | Maxio, Zuora, Chargebee RevRec | **Ignore.** Xero/QBO sync is enough. |
| Multi-entity / hierarchy | Chargebee Enterprise Plus, Zuora | **Ignore** until a paying group asks. |
| Price | Chargebee Flow **0.80%** of monthly billing (official 16 Aug 2026); or $99 + 0.65% commit. Historical Starter: free to $250k lifetime then 0.75% (D). Maxio often cited **$599/mo** (D). Zuora entry often cited **tens of k$/yr** (D). Lago Premium quote-only. | **Flat SaaS + credits**, 0% of billing volume. |
| Tax | Integrations to Avalara / TaxJar / Anrok | **Native MY**; partner for US/EU. |
| LHDN | None of them | **Native.** |

#### Chargebee

Official page 16 August 2026 is a **product suite** (Billing, CPQ, RevRec, Growth), not a three-tile Starter/Performance/Enterprise poster.

**Billing / Flow (official):**

- **Pay as you go: 0.80% of monthly billing value, $0 platform fee**, includes **100M usage events/month**, 40+ gateways.
- **Commit monthly: $99/mo + 0.65%** (page calculator; treat as official UI, still confirm in contract).
- **Enterprise Plus:** custom, annual, multi-entity, account hierarchy, contract terms, 500M events, enterprise ACL, warehouse export.

**CPQ:** Lite free for first 50 quotes (Billing customers only); full CPQ talk-to-sales.  
**RevRec:** Performance and Enterprise talk-to-sales.  
**Growth:** Starter $0 (Billing customers); Enterprise custom on active subscribers.

A large 2026 secondary corpus (Swell, Baremetrics, Flexprice, Orb’s Chargebee-pricing post — all class D) still describes:

- Starter **$0** until **$250,000 cumulative lifetime billing**, then **0.75% on all billing** (threshold never resets),
- Performance **$599/mo ($7,188/yr, annual commit)** up to ~$100k monthly billing, same 0.75% overage.

**How to read the contradiction.** Chargebee is mid-reprice toward a **usage-era, bps-of-billing** motion (Flow) while the internet still remembers the land-and-expand Starter trap. For Lazuar’s sales narrative, both matter: founders fear the **0.75–0.80% forever tax on revenue**, and they fear the **$599 cliff**. Our answer is neither.

**Where Chargebee wins deals we will lose.** US/EU B2B SaaS with Salesforce CPQ, multi-entity, need for RevRec, 15 pricing pages, and a finance team that wants a named vendor. Also any team already on Chargebee — switching cost is brutal.

**Where Chargebee fails MY.** No first-class FPX at local cost. No MyInvois. Dunning is email/in-app. Implementation is a project. The bps model punishes the exact high-volume low-ticket B2C creator ADR 021 wants.

#### Recurly

Subscriber-management veteran. Strong at retries, couponing, account updater, high-volume B2C subscriptions (streaming / boxes / media). Pricing is sales-led; secondary posts in 2026 still quote a **Starter around $249/mo** or Growth ~$499 (class D, do not treat as contract). Multi-gateway, not a rail.

**Relation.** **Ignore** as a v1 competitor. Recurly’s buyer is a US subscription brand, not a KL SaaS.

#### Maxio (Chargify)

Chargify billing + SaaSOptics finance. Positioned as billing **plus** cash/RevRec analytics. Secondary consensus (class D): **from ~$599/mo**. Wins when the CFO wants one vendor for bill + recognize.

**Relation.** **Ignore.** Xero/QBO sync is our finance story, not ASC 606.

#### Zuora

Quote-to-Cash for enterprises that have a Zuora *team*. 50+ pricing models, order-to-revenue, Salesforce-native. Pricing unpublished; secondary “$75k entry” figures (class D) are folklore-adjacent — treat as **enterprise-only**.

**Relation.** **Ignore.** If a prospect is running a Zuora RFP, we are in the wrong meeting.

#### Lago

Open-source billing infrastructure (usage + subscriptions + entitlements + invoices + wallets). Customers they claim on the pricing page: **Mistral, PayPal, Groq, Synthesia, Laravel, OVHcloud, 1NCE**, etc. (vendor claims). **Lago Premium** is quote-only for cloud or self-hosted; many “real” features (credit notes, customer portal, branded invoices, analytics, dunning, tax integrations) are Premium/add-on in 2026 commentary (class D, including Meteroid’s “open-core” note). OSS remains cloneable.

**Relation.** **Ignore as a sales competitor** (self-hosters and AI labs). **Study** their event/metric/plan model so our ledger can grow into hybrid pricing without a rewrite. **Do not** open-core LHDN.

#### Orb

Developer-first usage billing: SQL-ish metrics, retroactive price changes, clean API. Pricing historically invoice-based or custom (one 2025 Knock post said $0 then $1/invoice — class D, likely stale). Orb wins AI/API companies that are not ready for Metronome’s sales motion.

**Relation.** **Ignore.** If a tenant needs Orb, they will bring Stripe + Orb and only want us for MY invoices — a custom job, not a SKU.

#### Metronome (now Stripe)

Closed into Stripe 14 January 2026. Stripe’s own MY pricing page already sells Metronome as the usage SKU (US$100k allotment / 10M events on the included plan). Lago’s Feb 2026 post (class D) argues Stripe paid ~$1B because Billing’s 2018 model could not stream AI events.

**Relation.** **Ignore as a company; track as a Stripe capability.** When Stripe can meter like Metronome *and* tax like Avalara *and* talk to MyInvois, the global wedge narrows. That last clause is the long-term risk, and it is years away in MY.

---

### 4. Creator checkout

#### Job they own

Turn a followership into a receipt with as little setup as possible: product page, checkout, file delivery or course access, email to the buyer, a payout. The category oscillates between **take-rate storefronts** (Gumroad, Payhip free tier) and **flat-fee checkout funnels** (ThriveCart, SamCart). Adjacent: Stan Store, Beacons, Whop, Lemonade-stand-class tools (simple “sell a thing” links without claiming to be Shopify).

They own **persuasion adjacent to the cash register**: order bumps, one-click upsells, affiliates, abandoned carts, “store” themes. This is the CMS trap ADR 015 walked away from.

#### Category leader

**Gumroad** is the cultural leader (the word creators actually say). **Payhip** is the low-fee Gumroad alternative. **ThriveCart** and **SamCart** are the leaders for *marketers who live in funnels* (order bumps, upsells, affiliates). None of them are leaders for B2B SaaS.

#### How Lazuar should relate

- **Compete** for the professional Asian creator / course / community / template seller who has outgrown 10% and needs a real tax invoice, FPX, and a portal — and who already has a Framer or Instagram presence.
- **Ignore** hobbyists whose entire business is “one $9 PDF on Gumroad Discover.”
- **Do not** build themes, course players, community DRM, email marketing, or a marketplace. ADR 021 already killed those vitamins.
- **Steal** Gumroad’s “link in bio to cash” simplicity and ThriveCart’s bump/upsell *as data on a checkout session*, not as a page builder.

#### Feature table vs Lazuar thesis

| Job | Gumroad / Payhip / SamCart / ThriveCart | Lazuar thesis |
| --- | --- | --- |
| Time to first sale | Minutes, no site required | Minutes **if** they have a place to put a URL. We accept that trade. |
| Storefront / themes | Yes (Gumroad, Payhip) | **No. Headless.** |
| Take-rate | Gumroad **10%+$0.50** (30% Discover). Payhip **5% / 2% / 0%** + Stripe/PayPal. SamCart **$79 / $199/mo**, 0% platform (processor extra). ThriveCart historically **$495 lifetime**, 0% platform; 2026 secondaries claim monthly $47/$87 (D). | **0% GMV** + SaaS |
| MoR / tax | Gumroad MoR since Jan 2025. Payhip: EU/UK VAT collect-and-remit claim; other taxes DIY. Funnel tools: BYO Stripe + optional tax %. | Merchant is seller; we do **their** tax. |
| Upsells / bumps | ThriveCart / SamCart reason to exist | Thin, later. Not v1 identity. |
| Affiliates | Common | Phase 3 in the README (Wise/PayPal). Not v1. |
| File delivery | Native | Thin fulfillment (R2 signed URL) if we keep Vault as a wrapper. Not a course LMS. |
| Courses / community | Payhip, SamCart Courses, Whop | **Kill list.** Webhook out to their tool. |
| FPX / DuitNow | Rare or via Stripe-at-3% | **Native cheap rail** |
| LHDN | No | **Yes** |
| WhatsApp | Link pasted by hand | **Recovery + receipt as a channel** |

#### Gumroad

Official 16 Aug 2026: **10% + $0.50** on profile/direct links; **30%** on Discover; no monthly fee; **Merchant of Record since 1 January 2025** (“we handle ALL your tax obligations”). Physical goods prohibited. Memberships supported. Payouts by country (bank or PayPal).

This is the correct product for a designer selling a $12 brush set. It is a **punitive** product at RM200k/year. Discover’s 30% is a marketplace tax — a shape we must never copy.

**Relation.** **Compete** on price + legality for serious sellers. **Lose** on “I need a storefront and I refuse to own a domain.”

#### Payhip

Official: **Free $0 + 5%**, **Plus $29 + 2%**, **Pro $99 + 0%**, all features on all plans, unlimited products/revenue. PayPal/Stripe processor fees extra. Instant payout to the connected processor. EU VAT and UK VAT collect-and-remit claim; other taxes configurable.

Payhip is the **rational Gumroad** for a creator who will not become a software company. Still a storefront. Still no MyInvois. Still processor-dependent for rails.

**Relation.** Closest **price** competitor for mid creators once they hit Pro ($99 + Stripe). We must be cheaper **in total MY cost** (FPX sen vs Stripe 3%) and uniquely legal, not merely $20 cheaper on SaaS.

#### ThriveCart

Classic pitch: **0% transaction fee**, one-click upsells, cart abandonment, one-time license folklore at **$495**. 2026 secondary sources disagree on whether monthly ($47 Standard / $87 Pro+, July 2026) has replaced or sits beside the lifetime SKU. Official campaign pages still push “one tool, one payment, no monthly fees.” Treat pricing as **unstable in 2026**; treat the **job** as stable: funnel checkout for marketers.

**Relation.** **Do not become ThriveCart.** Order-bump logic can be a checkout flag. Page builders cannot.

#### SamCart

Secondary (class D, 16 Aug 2026 corpus): **Core $79/mo** (often $59 annualized), **Pro $199/mo**, 0% SamPay platform fee, processor 2.9%+30¢ typical, Core tax add-on ~0.5%. Courses app, AI assistant, abandonment, affiliates on Pro.

**Relation.** Same as ThriveCart. US marketer tool. Ignore as a MY sales competitor; respect their checkout-CRO checklist (bump, timer, guarantee seal, two-step) as **UX gravity**.

#### Lemonade Stand-class / adjacent

Simple “sell a product” tools without a full LMS: Carrd + Stripe Payment Link, Notion + Polar, Lemon Squeezy overlays, Stan Store, Beacons, Ko-fi, Buy Me a Coffee. The job is **zero ceremony**.

**Relation.** This is the **bottom of the funnel we will never own** if we require a tax profile on minute one. ADR 021 already accepted losing the casual “buy me a coffee” user. Keep a **path** (Payment Link + optional tax later) so we do not lose the professional who is still validating a product.

---

### 5. Invoicing + tax

#### Job they own

Calculate the right indirect tax, collect evidence (certificates, TINs, tax IDs), register, file, and remit — and sometimes emit a human invoice PDF. In the US this is economic-nexus sales tax. In the EU it is VAT + OSS. In Singapore it is GST. In Malaysia the **same English word “tax”** splits into at least three documents:

1. **SST** (Royal Malaysian Customs) — sales tax and/or service tax, registration thresholds, bimonthly returns.
2. **LHDN e-Invoice / MyInvois** (IRBM) — income-tax continuous transaction control, UBL 2.1 XML/JSON, 55 fields, digital certificate, QR, 72-hour cancel window. This is **not** SST.
3. **The commercial invoice / receipt** the buyer wants to expense.

Global tax SaaS almost always means (1) in a VAT/GST/sales-tax ontology. Almost never (2).

#### Category leader

**Stripe Tax** is the leader *inside the Stripe OS* (the default a Stripe merchant clicks). **Avalara** is the enterprise calculation/filing leader. **Vertex** is the ERP-adjacent enterprise leader. **TaxJar** (Stripe-owned) is the US e-commerce self-serve leader. **Anrok** is the 2024–2026 leader for **SaaS-native** global sales tax (Notion, Anthropic, Cursor, Synthesia appear on their site — vendor claims). **Quaderno** is the leader for small global digital sellers who want invoices + VAT without becoming Avalara.

#### How Lazuar should relate

- **Compete** on **Malaysia (and later ID/IN) government e-invoice at the point of sale.** That is not Anrok’s product and not Stripe Tax’s product.
- **Partner / integrate** Anrok or Quaderno **only** if we sell a “your US/EU destination tax” module to export-heavy tenants and do not want to maintain 12,000 jurisdictions. Do not build US sales tax.
- **Ignore** Vertex. Wrong buyer.
- **Do not** pretend Stripe Tax “supports Malaysia” in a sales deck without the footnote: **digital products, service tax, business location not supported, Stripe does not file, Stripe does not emit MyInvois UBL for your sales.**

#### Feature table vs Lazuar thesis

| Job | Stripe Tax / Anrok / Avalara / Quaderno | Lazuar thesis |
| --- | --- | --- |
| Destination VAT/GST/sales tax | Yes, 100+ countries (Stripe); per-market filing (Anrok) | Partner later; not v1 identity |
| MY as **customer** location | Stripe: digital / service tax only | Handle as import of digital services **if** we ever calculate SST on inbound; rare for our ICP |
| MY as **business** location | Stripe Tax: **not supported** | **This is home.** |
| SST calculation (8%/6%/5%/10%, exemptions, thresholds) | Stripe publishes an explainer; product coverage is not a full Customs engine | Need **good-enough** SST on digital/services + export zero-rate. Not a full manufacturer sales-tax system. |
| Nexus / threshold alerts | Anrok, Quaderno, Stripe Tax monitoring (cross-border only) | MY SST threshold (~RM500k services, with sector variants) + LHDN phase — different alerts |
| Filing & remittance | Anrok includes; Stripe uses partners; Avalara/Vertex yes; Quaderno optional | **MyInvois submit** is our filing. SST return still often accountant + MySST. Do not claim we replace the Customs return on day one. |
| E-invoicing (CTC) | Anrok Custom lists “E-invoicing”; Avalara/Sovos/Storecove are the global CTC vendors | **MyInvois UBL 2.1 is the product.** PEPPOL/Clearance in other countries is a later map, not a detour. |
| Invoice PDF | Quaderno’s reason to exist; Stripe Invoicing 0.4% | Receipt + **IRBM QR** + human PDF. |
| Price | Stripe Tax ~0.5% or 50¢/tx (global pages). Anrok **$100/market/mo** SaaS, **$50** ecom. Quaderno **$29–$149/mo**. TaxJar secondaries **$39 / $99** + per-filing (D). Avalara/Vertex quote. | Credits per submission + SaaS. Predictable. Not 50 bps of GMV. |

#### Stripe Tax

Global product: calculate/collect in 100+ countries; filings in 90+ via partners; no-code **0.5%** per transaction where registered; API **50¢** per transaction (10 calc calls included). MY-specific facts from official docs (16 Aug 2026):

- Country table: **Digital products / Service tax / business location ❌ / customer location ✓**.
- APAC essay: outside AU, HK, JP, NZ, SG, AE, Stripe Tax is for **remote sellers with no physical presence**, and **only digital products**.
- “Stripe doesn’t file taxes on your behalf.”
- Threshold monitoring is for sales **outside** the country the business is based in.

**Relation.** When the rail is Stripe and the buyer is in the EU, we may **read** Stripe Tax amounts into the ledger. We must never say “Stripe Tax does LHDN.”

#### TaxJar

Stripe-owned, US e-commerce DNA. 2026 TaxJar blog (class A for their own prices): Starter from **$39/mo**, Professional from **$99/mo**, filing credits extra (~$50–$55), registration **$299/state**. Not SST-certified (Quaderno’s Aug 2026 attack post, class E as a weapon, class D as a hint). Irrelevant to MyInvois.

**Relation.** **Ignore.**

#### Avalara

Enterprise AvaTax calculation + returns + (separately) e-invoicing in some CTC countries. Quote-only, implementation-heavy, Shopify history is messy (Avalara’s own comparison pages acknowledge complexity). Wins when a tax team already exists.

**Relation.** **Ignore** as a competitor. **Possible future connector** if a multi-national tenant already has an Avalara contract and wants us only as the MY cashier.

#### Quaderno

Official 16 Aug 2026:

| Plan | Price | Transactions / mo |
| --- | --- | --- |
| Hobby | **$29** | 25, 1 user, 1 jurisdiction, 1 integration |
| Startup | **$49** | 250 |
| Business | **$99** | 1,000 |
| Growth | **$149** | 2,500 |
| Enterprise | Quote | 2,500+ |

Includes calculations, nexus alerts, invoices, Quaderno Checkout, reports; overage = auto-upgrade, not surprise bps. Optional registration & filing service.

Quaderno is the closest **philosophical** cousin: invoices + tax for digital sellers, checkout included, flat SaaS. It is **VAT-world**, not MyInvois-world, and its checkout is a side feature.

**Relation.** **Respect.** Do not enter their US/EU invoice-and-VAT lane. Do not let them enter ours (they won’t, soon).

#### Anrok

Official 16 Aug 2026: **$100 per market per month** (SaaS Starter) or **$50** (ecom Starter); Custom for 10+ markets, e-invoicing, multi-entity, audit tooling. A “market” = a US state or an international filing regime (OSS = one market). High-volume Starter may have extra fees. Customers displayed: Notion, Anthropic, Cursor, Vanta, Mercury, Synthesia, etc. (vendor claims).

Anrok Custom listing **e-invoicing** is the long-term strategic tell: US SaaS tax vendors know CTC is coming. They will land in Europe and LATAM first. Malaysia is not their week-one map.

**Relation.** **Partner candidate** for destination-tax if we expand the export story. **Competitor** only if they hire a MY tax team and bolt on checkout — watch, do not obsess.

#### Vertex

ERP tax engine (SAP, Oracle). Formal tax departments. Quote-only.

**Relation.** **Ignore.**

---

### 6. Open-source / self-host

#### Job they own

Let a team **own the billing data plane**: run it in their VPC, avoid per-revenue bps, customize the state machine, pass security review. Buyers are platform companies, telcos, AI labs, and “we do not trust SaaS with our ledger” banks.

#### Category leader

**Lago** is the modern conversation leader (GitHub + AI billing Twitter). **Kill Bill** is the veteran (10+ years, Apache 2.0, F500 claims, Aviate managed cloud). **Solidus** is an OSS storefront (Spree fork) with payment extensions — adjacent, commerce not billing. **BTCPay Server** is the leader for **self-hosted crypto acceptance** (Bitcoin/Lightning), adjacent to our Phase 3 “borderless” note.

#### How Lazuar should relate

- **Ignore** as a sales competitor. Our ICP wants a **hosted** cash register, not a Helm chart.
- **Do not** self-host-as-a-product in v1.
- **Steal** Kill Bill’s plugin mindset (gateways as adapters) and Lago’s metric/plan language.
- **BTCPay**: possible **BYOK adapter** later for the crypto checkout README item. We are not a Bitcoin company.

#### Feature table vs Lazuar thesis

| Job | Lago / Kill Bill / Solidus / BTCPay | Lazuar thesis |
| --- | --- | --- |
| Hosting | You run it (or paid cloud) | **We run it.** Multi-tenant CaaS. |
| Price | OSS $0 + engineering; Kill Bill Aviate fixed (B); Lago Premium (B) | SaaS + credits |
| Customization | Infinite | Finite, on purpose |
| Compliance | You build it | **We sell it** |
| Crypto | BTCPay | Optional later rail |
| Storefront | Solidus | **Forbidden** (CMS trap) |

#### Kill Bill

Apache 2.0 subscription billing + payments. Official pitch: no % fees, predictable cost, Docker/AWS self-host free, **Aviate** managed at fixed price. Plugin architecture for gateways and invoice formatters. This is what a telco uses when Chargebee is both too expensive and too closed.

**Relation.** Inspiration for adapter boundaries. Not a logo we will see in a KL creator deal.

#### Solidus + payments

Headless-ish OSS e-commerce. Payments via extensions (Stripe, etc.). Wrong layer: they own catalog and cart. We own cash register and compliance.

**Relation.** **Ignore.** If someone wants Solidus, they want Shopify without SaaS, not Lazuar.

#### BTCPay

Self-hosted Bitcoin payments, no middleman, Greenfield API, point-of-sale apps. Chargeback-free, FX-volatile, buyer-unfamiliar in MY except niche.

**Relation.** **Adjacent integrate-later.** Never default. Never imply BTCPay solves SST.

---

### 7. Embedded finance / BaaS

#### Job they own

Let a **platform** onboard sub-merchants, split funds, file 1099s, underwrite risk, and sometimes issue cards or hold balances. The buyer is a marketplace, a vertical SaaS (the “Shopify of X”), or a PayFac wannabe.

Stripe Connect is the default. **Finix** is the US PayFac-as-a-service alternative (dedicated MIDs, IC+, platform-controlled pricing). Adyen for Platforms and Checkout.com platforms play the same game upmarket.

#### Category leader

**Stripe Connect** (expectation-setter, documentation, global-enough). Finix leads the “we want to be the PayFac and see interchange” conversation in the US.

#### How Lazuar should relate

- **Ignore as a product line.** Becoming a platform / marketplace / PayFac is on the do-not-become list.
- **Do not** take a Connect-shaped cut ($2 / active sub-account + 0.25% + 25¢, or a platform take-rate). That is how we accidentally become a marketplace.
- If a future tenant *is* a platform (e.g. a course marketplace), the correct answer is: **each seller BYOK their own MID**, or they use Stripe Connect themselves and we only see the platform’s key. We do not hold seller funds.

#### Feature table vs Lazuar thesis

| Job | Connect / Finix | Lazuar thesis |
| --- | --- | --- |
| Sub-merchant KYC | Stripe/Finix | **Out of scope.** Merchant already has a MID. |
| Split payouts | Native | **Do not hold or split money.** |
| Platform monetization | Take-rate or IC+ markup | **Software + credits** |
| Who is liable | Platform + provider | Merchant + their acquirer |
| Finix price (Finix Jun 2026 comparison) | From **$99/mo** or IC+ custom | N/A |

**Finix** is US/Canada-centric. No MY FPX, no MyInvois, no reason to appear in a Lazuar demo.

**Stripe Connect** will show up in conversations when someone says “can we build a marketplace of creators on Lazuar?” The answer is **no, we will not**. They can build a marketplace on Stripe Connect and use Lazuar only if each tenant is a real merchant. That sentence must be a slogan.

---

### 8. Developer-first billing

#### Job they own

Make **pricing a config file**: entitlements, credits, usage meters, feature gates, no webhook hell, copy-paste SDKs, “change the plan without a migration.” Buyers are AI startups and indie SaaS who hate Stripe Billing’s objects and hate Chargebee’s sales call.

Named set: **Polar** (MoR + billing), **Autumn** (open-source layer *on* Stripe), Lago/Orb (already covered), and a swarm of schemaless / usage tools (Flexprice, Stigg, Hyperline, Solvimon, Amberflo, Togai-class, “schemless” as the brief’s catch-all).

#### Category leader

No single leader. **Polar** leads the “just take my money and also be MoR” developer conversation in 2026. **Autumn** (YC W2026) leads the “three functions, no webhooks, sit on Stripe” conversation. **Metronome-inside-Stripe** will lead the AI-scale conversation by default once the integration is real.

#### How Lazuar should relate

- **Compete** on integrator DX: one machine key, one checkout session, one signed outbound webhook catalog, TypeSpec-honest APIs, test clocks, request logs. That is Autumn’s *feeling* without Autumn’s Stripe-only constraint.
- **Adopt Autumn’s architectural idea**: we are a **control plane above rails**, not a replacement for Stripe objects when the rail is Stripe.
- **Do not** become an AI token-metering company. Credits in *our* product are **tenant prepaid credits for LHDN and WhatsApp**, not end-customer GPU seconds.
- **Ignore** Flexprice/Stigg/Hyperline as sales competitors in MY.

#### Feature table vs Lazuar thesis

| Job | Polar / Autumn / usage swarm | Lazuar thesis |
| --- | --- | --- |
| Sit on Stripe | Autumn: explicit. Polar: Polar is MoR on Stripe Connect. | **Sit on many rails**, Stripe included. |
| Entitlements / feature gates | Autumn `check` / `track` / `attach` | Need a **thin** access signal (webhook + portal). Not a full entitlement OS in v1. |
| Usage / credits for *their* customers | Polar meters; Autumn credits; Lago/Orb | Later. Ledger must not forbid it. |
| MoR | Polar yes; Autumn no | **No** |
| Open source | Autumn Apache-2.0; Polar parts; Lago | Closed CaaS. Fine. |
| Asia compliance | No | **Yes** |

#### Polar (again, as a billing DX)

Usage-based billing docs, seats, credits, trials, discounts, finance views. The reason developers mention Polar in the same breath as Stripe is **time-to-first-dollar + OSS vibe + public price**. We will lose Twitter bake-offs that start with “I have no company.” We should win bake-offs that start with “I have an Sdn Bhd and Billplz.”

#### Autumn

Official: open-source pricing & billing layer **between the app and Stripe**. Manages subscription state, credit balances, entitlements, usage enforcement. Pricing changes become config. “No webhooks” as a developer promise. Self-host free; managed paid (classic OSS). YC W2026. Explicitly **does not replace Stripe Billing**.

**Relation.** **Spiritual sibling, different rail set.** If Autumn ever adds “adapter: anything,” they become more like us minus LHDN. Until then they help US AI startups; we help Asian legal entities.

#### Schemaless / usage tools

The swarm (Flexprice, Stigg, Hyperline, Solvimon, Amberflo, etc.) sells **metering and price books** for AI. Pricing is usually custom or a small % of revenue after a free band (Solvimon’s own site: first $3M free then 0.4% — vendor claim).

**Relation.** **Ignore.** If we need metering we will add events to *our* ledger, not resell Amberflo.

---

### Extra category A — Payment orchestration

Spreedly, Primer, Basis Theory, and Stripe Vault-and-Forward exist because enterprises refuse single-homing. They tokenize once and route to many acquirers.

**Relation.** **Ignore as a competitor.** Lazuar is a **vertical orchestrator** (Asian APMs + compliance + dunning), not a horizontal token vault. If we ever need network tokens across acquirers, we buy or partner; we do not build a PCI vault product.

### Extra category B — New-wave MoR (Creem, Dodo, InflowPay)

A 2025–2026 flock undercutting Paddle on published bps and onboarding speed. Same legal shape as Polar, weaker brands. They will keep MoR pricing honest.

**Relation.** **Ignore individually. Track the price floor.** If MoR starter rates go to 3% all-in, Paddle’s 5%+50¢ looks worse and more US founders stay on Stripe+Anrok — which is fine. None of them become LHDN.

### Extra category C — CTC / e-invoice bureaus

ClearTax (MDEC-accredited in MY), Storecove, Sovos, local MyInvois middleware, accounting vendors (AutoCount, SQL, Xero’s eventual connector). They own **submission and 55-field mapping**. They do not own the Buy button.

**Relation.** **Compete at POS** (we emit from the ledger that already knows the sale). **Partner** with accountants. **Do not** become a bureau that uploads CSVs for companies that check out on Shopify.

ClearTax’s own 17 July 2026 explainer is the best **non-IRBM** public summary of the mandate we must implement; treat it as secondary to IRBM PDFs when they disagree.

---

## Global vs local wedge

This is the heart of the paper. Global products fail in Malaysia in specific, documentable ways. Lazuar only deserves to exist if it owns those failures.

### 1. FPX is the default, and Stripe prices it like a card

PayNet FPX is how Malaysian buyers pay for anything more expensive than a coffee if they do not have (or do not want to use) a card. DuitNow QR / DuitNow Pay is how they pay in person and, increasingly, online. Touch ’n Go, GrabPay, and Boost are the wallet layer.

What global OS vendors charge for that default, on 16 August 2026:

| Rail | Stripe MY | Adyen public table | Local PSP pattern (not this paper’s census) |
| --- | --- | --- | --- |
| FPX / online banking | **3% + RM1.00** | **$0.13 + $0.52** | Sen-to-low-RM, often flat |
| DuitNow | not the MY headline SKU | **$0.13 + 1.5%** | Flat or low-% |
| GrabPay | **3%** | **$0.13 + 1.5%** (MY) | Similar to wallet deals |
| Cards | **3% + RM1.00** (+1% intl, +2% FX) | IC++ + $0.13 | 2.5–3.5% typical |

A RM197 course on Stripe Payment Links costs **RM6.91** in Stripe FPX fees (3%+RM1). On a local collection it might cost **under RM2**. That delta *is* the sales pitch. Paddle’s 5%+50¢ on the same sale is **~RM10.35** plus the fact that Paddle is now the seller.

**Wedge.** Hosted checkout that **defaults to the merchant’s cheap local rail**, with cards as a second button for foreigners. Never let Stripe FPX be the only FPX.

### 2. SST is not VAT, and Stripe Tax will not be our domestic engine

Official Stripe Tax country matrix: Malaysia is **digital products + service tax**, and a Malaysian **business location is not supported**. Stripe’s own SST essay (16 Mar 2026 page, still live) correctly describes the split:

- Sales tax on goods at **5% or 10%**, usually at manufacture/import — retailers often do not re-charge it.
- Service tax at **6%** (F&B, telecoms, parking, logistics) or **8%** (most professional, hospitality, **digital services**).
- Registration commonly **RM500,000** taxable turnover (with RM1.0–1.5m variants by sector).
- Bimonthly MySST returns.

A KL SaaS or course seller cares about: (a) whether their service is in the **service-tax list**, (b) whether they have crossed the threshold, (c) how to show SST on a document, (d) how **export** of digital services is treated, and (e) the fact that **LHDN e-invoice is a different law**.

Global tax products will happily compute **Irish VAT on a sale to Dublin**. They will not maintain the Customs service-tax schedule, the export analysis, and the LHDN document in one ledger line.

**Wedge.** Ledger lines that already know `tax_regime = SST | export_zero | out_of_scope`, and a compliance job that does not confuse SST with MyInvois.

### 3. LHDN UBL 2.1 / MyInvois is a clearance system, not an invoice PDF

ClearTax’s 17 July 2026 summary (cross-check against IRBM PDFs before implementation):

- Mandatory e-invoice via **MyInvois**, UBL 2.1 **XML or JSON**, **55 fields**, IRBM digital certificate, real-time validation, UUID, QR.
- Channels: MyInvois Portal (manual / bulk) or **API**.
- Document types: invoice, credit note, debit note, refund note, plus self-billed variants.
- **Phase 4 from 1 January 2026**: taxpayers with turnover **RM1 million to RM5 million**. Relaxation extended (ClearTax: through 31 Dec 2027, full enforcement 1 Jan 2028 — **verify against latest IRBM circular before promising dates**).
- Exemption threshold raised to **RM1 million** (7 Dec 2025 announcement, Phase 5 cancelled). Below RM1m: exempt. Once in, you stay in even if revenue falls.
- From **1 January 2026**, **no consolidation for any single transaction exceeding RM10,000**. Individual e-invoice required.
- B2C: if the buyer does not need an e-invoice, monthly **consolidated** e-invoice is still allowed **below** that RM10k-per-txn rule; submit within **7 calendar days** after month-end (IRBM specific guideline pattern).
- 72-hour reject/cancel window.
- Non-compliance: Income Tax Act s.120(1)(d) — fines **RM200–RM20,000** and/or up to **6 months** per offence (as commonly cited; counsel must confirm).

IRBM SDK documents the invoice as UBL 2.1. Lazuar already keeps XML samples and an LHDN SDK in-repo. That is not a feature checkbox; it is the **category we are founding**.

What global products do instead:

- Stripe: e-invoices **its fees** to the MY account holder.
- Paddle/Polar/Gumroad: issue **their** tax invoice, because they are the seller.
- Chargebee/Recurly: PDF invoices + Avalara tax lines.
- Anrok Custom: “e-invoicing” as an enterprise bullet, not a MyInvois connector.
- Quaderno: beautiful VAT invoices.

**Wedge — ADR 021’s three pillars, restated as competitive facts:**

1. **Low-ticket B2C:** thousands of FPX receipts → ledger → **one consolidated MyInvois document** a month (except any txn > RM10k, which must be individual). No founder can do this in a spreadsheet once Phase 4 bites.
2. **High-ticket B2B:** TIN validation **before** pay, immediate validated e-invoice + QR so the buyer can claim the expense. Paddle’s invoice is the wrong legal object.
3. **Export:** classify as export / zero-rated correctly so the founder stays competitive abroad without corrupting the local file.

ADR 023’s UI lobotomy (hide LHDN in MVP) is a **go-to-market sequence**, not a strategy change. The backend must stay real or the wedge is vapour.

### 4. WhatsApp-first buyers, email-first vendors

Malaysian and SEA buyers:

- receive the payment link on WhatsApp,
- pay with FPX inside a webview,
- expect the receipt on WhatsApp,
- ignore email dunning until the card is dead.

Global dunning (Stripe Billing, Chargebee Smart Dunning, Paddle recovery, Recurly) is **email + retry + in-app**. SMS exists in the US at painful rates. WhatsApp Business Platform is a **template-rated, per-message, per-market** API (Meta switched from conversation billing to per-template-message on **1 July 2025**). Utility templates for “your payment failed, tap to update” are the correct category; marketing templates are the wrong (and more expensive) category.

Lazuar’s prepaid credit wallet is the **only honest way** to sell this: WhatsApp is not free, we must not hide it inside GMV bps, and we must not ship a fake “native WhatsApp” that only `console.log`s (current honesty gap in `docs/001-gaps`).

**Wedge.** Recovery journeys that start where the sale started: chat. Email is the audit copy.

### 5. Headless is how this region actually sells

Serious SEA sellers already have:

- a Framer or custom Next.js page,
- an Instagram bio,
- a WhatsApp Business catalog,
- sometimes a Shopify or EasyStore for physical SKUs.

They do **not** want to move the brand onto Gumroad’s theme or Kajabi’s course skin. Global creator tools fight them. Lazuar agrees with them (ADR 015). The cash register is supposed to be boring.

### 6. Corporate procurement is TIN-shaped, not VAT-ID-shaped

A RM12,000 workshop sold to a Malaysian Sdn Bhd dies at checkout if:

- the invoice is from “PADDLE.NET INC” or “LEMON SQUEEZY LLC,”
- there is no buyer TIN,
- there is no IRBM QR,
- SST is missing or wrong,
- payment was a foreign-currency card with FX.

Global B2B billing (Chargebee quotes, Stripe Invoicing, Paddle invoicing SKU) assumes **VAT ID + PDF + wire**. MY B2B assumes **TIN + BRN + SST ID + MyInvois UUID + FPX or IBG**.

**Wedge.** Pillar 2 is not “enterprise features.” It is the minimum document a finance clerk will accept.

### 7. What “local” already does that we must not ignore

Billplz, CHIP, Fiuu, Xendit, HitPay, and the accounting stack (Xero, AutoCount, SQL) already exist. They are **dumb-to-medium pipes** plus, in some cases, a PDF. The gap ADR 019 named is still the gap:

> Stripe is terrible for local SEA bank transfers. Billplz/CHIP are great for local payments but lack robust developer webhooks and subscription dunning. No standard website builder does LHDN. Native payment links leave buyers on a generic thank-you page.

**Wedge.** We are the **brain** those pipes do not have. We are not a sixth local PSP.

---

## Feature gravity (what customers expect because Stripe exists)

Even when the buyer is a Malaysian founder who will never put 100% of volume on Stripe, **Stripe trained their checklist**. If we miss these, we look unserious. If we copy the rest of Stripe, we die.

### The checklist we must honour (jobs, not pixels)

1. **A hosted checkout that feels inevitable.** One column, trust marks, order summary, mobile-first, Apple Pay / Google Pay when the rail is a card, FPX bank list when the rail is FPX. Stripe Checkout is the aesthetic reference. Adyen Drop-in is the enterprise reference. We should look closer to Stripe than to a 2014 Billplz page.

2. **Payment Links as a first-class object.** Create in UI or API, set amount or product, expiry, one-time or subscription, UTM, after-completion redirect. Paste into WhatsApp. This is how 80% of our ICP will start.

3. **Test mode that is a universe, not a flag.** Separate keys, separate webhooks, clock simulation, test FPX if the rail offers it. Stripe’s test clocks spoiled billing engineers.

4. **Customers, products, prices, subscriptions, invoices** as named nouns. We can have better domain names internally; externally, an integrator should not need a glossary to map us onto the mental model they already have.

5. **Signed, retryable, documented webhooks** with a public event catalog, delivery logs, and “send again.” Stripe Dashboard’s webhook tab is the bar. Our gap doc already admits outbound is the weak twin.

6. **A customer portal.** Update payment method, see invoices, cancel (with our rules), download the tax document. Stripe Billing Customer Portal and Chargebee’s portal taught this.

7. **Dunning that is a policy, not a hope.** Schedules, channels, retry vs ask-for-new-method, end state (pause / cancel / mark uncollectible). Stripe Smart Retries and Chargebee Smart Dunning set the *expectation* of intelligence. We can ship **deterministic** journeys first; we cannot ship “we email sometimes.”

8. **Idempotent APIs and safe keys.** Secret keys, restricted keys, rotate, last-used. Machine credentials that are not a stolen user JWT (`docs/001-gaps` theme 2).

9. **A dashboard that reconciles to the bank.** Gross, fees, tax, net, refunds, disputes, by rail. Double-entry is how we *beat* Stripe here if we show “net cash in bank” across Billplz + Stripe in one number.

10. **Docs a founder can paste into Cursor.** Stripe’s docs are a competitive moat. Polar and Autumn win developer love with the same weapon. Our TypeSpec + developers hub has to read like a product, not like a modular monolith tour.

### The checklist we must refuse (Stripe-shaped traps)

| Gravity | Why it exists | Why we refuse |
| --- | --- | --- |
| Connect / marketplaces | Stripe makes platforms pay rent | We are not a PayFac |
| Atlas / “incorporate in Delaware” | Stripe’s US startup funnel | Wrong geography, wrong company |
| Terminal / hardware | Unified commerce story | Not our buyer |
| Radar as a product | Stripe has the network | We piggyback the rail’s fraud tools |
| Billing as 70 bps | Easy attach | Betrays BYOK |
| Tax as 50 bps | Easy attach | Betrays utility-wallet economics; still doesn’t do MyInvois |
| Invoicing as 40 bps | Easy attach | Invoices are part of compliance, not an upsell |
| Sigma / data pipeline paid SKUs | Monetize data | Nice later; not the wedge |
| Link / one-click network | Stripe’s consumer graph | We will not have it; FPX + saved-bank is our equivalent |
| 15 products in one sidebar | OS packaging | ADR 023 lobotomy is correct |

### Chargebee / Paddle gravity (secondary teachers)

From **Chargebee**, buyers expect: a price book, coupons, trials, proration, pause, scheduled changes, quote PDFs, a “growth” cancel-save page. We should implement the **nouns** that show up in every mid-market SaaS, not the Salesforce CPQ suite.

From **Paddle**, buyers expect: “I will not get a tax letter.” We cannot offer that globally. We must offer the regional equivalent: **“I will not get an LHDN letter for missing e-invoices, and I will not pay 5% for the privilege.”** That is a different sentence and we should stop borrowing Paddle’s.

From **Gumroad**, buyers expect: first sale today. Our onboarding has to get a Payment Link live **before** we demand a 55-field tax profile. ADR 023 already chose this sequence. Keep it.

### Feature gravity vs honest now

| Gravity item | Honest Lazuar now (product watermark + gaps) | What “bar met” means |
| --- | --- | --- |
| BYOK rails | Real (Billplz / Stripe / …) | More Asian rails, tested refunds/disputes |
| Hosted checkout | Portal exists | CRO to Stripe Checkout level |
| Subscriptions | Commerce subscriptions exist | Portal + proration + pause |
| Ledger | Double-entry skeleton | Fee + tax + net cash report the CFO trusts |
| Email dunning | Templates / config ahead of engine | Closed loop: fail → run → retry → recover |
| WhatsApp dunning | Roadmap / not really sending | Meta Cloud API + credits + template approval |
| LHDN | Backend pipeline, UI hidden | Phase D UI + consolidation job + TIN checkout |
| Outbound webhooks | Weak twin | Versioned catalog + delivery log |
| Machine keys | Incomplete / too powerful | Scoped integrator credentials |

Feature gravity is a **sequence**, not a reason to lie on the website.

---

## Who we actually lose deals to

Not “who is famous.” Who a specific founder will pick instead of us, and why that is sometimes correct.

### Loss table (structured, not counted)

| If the founder is… | They will pick | Why we lose | Is that acceptable? |
| --- | --- | --- | --- |
| US/EU indie, no entity, $19–$49 SaaS, VAT-scared | **Paddle** or **Polar** | MoR *is* the product they want | **Yes. Send them away.** |
| US/EU indie, has Stripe, wants config-not-code | **Autumn** or **Stripe Billing** | We add no US tax value | **Yes.** |
| Hobby creator, one PDF, no company | **Gumroad** | Time-to-first-dollar + Discover | **Yes (ADR 021).** |
| Creator who wants a pretty store and email | **Payhip** / **Stan** / **Kajabi** | We refused the CMS | **Yes.** |
| Marketer whose religion is order bumps | **ThriveCart** / **SamCart** | We refused the funnel builder | **Yes.** |
| Series B SaaS, Salesforce, RevRec, 4 entities | **Chargebee** + Stripe/Adyen, or **Zuora** | We are not a Q2C suite | **Yes.** |
| AI lab metering billions of events | **Metronome (Stripe)** / **Lago** / **Orb** | Wrong company | **Yes.** |
| Marketplace CEO | **Stripe Connect** / **Finix** | We will not hold funds | **Yes. Do not bend.** |
| Enterprise unified commerce | **Adyen** / **Checkout.com** | Not our meeting | **Yes.** |
| “Just give me a Billplz collection + WhatsApp” | **Billplz / HitPay + spreadsheet** | We look like work; they do not feel LHDN yet | **No — this is the deal we must learn to win.** |
| MY Sdn Bhd, courses + retainers, has TIN, hates 5% | Should pick **us** | If we lose, we lost on UX, trust, or honesty (hidden LHDN, fake WhatsApp) | **No.** |
| MY Sdn Bhd, already on Stripe Billing, only cards, no FPX need | **Stripe Billing** | 70 bps + 3% still simpler than a new vendor if volume is card-heavy and LHDN is still “the accountant’s job” | **Sometimes. Win on MyInvois + FPX + WhatsApp, not on “nicer Billing.”** |
| MY Sdn Bhd, accountant runs AutoCount / SQL, checkout is FPX link | **Accountant + Billplz** | Compliance lives in the accounting suite, not POS | **Fight with POS-native e-invoice, not with GL features.** |
| Platform / vertical SaaS embedding checkout | **Stripe Connect** or in-house + Xendit | They need split payouts | **Only win if each sub is BYOK.** |
| Crypto-native | **BTCPay** / Polar crypto | We are fiat-first | **Yes for now.** |

### Where we would lose to Stripe Billing specifically

Write this on the wall. Stripe Billing beats us when **all** of the following are true:

1. The merchant is already on Stripe and does not want a second vendor.
2. Volume is **cards**, not FPX (or they have accepted 3%+RM1).
3. Tax is either “US/EU and Stripe Tax is enough” or “our accountant handles LHDN offline.”
4. Dunning by email is culturally fine (US/EU buyers).
5. They want Link, Smart Retries, Customer Portal, and the Billing API **today**, not on our roadmap.
6. Usage is about to go Metronome-shaped, and they would rather stay inside Stripe.

We also lose the **integration tax** argument: every third-party already has `stripe` as an npm package. We must make the “second vendor” feel like **one afternoon**, not a platform migration.

We do **not** lose Stripe Billing on: FPX economics, MyInvois, SST-as-a-first-class line, WhatsApp recovery, multi-rail ledger, or 0% of GMV. If a deal came down to those and we still lost, we failed the demo.

### Where we would lose to Paddle specifically

Paddle beats us when:

1. The founder’s primary fear is **a tax authority they do not live in** (HMRC, a US state, EU OSS).
2. They want **Paddle to answer buyer emails** about receipts and cancellations.
3. They have **no appetite to own a MID** or to pass KYC with a local PSP.
4. Their SKU is globally priced in USD and their entity is already optimized for MoR (often US LLC / UK Ltd).
5. They have read Tailwind’s quote and decided sleep is worth 5%.

Paddle loses — and we should be present — when:

1. The founder **is** the tax authority’s subject (LHDN).
2. Buyers are **companies that need the founder’s invoice**.
3. 5% of GMV is a real number (RM50k–RM500k/yr digital).
4. Local rails matter.
5. Restricted-product AUP risk (info products, some coaching, some health) makes MoR account death a threat.

### Where we would lose to Chargebee specifically

Chargebee beats us when:

1. There is a **Director of Revenue Operations**.
2. The catalog has hundreds of prices, ramps, evergreen quotes, and Salesforce.
3. They need **RevRec** or multi-entity.
4. They already pay the 80 bps happily because finance wants a named system of record.
5. They need 40 gateways for a global card patchwork — and none of those gateways is “Billplz.”

Chargebee loses when the buyer is the founder-CTO, the catalog is four plans, the finance system is Xero, and the existential risk is **MyInvois Phase 4**, not ASC 606.

### The silent incumbent (we lose more deals here than to Stripe)

**Billplz or HitPay payment link + WhatsApp chat + accountant at year-end.**

This is the MY equivalent of “the informal salon stack” in Aura’s local paper. It is already paid for. We only displace it when:

- subscriptions require retries the human forgets,
- Phase 4 / RM10k rules make month-end consolidation real work,
- the founder wants a customer portal instead of “bro I send again,”
- or a SaaS product needs a webhook to unlock accounts.

Until one of those four is felt, we are a vitamin. ADR 021 said we only sell painkillers. Respect that and **do not discount into the informal stack**.

---

## Features to track

Not a build list. A **watchlist**. Each item is either gravity we must eventually honour, a competitor move that changes the wedge, or a trap.

### Track as competitive intelligence (re-verify every quarter)

| ID | What | Why it can change the map |
| --- | --- | --- |
| CI-01 | **Stripe Managed Payments / Lemon Squeezy end-state** | If Stripe one-clicks MoR, Paddle’s reason-to-exist compresses. Our US/EU ignore-list gets bigger. |
| CI-02 | **Stripe × Metronome integration timeline** | When Billing *is* Metronome, “Stripe can’t do usage” dies. We must never have bet the company on usage. |
| CI-03 | **Stripe Tax × MY business location** | If Stripe flips MY business location to ✓ and adds MyInvois, the wedge narrows to WhatsApp + multi-rail + price. Watch the country table. |
| CI-04 | **Stripe FPX price** | If Stripe ever prices FPX at Adyen-like flat, our “3% is a joke” slide dies. Unlikely soon (they like one blended number). |
| CI-05 | **Chargebee Flow adoption vs old Starter** | Confirms billing-bps as the mid-market standard we refuse. |
| CI-06 | **Polar rate card + country coverage** | Public MoR floor. If they add MY entity support, they still are MoR — but they will show up in KL Twitter. |
| CI-07 | **Anrok e-invoicing SKU geography** | First CTC country they clear is a leading indicator. |
| CI-08 | **Paddle invoicing + B2B SKU** | If Paddle learns to emit *seller-of-record-looking* invoices for enterprises, high-ticket gets harder. Still legally Paddle. |
| CI-09 | **Meta WhatsApp utility rates for MY** | Unit cost of our recovery wallet. |
| CI-10 | **IRBM guideline revisions** (consolidation, Phase 4 relaxation, 55 fields, JSON vs XML) | Our actual backlog. |
| CI-11 | **Xero / QuickBooks MyInvois** | If Xero files e-invoices natively from bank feeds, accountants will say “just Xero.” We must remain **at POS**, not in GL. |
| CI-12 | **HitPay / Billplz subscription + e-invoice features** | Local pipes growing brains. Closest *product* risk, even if they are also BYOK targets. |
| CI-13 | **Creem / Dodo / new MoR pricing** | Floor for “I just want a link.” |
| CI-14 | **Gumroad MoR quality** (VAT letters, payout holds) | If Gumroad MoR is messy, serious creators churn sooner. |

### Track as feature gravity (honour the job, not the SKU)

| ID | Job | Bar-setter | Lazuar stance |
| --- | --- | --- | --- |
| FG-01 | Payment Links | Stripe | **Own** |
| FG-02 | Hosted Checkout | Stripe Checkout | **Own** |
| FG-03 | Customer Portal | Stripe / Chargebee | **Own** |
| FG-04 | Webhook delivery log | Stripe | **Own** |
| FG-05 | Test clocks | Stripe Billing | **Own** |
| FG-06 | Smart retries | Stripe / Chargebee | Deterministic first; ML never as identity |
| FG-07 | Order bump / one-click upsell | ThriveCart / SamCart | Thin, later |
| FG-08 | Hosted invoice + public pay page | Stripe Invoicing / Quaderno | Own as **tax document**, not 40 bps SKU |
| FG-09 | Tax ID collection at checkout | Stripe Tax / Chargebee | Own **TIN / BRN / SST** |
| FG-10 | Usage meters | Lago / Metronome / Polar | Ledger-ready, not v1 pitch |
| FG-11 | Entitlements | Autumn / Chargebee | Webhook + thin gate |
| FG-12 | Revenue recognition | Maxio / Zuora / Stripe RevRec | **Ignore** |
| FG-13 | CPQ | Chargebee / Zuora | Thin quote only |
| FG-14 | Affiliates | SamCart / LS | Phase 3 |
| FG-15 | Apple Pay / Google Pay / Link | Stripe | When rail is card |
| FG-16 | 3DS / network tokens | Stripe / Adyen | Rail’s job |
| FG-17 | Multi-acquirer routing | Primer / Spreedly | Ignore |
| FG-18 | Connect split pay | Stripe Connect | **Refuse** |
| FG-19 | Storefront themes | Gumroad / Payhip | **Refuse** |
| FG-20 | Course player / community DRM | Kajabi / Whop | **Refuse** (webhook out) |
| FG-21 | Hardware POS | Square / Stripe Terminal | **Refuse** |
| FG-22 | MyInvois submit + QR | ClearTax / IRBM SDK / us | **Own** |
| FG-23 | B2C consolidation job | IRBM rules / us | **Own** |
| FG-24 | WhatsApp utility dunning | Nobody global | **Own** |
| FG-25 | Multi-rail double-entry | Nobody global | **Own** |
| FG-26 | Prepaid credits for compliance/comms | Us (designed) | **Own** — do not bury in GMV |
| FG-27 | Xero/QBO sync | Chargebee / Stripe / us | **Partner-complete** the CFO loop |
| FG-28 | Escrow + e-sign for high ticket | Escrow.com / PandaDoc (README Phase 2) | Optional later; do not become DocuSign |

### Track as honesty bugs (if marketing claims them, product must ship or shut up)

| Claim | Source of temptation | Current watermark |
| --- | --- | --- |
| “Native WhatsApp dunning” | README Phase 1, ADR 019 | Not guaranteed; gaps say it may only log |
| “LHDN automated” | ADR 021 | Backend yes; UI lobotomized (ADR 023) |
| “We replace Chargebee” | Sales ego | We replace a *slice* |
| “Global tax handled” | Paddle envy | We handle **MY (and later ID/IN)**. Not OSS. |
| “FPX included” | Local pride | Only if a **BYOK local rail** is configured — not Stripe FPX at 3% |

---

## Implications

### 1. Category strategy, one more time

Lazuar is **not** a global payment OS, **not** a Merchant of Record, **not** a Chargebee, **not** a Gumroad, **not** an Avalara, **not** a Finix, **not** an Autumn-for-AI.

Lazuar is the **compliance and ledger control plane above whatever rails an Asian professional merchant already has**, with a headless cash register and a recovery channel that lives in WhatsApp.

If a feature does not (a) complete a payment, (b) keep the merchant legally alive, (c) recover failed money, or (d) tell a machine that access should change — it is off the island (ADR 021).

### 2. Integrate the OS; do not re-acquire the world

Ship and maintain excellent BYOK adapters for **Billplz, CHIP, Fiuu, Xendit, Razorpay, Stripe**. Treat Adyen, Checkout.com, Braintree, Square as non-goals. Speak Stripe-shaped JSON. Price like software, not like an acquirer.

### 3. Compete with MoR only where MoR is the wrong legal shape

The Malaysian (and Indonesian, Indian) entity that must be the seller of record is our ICP. The Delaware LLC that wants to never learn what OSS means is Paddle’s. Draw the line in onboarding, not in a comment thread.

### 4. Build a *small* billing engine, not a Q2C suite

Honour Stripe/Chargebee nouns (links, portal, dunning, invoices, webhooks). Refuse RevRec, multi-entity, Salesforce CPQ, 40-gateway pageantry, and 80 bps.

### 5. Creator checkout is a GTM motion, not a storefront product

Win professional creators with links + FPX + invoices + portal. Lose hobbyists and funnel-marketers on purpose.

### 6. Tax: own CTC-at-POS; partner destination VAT if ever

MyInvois UBL 2.1, consolidation, TIN checkout, SST classification, export zero-rating. That is the moat. Anrok/Quaderno are allies for the US/EU problem we should not rebuild.

### 7. Ignore OSS, BaaS, and AI-metering as companies; steal their DX

No Helm chart product. No PayFac. No GPU-second price book. Yes to adapter discipline, public event catalogs, and “pricing as config” later.

### 8. Sequence honesty ahead of wedge theatre

ADR 023 hid LHDN to ship a cash register. That is allowed **only if** the website does not claim the hidden thing, and only if Phase D is real. Fake WhatsApp is more dangerous than no WhatsApp. The informal stack (Billplz + chat) beats a dishonest CaaS.

### 9. Do-not-become list (non-negotiable)

Do not become:

1. **A marketplace.** No Discover, no 30% tax, no “we bring you buyers,” no Connect take-rate. (Gumroad Discover, Fresha Boost, Stripe Connect platforms.)
2. **A Merchant of Record.** No title to the sale, no 5% “for tax,” no holding funds, no buyer-support call centre as the product. (Paddle, Polar, LS, FastSpring, Gumroad-as-MoR.)
3. **A website builder / CMS / course LMS / community app.** No themes, no drag-and-drop, no Telegram bouncer as identity. (Gumroad store, Payhip, Kajabi, Whop, the killed 15-app suite.)
4. **A full ERP / accounting suite / RevRec engine.** We sync to Xero. We do not replace AutoCount. (Zuora, Maxio, SAP+Vertex.)
5. **A PayFac / BaaS / card issuer.** (Finix, Connect, Issuing, Treasury.)
6. **A sixth local PSP.** We do not apply for a PayNet membership to “save the customer a Billplz account.” That is acquiring. That is not us.
7. **A US sales-tax company.** 12,000 jurisdictions is Anrok’s furnace.
8. **An AI metering company.** Metronome/Lago/Orb already exist; Stripe just paid ~a billion dollars (class D) to prove it.
9. **A hardware POS company.** Square/Terminal.
10. **A salon, clinic, or vertical OS.** That is Aura. Lazuar may be Aura’s cashier. It is not Aura.

If a competitor comparison slide requires us to be one of those ten, **throw away the slide**, not the constraint.

### 10. What to tell a founder in one paragraph

> Keep your Billplz, CHIP, or Stripe account. Keep your Framer site. Paste our Buy link. We will take the payment on **your** MID, write a double-entry line that already knows SST and export, push the right MyInvois document (individual or consolidated), open a portal for your buyer, and chase failed subscriptions on WhatsApp. We charge software and credits, not a fifth of your margin. If you want someone else to *be* the seller, use Paddle. If you want Salesforce CPQ, use Chargebee. If you want a storefront theme, use Payhip. If you want to stay legal at the cash register in Malaysia without becoming a payments company, use us.

### 11. What to tell ourselves in one paragraph

Stripe is the teacher and a rail. Paddle is the honest alternative legal product we will not imitate. Chargebee is the billing-OS ceiling we will not hit. Gumroad is the hobby floor we will not serve. Avalara/Anrok are tax engines we will not rebuild for the West. Finix is a platform company we will not become. Lago is a pattern library. ClearTax is a bureau we must not shrink into. The only category we are allowed to found is **Compliance CaaS above Asian rails**. Everything in this paper that is not that is either an integration, a lesson, or a trap.

---

*End of document. Research date 2026-08-16. Re-verify any number before it enters a contract, a landing page, or a fundraising deck.*
