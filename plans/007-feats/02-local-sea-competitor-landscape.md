# 02 — Local Malaysia / SEA competitor landscape

**Product under analysis:** Lazuar Pay (Checkout-as-a-Service + Compliance CaaS).  
**Research date:** 16 August 2026.  
**Job-to-be-done this file maps:** “Accept money from customers in MY/SEA, recover failed subscriptions, stay legally compliant (LHDN/SST), and plug checkout into my own site/SaaS.”

This note is written for Lazuar Pay, not for salon booking software. It is a product-research dossier, not a sales deck. Pricing numbers that appear here were taken from vendor pages, vendor help centres, or clearly dated official tables fetched on 16 August 2026 (or from pages whose last-crawled timestamp is mid-August 2026). Secondary blogs (Airwallex comparison posts, Xendit comparison posts, DHL Discover, EasyStore help, accounting.my, etc.) are used as *context* and labelled as such. Vendor installed-base claims (“60,000+ organisations”, “20,000+ businesses”) are vendor claims unless a public listing count exists.

**Lazuar Pay, as of the product-truth watermark in the lazuar-pay repo (read 16 August 2026):**

- BYOK. The merchant keeps their own gateway keys. Lazuar is software, not Merchant of Record.
- Hosted checkout / payment links.
- Subscriptions + dunning (email dunning is shipping; WhatsApp dunning is roadmap).
- Double-entry ledger.
- LHDN e-invoice backend exists; the UI is intentionally hidden (ADR 021 + ADR 023).
- Developer APIs + webhooks.
- Local rails via Billplz / CHIP (FPX, DuitNow, wallets). Cards via Stripe (and other BYOK card processors).
- Monetisation intent: SaaS fee + prepaid utility credits for LHDN XML submission and dunning actions. Not a take-rate on GMV.

The rest of this file asks, for every local and SEA name that can steal that job: who they are, how they price, what they ship, where they beat Lazuar, where they lose, what they are in the competitive graph (direct rival / upstream rail / substitute / partner), and which of their features Lazuar must track.

---

## Method and sources

### How this research was done

1. **Search the market the way a Malaysian founder would.** English and Malay queries: `payment gateway Malaysia`, `FPX gateway`, `recurring billing Malaysia`, `e-invoice MyInvois`, `payment link WhatsApp`, plus every named suspect in the brief.
2. **Open official pages first.** Pricing, product, docs, about, and country pages were fetched on 16 August 2026.
3. **Separate rails from products from substitutes.** PayNet’s FPX and DuitNow are infrastructure. Billplz is a product sitting on those rails. WhatsApp + a Billplz link + Excel + the MyInvois portal is a substitute stack, not a company.
4. **Separate take-rate from SaaS from hybrid.** Most local names monetise MDR. A few (HitPay, Xendit, Airwallex) add software on top of MDR. Almost nobody sells BYOK checkout + ledger + LHDN as a software layer. That gap is the entire Lazuar thesis.
5. **Treat comparison blogs as biased.** Airwallex’s Curlec / Fiuu / gateway-fee posts, Xendit’s “best gateway for subscriptions” post, DHL Discover’s ADAPTIS table, and EasyStore’s gateway comparison are useful maps of the *named* set. They are not independent market share and they sometimes quote stale rates.
6. **SEA is a second lens, not the ICP.** Indonesia (Midtrans, Xendit origin), Philippines (PayMongo), Singapore (HitPay origin, 2C2P, Airwallex), Thailand (2C2P origin) are covered so Lazuar does not confuse “famous in Jakarta” with “installed in PJ”.

### Pricing rule used in this file

- **Public** = printed on a vendor page, vendor help centre, or a clearly dated official pricing table fetched for this research.
- **Unknown** = sales-quoted, unpublished, or only appearing in a third-party blog without a matching official page.
- **Indicative / secondary** = a dated third-party comparison that names a number. Used only with attribution.

Transaction fees in Malaysia are almost always quoted exclusive of SST (8% as of CHIP’s own pricing footnote, fetched 16 August 2026). Do not compare headline MDR without adding SST and, for Shopify merchants, Shopify’s third-party gateway surcharge.

### Primary sources fetched or opened 16 August 2026

| Source | URL | What it was used for | Date stamp |
|---|---|---|---|
| Billplz homepage | https://main.billplz.com/ | Product, claims, Catalog | Fetched 16 Aug 2026 |
| Billplz pricing | https://main.billplz.com/pricing | Official rates Basic/Standard | Fetched 16 Aug 2026 |
| CHIP homepage | https://www.chip-in.asia/ | Collect / Control / Capital | Fetched 16 Aug 2026 |
| CHIP pricing | https://www.chip-in.asia/pricing | Official MDR, Send, Advance | Fetched 16 Aug 2026 |
| ToyyibPay homepage | https://www.toyyibpay.com/ | Positioning, features | Fetched 16 Aug 2026 |
| ToyyibPay pricing | https://www.toyyibpay.com/pricing-plans/ | Official FPX / card / DuitNow QR | Fetched 16 Aug 2026 |
| senangPay homepage | https://senangpay.com/ | DOKU merger, ICP | Fetched 16 Aug 2026 |
| senangPay pricing | https://senangpay.com/pricing/ | Official annual + MDR | Fetched 16 Aug 2026 |
| Fiuu homepage | https://fiuu.com/ | Features, channels, TPV claim | Fetched 16 Aug 2026 |
| iPay88 homepage | https://www.ipay88.com/ | Still live as NTT DATA / ADAPTIS surface | Fetched 16 Aug 2026 |
| Curlec pricing | https://curlec.com/pricing/ | Official Basic/Premium MDR | Fetched 16 Aug 2026 |
| HitPay Malaysia | https://hitpayapp.com/my/ | Product, MY pricing snippets | Fetched 16 Aug 2026 |
| Revenue Monster pricing | https://revenuemonster.my/pricing | Official setup + feature list | Fetched 16 Aug 2026 |
| Xendit MY “how to choose” | https://www.xendit.co/en-my/blog/how-to-choose-the-best-payment-gateway-in-malaysia/ | Xendit’s own MY fee table, updated Jul 2026 / published 17 Apr 2026 | Fetched 16 Aug 2026 |
| Xendit MY subscriptions guide | https://www.xendit.co/en-my/blog/top-payment-gateways-for-subscription-businesses-in-malaysia/ | Recurring rails comparison, published 10 Aug 2026 | Fetched 16 Aug 2026 |
| Airwallex Curlec review | https://www.airwallex.com/en-my/blog/curlec-review | Secondary Curlec/Billplz comparison, published 13 Apr 2026 | Fetched 16 Aug 2026 |
| 2C2P Malaysia | https://2c2p.com/countries/malaysia/ | Product, methods, enterprise ICP | Fetched 16 Aug 2026 |
| Boost Biz pricing | https://myboost.co/business/boost-biz-pricing | Official MDR + package fees | Fetched 16 Aug 2026 |
| Xero MY e-invoicing | https://www.xero.com/my/initiative/e-invoicing-malaysia/ | Official Peppol / plan inclusion | Fetched 16 Aug 2026 |
| StoreHub e-invoice blog | https://www.storehub.com/my/blog/transitioning-einvoicing-tips-malaysian-businesses | StoreHub as intermediary, “no extra cost” claim | Fetched 16 Aug 2026 |
| PayNet 2025 volume PR | https://paynet.my/about-us/media-centre/press-release/8-44-billion-transactions-processed-in-2025-as-digital-payments-become-malaysians-preferred-way-to-pay.html | Rail volumes, published 22 Apr 2026 | Search + citation 16 Aug 2026 |
| NTT DATA ADAPTIS PR | https://www.nttdata.com/global/en/news/press-release/2025/april/043000 | iPay88 + eGHL unification, 30 Apr 2025 | Search 16 Aug 2026 |
| Lazuar ADRs 019, 021 and README | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/...` | Product truth | Read 16 Aug 2026 |

### Secondary sources used as context only

- Airwallex “payment gateway fees Malaysia” (8 Jun 2026), “Fiuu review” (17 Apr 2026), “Curlec vs Fiuu” (16 Apr 2026), “Shopify gateway Malaysia” (28 Apr 2026), “best subscription billing Malaysia” (28 Apr 2026).
- DHL Discover “which online payment gateway is best in Malaysia” (25 Feb 2026) — useful ADAPTIS / Fiuu / SenangPay / Billplz table; numbers not treated as official.
- EasyStore help “transaction fees for payment gateways (Malaysia)” (5 Sep 2025) and “payment gateways comparison” (24 Apr 2026).
- SiteGiant Fiuu promo page (setup RM400 / annual RM499, promo to 16 Aug 2026).
- accounting.my SenangPay / ToyyibPay / Paydibs posts (2025).
- World First Airwallex review (28 Jul 2026) and e-invoicing SME guide (17 Jul 2026).
- HitPay “what is a payment link SEA” (2 Jun 2026), “HitPay rates” (3 Apr 2026).
- Midtrans official pricing and recurring-payment feature pages (crawled Aug 2026).
- PayMongo official pricing and subscriptions pages (crawled Aug 2026).
- BNM / PayNet / LHDN public material cited via the pages above.

### Limitations (do not over-read this file)

- **No independent GMV share.** Billplz’s “MYR 5.4B+ processed in 2025” and Fiuu’s “$13B TPV FY2025” are vendor claims.
- **Enterprise MDR is sales-quoted.** iPay88 / ADAPTIS / Fiuu / 2C2P / GHL do not publish a full public rate card. Do not pretend they do.
- **Feature flags hide recurring.** Xendit, Curlec, Fiuu, and CHIP all have “request recurring / enable on account” gates. A marketing page saying “subscriptions: yes” is not the same as every merchant account having e-mandate live.
- **e-Invoice rules moved in 2025–2026.** Thresholds, consolidated-invoice windows, and the RM10,000 individual-invoice rule have been amended more than once. Treat any “deadline” paragraph as time-stamped, not eternal.
- **This file does not cover global Billing OS names** (Stripe Billing as a full product, Chargebee, Recurly, Paddle, Lemon Squeezy) except where they appear as a Malaysian alternative. Those belong in the global landscape note.

---

## How Malaysians actually get paid today

### The consumer side, 2025–2026

Malaysia is not a card-first market. It is a **bank-transfer-first, wallet-second, card-third** market, with QR becoming the default for anything that happens in person.

What the official and near-official numbers say:

- PayNet reported **8.44 billion digital transactions processed in 2025**, with DuitNow QR acceptance passing **three million registered touchpoints** and **681,250 new QR points** added in 2025, including 267,780 MSMEs (PayNet press release, 22 April 2026).
- Cross-border DuitNow QR (SG, TH, ID, CN, plus KH in 2025; IN expected 2026) grew 2.5× to 29.7 million transactions in 2025 (same PayNet release).
- BNM / industry summaries recycled by Xendit (17 April 2026) put selected retail e-payment value at **RM 698.1 billion in 2024**, with e-wallet usage at **88% in 2024** (up from 63% the year before) and DuitNow QR at **870 million transactions / RM 31.1 billion in 2024**.
- 2C2P’s Malaysia country page (fetched 16 August 2026) claims that in 2024, **94% of Malaysian ecommerce transactions used digital payments**, and sketches a 2024 mix of **cards 35% / domestic payments 35% / mobile wallets 23% / BNPL 4% / other 4%**. Treat the mix as 2C2P’s model, not a census.
- Credit-card penetration is still only about **20–25% of adults** (Xendit subscriptions guide, 10 August 2026). A Malaysian subscription business that only tokenises Visa/Mastercard is leaving most domestic subscribers on a worse rail.

What this means in a checkout:

1. **FPX B2C is non-negotiable** for any Malaysian web checkout. More than 30 banks. Customer is redirected to internet banking, authenticates, and the money moves. It is *not* a standing order. Every FPX session is a new customer action.
2. **FPX B2B** is a separate product with a higher fee (typically RM 2–3) and a different bank list. Agencies, tuition centres, and B2B SaaS that invoice Sdn Bhds need it.
3. **DuitNow QR** is the national QR. One merchant QR accepts every participating bank app and every participating wallet. Online, it is a scan-to-pay checkout tile. Offline, it is the phone-as-terminal. Cross-border QR (SG PayNow, TH PromptPay, ID QRIS, CN Alipay/UnionPay, KH) is now a real tourist-and-regional feature, not a press-release.
4. **Wallets** that matter at checkout: Touch ’n Go eWallet (still the volume leader on the consumer side — ~26 million verified users is the figure circulating in 2026 explainers), GrabPay, ShopeePay, Boost. Alipay+ / WeChat Pay matter for Chinese tourists and some marketplaces.
5. **Cards** matter for (a) higher-income Malaysians, (b) corporates, (c) foreigners, (d) anything that needs a stored credential for true merchant-initiated recurring. Local debit is cheaper than local credit. Foreign cards are 3%+.
6. **BNPL / IPP** (Atome, SPayLater, Grab PayLater, bank 0% instalment plans) is a conversion feature for RM 200–RM 5,000 carts, not a subscription rail.

### The merchant side — five stacks that actually exist

Malaysian businesses do not buy “a Stripe”. They assemble a stack. The five stacks Lazuar will walk into:

**Stack A — Informal / social seller (the default under RM 50k/month).**  
WhatsApp Business or Instagram DM → send a Billplz / ToyyibPay / senangPay / CHIP payment link or DuitNow QR → buyer pays → seller ticks a spreadsheet → at month-end someone (or no one) keys a consolidated e-invoice into MyInvois. No subscriptions. No dunning. No ledger. This stack is the real incumbent for 80%+ of Malaysian micro-merchants. Lazuar does not win this stack by being more PCI-compliant. Lazuar wins it only if the seller starts selling subscriptions, hits the e-invoice mandate, or hires a developer.

**Stack B — SME webstore.**  
EasyStore or Shopify or WooCommerce + one local gateway plugin (Billplz, CHIP, senangPay, Fiuu, Revenue Monster, HitPay) + maybe Atome + Xero or SQL Account bolted on later. Recurring is “WooCommerce Subscriptions + gateway plugin”, which is a different product from a billing OS. e-Invoice is either EasyStore’s built-in, a middleware (JomeInvoice / Assist.biz), or the accountant.

**Stack C — Malaysian SaaS / tuition / gym / membership.**  
The founder wants auto-debit. They discover FPX does not auto-debit. They then discover **FPX e-mandate / Direct Debit** (Curlec’s original product, now also claimed by Xendit) or they send a new Billplz bill every month and chase WhatsApps. Dunning is a Google Sheet. LHDN is the accountant’s problem until it is not.

**Stack D — Regional / venture-backed.**  
Xendit or Stripe or Airwallex or 2C2P, because the Series A deck said “SEA”. FPX is a checkbox. Recurring is cards + whatever local mandate the gateway actually enabled. LHDN is still nobody’s problem inside the payment product.

**Stack E — Enterprise / government / GLC.**  
iPay88 / ADAPTIS / Fiuu / GHL, because procurement already has them on a vendor list, they have a relationship manager, they have terminals + online + IPP + Alipay+, and the RFP asked for PCI + ISO + a local entity that has been around since 2005. Lazuar will not displace this stack at the acquiring layer. Lazuar can sit *on top of it* as BYOK software if the enterprise wants a ledger, dunning, and MyInvois without replacing the acquirer.

### Rails versus products (read this before the dossiers)

| Layer | Who | What they are to Lazuar |
|---|---|---|
| National rails | PayNet (FPX, DuitNow Transfer, DuitNow QR, RPP, IBG) | **Upstream rail.** Not a competitor. Lazuar never talks to PayNet directly; Billplz / CHIP / Curlec / Xendit / HitPay do. |
| Scheme / wallet rails | Visa, Mastercard, UnionPay, TnG, GrabPay, Boost, ShopeePay | **Upstream rail / substitute acquirer.** A merchant can take TnG or Boost *directly* and skip every gateway. |
| Licensed acquirer / TPA | Billplz, CHIP, Fiuu, iPay88/ADAPTIS, Curlec, Xendit-via-Payex, HitPay, Revenue Monster, GHL | **Upstream rail *and* sometimes a rival.** They take MDR. Lazuar’s BYOK model *uses* them. They become rivals the moment they ship hosted checkout + subscriptions + invoices that are “good enough”. |
| Checkout / billing software | HitPay, Xendit Subscriptions, Curlec Subscriptions, Stripe Billing, Airwallex Billing, senangPay Catalog, Billplz Catalog | **Direct rival for the software job**, even when they also acquire. |
| Commerce OS | EasyStore, Shopify, StoreHub, WooCommerce | **Substitute.** They own the “Buy” button. Payments and e-invoice are modules. |
| Compliance OS | MyInvois portal, Xero, AutoCount, SQL Account, FastAccount, Peppol access points | **Partner or substitute for the compliance half of the JTBD.** They do not collect money. |
| Chat + spreadsheet | WhatsApp + DuitNow + Excel | **The incumbent substitute.** Beats every SaaS on time-to-first-payment. |

Lazuar’s unusual move is to **refuse the acquiring layer**. Every other name in this file either is an acquirer or is a commerce OS that resells an acquirer. That is why “partner” appears so often below. It is also why the moat is not “we have FPX”. Everyone has FPX. The moat is **FPX + LHDN + dunning + BYOK ledger, sold as software**.

### What “recurring” actually means in Malaysia (the most important technical fact in this file)

Xendit’s 10 August 2026 subscriptions guide is unusually honest and matches how every serious Malaysian billing engineer talks:

- **Standard FPX cannot be merchant-initiated.** The customer must log into their bank every cycle. A “recurring FPX” that is just a new payment link each month is not a subscription engine. It is a reminder engine.
- **True auto-debit rails in MY are:**
  1. **FPX e-mandate / Direct Debit** — customer authorises once (often via an RM 1 test FPX), then the merchant pulls. Curlec built the category. Xendit now claims it. Almost nobody else has it as a first-class product.
  2. **Card tokenisation** — Visa/Mastercard stored credential, MIT after initial 3DS. Works. Coverage is the 20–25% of adults with cards, plus foreigners.
  3. **E-wallet mandates** — TnG / GrabPay / ShopeePay recurring pulls. Best for RM 9.90–RM 99 consumer subs. Failure mode is empty wallet, not expired card.
- **Involuntary churn from failed payment is 20–40% of total churn** in subscription businesses; smart retry recovers 40–85% of those (Xendit, 10 Aug 2026, citing the usual subscription-industry ranges). This is the entire reason Lazuar’s dunning pillar exists.
- **PDPA applies to stored tokens and mandates.** The right architecture is: tokens live at the PCI-DSS gateway (Billplz/CHIP/Stripe/Curlec/Xendit), not in Lazuar’s application database. Lazuar should store mandate *references* and subscription state, not PANs.

If Lazuar’s “subscriptions” product is only “create a Billplz bill every month and email the link”, it is feature-equal to Billplz Catalog Billing and worse than Curlec. The product has to orchestrate **whichever true MIT rail the merchant’s BYOK keys actually support**, then run dunning across email (now) and WhatsApp (roadmap) when that rail fails.

### What “compliant” actually means in Malaysia (the second most important fact)

Two regimes get collapsed in sales conversations. They are not the same.

**SST (Sales and Service Tax)** is a consumption tax. Digital services, SaaS, and many B2C services are in scope at 8% (service tax) depending on activity and threshold. A checkout that cannot itemise SST, cannot emit a tax invoice, and cannot put the right tax code on the ledger is not a Malaysian checkout.

**LHDN e-Invoice (MyInvois)** is a *transaction reporting* mandate, not a VAT-style clearance in the European sense, but it behaves like one: every invoice (or a monthly consolidated B2C invoice) must be submitted to IRBM, validated, and stored, typically as UBL 2.1 XML/JSON with a long mandatory field list (StoreHub’s own explainer still says “up to 55 fields”). Individual e-invoices for transactions above a threshold (secondary 2026 explainers cite RM 10,000 as the point where consolidated invoices are no longer allowed — confirm against the current LHDN guideline before shipping copy) and buyer-requested e-invoices must go out per transaction. B2C can still be consolidated monthly and submitted within the statutory window after month-end.

Two transmission paths exist (LHDN / BDO / Xero all describe the same split):

1. **MyInvois Portal** — free, manual, fine for a few dozen invoices a month, unusable at SaaS volume.
2. **MyInvois API** — direct or via an intermediary. This is what Xero, StoreHub, AutoCount, SQL, FastAccount, JomeInvoice, and Lazuar’s LHDN module all sit on.

Peppol is the *network* Malaysia also uses for system-to-system invoice exchange (MDEC is the authority). MyInvois is the *tax authority clearance*. A complete Malaysian invoice stack often needs both: MyInvois for LHDN, Peppol for sending a structured invoice into the buyer’s Xero/SAP. Lazuar’s current backend is MyInvois-shaped (UBL 2.1 XML, sign, submit, QR). Peppol access-point status is a later question, not a 2026 MVP requirement, but Xero already uses it as a marketing wedge.

**The gap Lazuar is betting on:** Stripe does not emit LHDN XML. Billplz is a pipe. Xero emits LHDN XML but does not own the checkout button. Nobody owns “the payment happened, therefore the tax document happened, therefore the ledger balanced, therefore the failed renewal was retried on WhatsApp”.

---

## Competitor dossiers (one H2 per company)

---

## Billplz

### Who they are

Billplz is the default Malaysian collection brand for anyone who is not an enterprise procurement department. Official site: [https://main.billplz.com/](https://main.billplz.com/). HQ Malaysia. Stage: scaled local independent (not a unicorn, not a bank subsidiary). Positioning: “Payment gateway for Malaysian businesses”, “trusted where payments can’t fail”.

**Vendor claims on the homepage (fetched 16 August 2026):** 60,000+ Malaysian organisations; 19.5M+ transactions in 2025; MYR 5.4B+ processed in 2025; 99.9% historical uptime. Logo wall includes PDRM (MyBayar Saman), Perodua, Boost, Wahed, Lembaga Zakat Selangor, Pandora, Farm Fresh / Happikiddo, PTPTN. That logo wall is the point: Billplz is what you use when the money is zakat, traffic summons, PTPTN, or school fees — high-trust, high-volume, low-glamour collections.

**ICP:** SMEs, NGOs, schools, government-adjacent collections, Islamic finance, social sellers, and any WooCommerce/EasyStore merchant who wants the cheapest honest FPX. Not a good fit for a Series B marketplace that wants split payments across six SEA countries (they do have split payments and Payment Order, but the gravity is MY).

**Relationship to Lazuar:** **Upstream rail + substitute + occasional partner.** Lazuar’s own README lists Billplz as a first-class BYOK gateway. Every Malaysian founder Lazuar pitches already has a Billplz account, or their accountant does.

### Pricing model

**Take-rate + optional annual SaaS.** Official pricing page (fetched 16 August 2026):

| | Basic (MYR 0/year) | Standard (MYR 999/year) | Enterprise |
|---|---|---|---|
| FPX B2C | **RM 1.25** | **RM 0.75** | Custom |
| FPX B2B | **RM 3.00** | **RM 2.00** | Custom |
| FPX payout | Next business day | Next business day | Real-time available |
| Cards MYR | **1.8%** | **1.5%** | Custom |
| Cards non-MYR | 3.8% (optional) | 3.5% (optional) | Custom |
| Auto-Deduct MYR | 2.3% + RM 1.25 | 2.0% + RM 0.75 | Custom |
| Auto-Deduct non-MYR | 4.2% + RM 1.25 | 4.0% + RM 0.75 | Custom |
| Card payout | T+2 | T+2 | — |
| Wallets (DuitNow QR, TnG, Boost, GrabPay) | **1.5%** | **1.5%** | Custom |
| Wallet payout | Next day | Next day | — |
| Atome | 6%, 3-month, payout Wed & Fri | Same | Custom |
| Payment Order (DuitNow Transfer) | RM 1.25, real-time | RM 0.75, real-time | Custom |
| Catalog Link / Store / Billing | Free + Billplz txn fees | Free + Billplz txn fees | — |
| Shopify plugin | Billplz + **0.3%** | Billplz + **0.3%** | — |

No setup fee on Basic. No contract. Enterprise adds: dedicated AM, branded bank transactions, FPX CCA (credit-card-on-FPX-rails with lower fees and instant payout), Receivables/Payables/Reconciliation BPO, Auto Payment Order.

This is the cheapest honest public FPX in the country on the paid plan (RM 0.75 B2C). CHIP is RM 1.00 with no annual fee. ToyyibPay is RM 1.00 with no annual fee. Billplz Standard wins on volume FPX; Billplz Basic loses to CHIP/ToyyibPay on a per-txn basis if the merchant refuses the RM 999.

### Feature list

| Capability | Billplz reality (16 Aug 2026) |
|---|---|
| Hosted checkout / Bill Page | Yes. The original product. |
| Payment links | Yes. Catalog Link / Payment Form. Free, unlimited forms on Catalog. |
| Online storefront | Yes. Catalog Store. Inventory, categories. |
| Catalog Billing | Yes. Listed as free on both plans. This is membership-style recurring collection, **not** FPX e-mandate. |
| Subscriptions / dunning | Recurring via API, plugins, and Catalog Billing. Billplz’s own comparison post vs Curlec (5 Nov 2025) admits Curlec has the “built-in subscription engine with flexible models, automated retries, and lifecycle tools” and positions Billplz as “great for simple subscriptions”. Xendit’s 10 Aug 2026 guide is harsher: **no e-mandate, no card recurring**, limited wallets, FPX recurring is payment-link-based. |
| Invoices | Bills *are* invoices in the Billplz sense. Not LHDN e-invoices. |
| Tax / LHDN | Not a MyInvois product. Merchant or accountant files. |
| API | REST, documented at billplz.com/api. Predictable, old, widely wrapped. |
| Webhooks | Yes. Callback URLs on bills. The Malaysian industry’s reference implementation of “ping me when paid”. |
| Multi-gateway / BYOK | No. Billplz *is* the gateway. |
| Split payments | Yes. First-class. |
| Payouts | Payment Order API, DuitNow Transfer, real-time. |
| Plugins | 30+ claimed. WooCommerce, Shopify, EasyStore, plus a long tail of Malaysian CMS plugins. |
| Security | PCI DSS, ISO 27018:2019 claimed, PayNet System Integrator badge. |
| Multi-account / team / SSO | Yes on current plans. |
| Statements | Download up to 2 years. |

### Strengths versus Lazuar

- **Distribution.** 60k organisations is a distribution moat. Every Malaysian accountant, school clerk, and WordPress freelancer already knows how to create a Billplz bill.
- **Trust logos that Lazuar cannot buy.** PDRM, PTPTN, zakat boards.
- **FPX price.** RM 0.75 on Standard is the number every founder quotes.
- **Next-day FPX payout, guaranteed in marketing.** Cash-flow argument for SMEs.
- **Catalog is good enough** for the social seller. A Lazuar hosted checkout that is “more beautiful” does not displace a link the seller already sent yesterday.
- **Split + Payment Order** make Billplz a light marketplace/payouts tool. Lazuar does not ship payouts.
- **They are already inside Lazuar** as a BYOK connector. They do not need to win the merchant away from Lazuar to keep earning MDR.

### Weaknesses versus Lazuar

- **Dumb pipe on purpose.** ADR 019’s phrase is accurate: Billplz does not own compliance logic, does not own a double-entry ledger of *the merchant’s* books, and does not own a dunning OS.
- **Recurring is the hole.** No FPX e-mandate, no card-on-file recurring (Xendit 10 Aug 2026). Auto-Deduct on the pricing page is a *rate*, not a billing engine.
- **No LHDN.** A Billplz receipt is not a MyInvois-validated e-invoice. The QR on a Billplz page is not the LHDN QR.
- **MY-only gravity.** Fine for Lazuar’s ICP. A problem only if the merchant is expanding to ID/PH/TH.
- **Developer experience is 2014-shaped.** It works. It is not Stripe-shaped. Indie hackers who have used Stripe Billing will bounce off bill-collection metaphors (collections, bills, callbacks).
- **They monetise GMV.** A merchant who is allergic to MDR still pays Billplz MDR even if they use Lazuar on top. Lazuar cannot make Billplz cheaper; it can only make Billplz *sufficient* so the merchant does not also buy Curlec or Xendit Subscriptions.

### Rival type

**Upstream rail + substitute + partner.** Direct rival only for the “I just need a payment link” job. Not a direct rival for “I need subscriptions + LHDN + ledger + webhooks into my SaaS”.

### Features Lazuar should track

- Catalog Billing depth (trial, proration, failed-payment retry, customer portal).
- Any launch of **true card recurring or FPX e-mandate**.
- Any launch of **MyInvois / e-invoice receipts** (CHIP already markets “issue e-Invoice receipts”; Billplz will be forced to follow).
- Shopify 0.3% surcharge — if Billplz ever waives it, Shopify-MY merchants consolidate further on Billplz.
- Payment Order becoming a self-serve payouts product with a better UI (marketplace wedge).
- Enterprise FPX CCA — if they productise “cards at FPX prices”, the card-vs-FPX economics change.

---

## CHIP / Chip Collect

### Who they are

CHIP (Chip-in Sdn Bhd) is a Malaysian digital-finance platform, not only a gateway. Official site: [https://www.chip-in.asia/](https://www.chip-in.asia/). HQ Malaysia. Stage: growth-stage regulated local. Public positioning: Collect (payments) + Control (Send payouts, Expense) + Capital (CHIP Advance, Shariah sales-based financing). BNM-registered non-bank merchant acquirer; PayNet TPA for FPX; ISO-certified security; Visa-listed service provider (badges on homepage, fetched 16 August 2026). Featured in Harian Metro, Astro AWANI, Fintech News MY for the BNM acquirer approval.

**Vendor claims:** 5,000+ growing brands; logos include Bateriku, ezQurban, Pak Mat Western, Todak, CloudJoi, Muslimtravelbug, Berjaya Waterfront, Adasms. Developer community on Facebook. Open GitHub org (`CHIPAsia`). Docs at docs.chip-in.asia.

**ICP:** Malaysian SMEs and SaaS/marketplaces that want one regulated stack for collect + payout + (soon) expenses + working-capital advance. Stronger developer posture than Billplz. Weaker household-name trust than Billplz/iPay88. Explicitly courts “SaaS platform & marketplace” with Collect API + Send API.

**Relationship to Lazuar:** **Upstream rail + partner + emerging rival.** README lists CHIP as a first-class BYOK gateway. CHIP’s own homepage now sells the exact SaaS-embed story Lazuar sells, except CHIP takes MDR and holds the money.

### Pricing model

**Pure take-rate. No setup, no monthly, no annual.** Official pricing page (fetched 16 August 2026). Transaction fees subject to 8% SST.

**Online / Collect:**

| Method | Official rate | Settlement |
|---|---|---|
| FPX B2C | **RM 1.00** | Next day |
| FPX B2B | **RM 2.00** | Next day |
| Local credit card | **2.0%** | T+2 |
| Local debit card | **1.0%** | T+2 |
| Foreign cards | **3.0%** | T+2 |
| Apple Pay | Coming soon (badge) | — |
| Google Pay | Available | — |
| DuitNow QR online (incl. cross-border ID/TH/SG) | **1.0% (min RM 0.15)** | Next day |
| E-wallets (TnG, GrabPay, ShopeePay) | **1.4%** | T+2 |
| Atome | **5.3%** | Thursday of following week |
| SPayLater | **1.4%** | T+2 |
| Stablecoin (BTC, ETH, PYUSD, USDC, USDT; ETH/Arb/Polygon/Solana/TON/Tron) | **1.5%** + 1.5% refund fee; excl. gas | T+1, settle in MYR |

**In-person (CHIP mini + POS):** DuitNow QR same 1.0% min RM 0.15. POS cards: local credit 1.35%, local debit 1.00%, foreign 4.40% (subject to approval). POS wallets itemised (TnG 0.95%, GrabPay 1.05%, Boost 1.00%, ShopeePay 1.15%, Maybank QRPay 0.95%, Alipay 1.00%, UnionPay QR 1.80%).

**Send:** RM 1.00 per successful transfer; RM 1.00 one-time bank-account verification; real-time.

**Expense:** transaction fee waived.

**Advance:** from 6% one-time + 0.5% stamping; up to RM 500,000; no interest/late fees; repay from sales; funds in 48 hours.

Refund fees: FPX B2C RM 1.00 / B2B RM 2.00 only.

This is the cleanest public rate card in Malaysia after Billplz. Versus Billplz Basic, CHIP is cheaper on FPX (RM 1.00 vs RM 1.25) and cheaper on local debit (1.0% vs 1.8% blended card). Versus Billplz Standard, CHIP is more expensive on FPX (RM 1.00 vs RM 0.75) and has no annual fee.

### Feature list

| Capability | CHIP reality (16 Aug 2026) |
|---|---|
| Hosted checkout | Yes. Website / app / plugins. |
| Payment links | Yes. No-code, branded. |
| In-person | CHIP mini (phone as DuitNow QR terminal). POS terminals coming/available. |
| Subscriptions | **Yes, first-class in docs** (`docs.chip-in.asia/chip-collect/overview/online-purchases/subscription`). Pricing page lists “Recurring payments” and “Auto-recurring payments available” on cards. This is card/token recurring, not (publicly) FPX e-mandate. |
| Pre-authorisation | Yes, documented. |
| Invoices | Products + pricing in dashboard. Not a full billing OS. |
| Tax / LHDN | Homepage feature chip: **“Issue e-Invoice receipts.”** This is the most important CHIP-vs-Billplz delta for Lazuar. Depth unknown from marketing — could be a receipt with buyer TIN, or a real MyInvois submission. **Must be verified in a CHIP merchant account.** |
| API / webhooks | REST Collect + Send. Webhooks documented. Sandbox / test mode. SDKs, GitHub, “integrate with AI agent / vibe-coding guide”. |
| Multi-gateway / BYOK | No. CHIP is the acquirer. |
| Multi-brand | Yes. Multiple brands under one account. |
| Payouts | CHIP Send, real-time, approver rules. |
| Plugins | 20+ sales platforms, 30+ plugins claimed. |
| Stablecoin collect, MYR settle | Yes. Unusual locally. Overlaps Lazuar ADR 021 Pillar 3 (cross-border + tax) in spirit, not in compliance depth. |
| Financing | CHIP Advance. Shariah. |
| Expenses | CHIP Expense app. Out of Lazuar scope. |

### Strengths versus Lazuar

- **Regulated acquirer.** BNM non-bank merchant acquirer + PayNet TPA. Lazuar is software. When a risk team asks “who holds the money?”, the answer is CHIP, not Lazuar.
- **One vendor for collect + payout + capital.** A marketplace founder can collect, split, and pay affiliates without adding a second payouts product.
- **Public, complete rate card.** Procurement-friendly.
- **Developer posture.** Docs, GitHub, sandbox, webhook reference, even an AI-agent guide. This is the local gateway that looks most like “a small Stripe”.
- **e-Invoice receipts (claimed).** If this is real MyInvois, it punches Lazuar in the mouth on the compliance pillar.
- **Stablecoin + cross-border QR.** CHIP is already collecting USDT and ID/TH/SG QR. Lazuar’s Pillar 3 is still an ADR.
- **Phone-as-terminal.** CHIP mini is a distribution channel into offline SMEs Lazuar will never touch.

### Weaknesses versus Lazuar

- **They are the money.** Merchants who want to keep Stripe *and* Billplz *and* CHIP keys, or who already have an iPay88 MID they cannot abandon, cannot use CHIP as an orchestration layer. Lazuar can.
- **Subscription engine is a gateway feature, not a billing OS.** No evidence of multi-phase schedules, credit-note-driven proration against a ledger, dunning campaigns, or WhatsApp retry. Docs describe creating a subscription purchase.
- **e-Invoice depth is unproven from the outside.** “Issue e-Invoice receipts” on a marketing page can mean a PDF. Lazuar’s LHDN module is UBL 2.1 XML, signed, submitted. Until CHIP publishes MyInvois intermediary status, treat this as a watch item, not a loss.
- **No double-entry merchant ledger.** CHIP’s dashboard is a processor dashboard. It is not the merchant’s books.
- **MY-only licence.** Same as Billplz.
- **Brand.** 5k merchants vs Billplz’s 60k claim. Sales cycles still start with “CHIP ke Billplz?”.

### Rival type

**Upstream rail + partner + emerging direct rival** on hosted checkout, payment links, SaaS embed, and (if real) e-invoice. Partner first: Lazuar should treat CHIP as the default *local* BYOK connector alongside Billplz, because the rate card is public, the API is modern, and recurring + e-invoice receipts are moving in Lazuar’s direction.

### Features Lazuar should track

- Exact behaviour of **e-Invoice receipts** (MyInvois UUID? consolidated B2C? buyer TIN validation? credit/debit/refund notes?).
- Subscription API: trial, proration, retry schedule, webhook events, e-mandate or only cards.
- Send API becoming a marketplace product (sub-accounts, held funds, seller KYC).
- Stablecoin rails — if CHIP starts classifying these as LHDN export/zero-rated, they have walked into Pillar 3.
- CHIP Advance underwriting using Collect data — a capital product Lazuar must never copy (licence, balance-sheet, Shariah board).

---

## ToyyibPay

### Who they are

ToyyibPay is Malaysia’s Shariah-positioned collection platform. Official site: [https://www.toyyibpay.com/](https://www.toyyibpay.com/). HQ Malaysia. Founded 2019 (homepage). Stage: SME / NGO fintech. Positioning: “Empowering Payments with Trust and Purpose”, “leading Shariah-compliant fintech platform” for businesses, government agencies, and NGOs.

**ICP:** NGOs, mosques, religious bodies, Islamic SMEs, social sellers, and cost-sensitive B2C merchants who will live on FPX-only. The **Santai** plan (RM 0 B2C for NPOs, T+10 settlement) is a category-defining price for zakat, infaq, and association fees.

**Relationship to Lazuar:** **Substitute + occasional upstream rail.** Not a natural BYOK partner (API is thinner than Billplz/CHIP). Direct rival only at the bottom of the funnel: “I need a halal-feeling payment link for RM 1.”

### Pricing model

**Take-rate, almost no SaaS.** Official pricing page (fetched 16 August 2026):

| Plan | Who | FPX B2C | FPX B2B | Settlement |
|---|---|---|---|---|
| Santai | NPO only | **RM 0.00** | RM 2.00 | Next **10** business days |
| Standard | Everyone | **RM 1.00** | RM 2.00 | Next **1–4** business days |

**Cards** (via “toyyibPay Partners”, not native): local **1.50%**, foreign **3.5%**. Onboarding **RM 100**. Yearly **RM 100** starting subsequent year. MYR only. Settlement next 4 business days.

**DuitNow QR:** **1.00% or RM 1.00** per transaction, next 2 business days, subject to provider approval.

No monthly fee on FPX. Cards are the only place they charge an annual.

### Feature list

Payment Link & QR; auto settlement; split payment; dashboard; API & plugins (WooCommerce plugin on wordpress.org); payouts/disbursement page; Toyyib+ extras (Seedflex live; Toyyib355, ToyyibGold, Wasiat, Waqf “coming soon”). Homepage also mentions subscriptions as a use case (“simplifies collections, donations, and subscriptions”) without documenting a billing engine.

### Strengths versus Lazuar

- **Shariah certificate as a sales weapon.** Islamic schools, zakat, waqf-adjacent, and conservative SMEs will pick ToyyibPay over a “fintech from PJ that talks about USDC” every time.
- **RM 0 NPO FPX.** Unbeatable for the NGO ICP. Lazuar should never try to match this with a take-rate, and cannot match it as software.
- **RM 1 flat for everyone else.** Same as CHIP FPX, no annual, less product around it.
- **WhatsApp/Facebook support culture.** Matches how Malaysian SMEs actually get help.

### Weaknesses versus Lazuar

- **Not a billing OS, not a compliance OS, not a developer platform.** API reference exists; it is not CHIP/Xendit.
- **Card is partnered and gated** (RM 100 + RM 100/year). Recurring card is not the product.
- **Settlement on Santai is T+10.** Cash-flow hostile; the “free” is paid in float.
- **No LHDN, no ledger, no dunning, no BYOK, no multi-gateway.**
- **Coming-soon Islamic product cloud** (gold, wasiat, waqf) is a distraction, not a checkout feature.

### Rival type

**Substitute** for Islamic / NGO / RM-1-FPX collections. **Not a partner** worth prioritising unless a specific tenant demands it as a BYOK connector.

### Features Lazuar should track

- Any real subscription / e-mandate product.
- Any MyInvois feature (Islamic NGOs still have to e-invoice when they are in scope).
- Seedflex / financing attaching to collections (same capital wedge as CHIP Advance).

---

## SenangPay (senangPay-DOKU)

### Who they are

senangPay is the SME-friendly Malaysian gateway that spent a decade owning “instant approval + payment form + RM199/year”. In 2025–2026 it is **senangPay-DOKU**: the homepage (fetched 16 August 2026) announces the combination with **DOKU** (Indonesian gateway, itself historically tied to Bank Central Asia’s orbit and now a regional SME brand), “local expertise and regional strength to support 160,000+ businesses. Still *senang*, now more powerful.” Sandbox now lives at `sandbox.doku.com`. This is the most important corporate fact about senangPay in 2026: it is no longer a Malaysia-only SME toy; it is DOKU’s Malaysian face.

**ICP:** Social sellers, EasyStore/Shopify/WooCommerce SMEs, Tabung Haji-adjacent and retail brands on the logo wall, web developers who want a sandbox. Enterprise plan for Tabung Haji, Sutera, Inhanna, etc.

**Relationship to Lazuar:** **Direct rival for the SME hosted-checkout job** + **substitute** + **possible future BYOK connector**. They sell payment links, digital catalog, invoices/quotations, recurring (Advance+), tokenisation, and even “e-invoice and quotation” as a dashboard feature.

### Pricing model

**Annual SaaS + take-rate.** Official pricing (fetched 16 August 2026). Raya 2026 promo: RM50 off with code RAMADAN26.

| Plan | Annual (list) | What you get |
|---|---|---|
| Starter | **RM 199** (promo RM 149) | Instant approval for FPX + e-wallet; basic features; 24/7 support |
| Advance | **RM 349** (promo RM 299) | All payment options; 0% IPP (extra cost); advanced features |
| Enterprise | Custom | Lowest MDR, custom API, flexible settlement, dedicated AM |

**Official transaction rates (same page):**

| Method | Rate |
|---|---|
| FPX | **RM 1 or 1.5%, whichever is higher** |
| Local cards | **RM 0.65 or 2.5%, whichever is higher** (Enterprise can go lower; JCB not yet on new DOKU platform) |
| Foreign cards | Package-based |
| E-wallets | **RM 0.65 or 1.5%, whichever is higher** (Boost & Shopback not yet on new platform) |
| SPayLater | **2.0%** |
| Grab PayLater 4x / 8x / 12x / postpaid | **6.0%** (processing fees on 8x/12x) |
| Atome 3x | **5.5%** |
| Bank IPP | Extra; one-time activation; up to 24 months |

The “whichever is higher” construction is hostile to small tickets. A RM 20 FPX payment costs RM 1 (5%). A RM 200 FPX payment costs RM 3 (1.5%). Billplz/CHIP/ToyyibPay are flat on FPX and win every cart under ~RM 67 versus senangPay’s 1.5% limb.

### Feature list

**All packages:** 24/7 support; FPX; digital payments; Digital Catalog (no website); dashboard; shopping-cart plugins; **e-invoice and quotation** (customisable invoice with payment); API.

**Advance / Enterprise:** dedicated AM (Enterprise); lower MDR (Enterprise); **Payout API**; **Recurring payment**; **Tokenisation**; **Mass or split payments**; faster settlement; foreign cards.

Social selling, ecommerce plugins (Shopify, WooCommerce, EasyStore), sandbox, branded checkout customisation, BNM-regulated + PCI DSS claimed.

### Strengths versus Lazuar

- **Time-to-first-payment for non-technical SMEs.** Instant FPX/e-wallet approval is the sales line. Lazuar’s BYOK model *adds* a hop (get a gateway account, then paste keys).
- **Digital Catalog + quotation + payment** is the social-seller workflow in one vendor. This is HitPay’s job and Billplz Catalog’s job.
- **“E-invoice and quotation”** on the dashboard — even if this is a commercial invoice and not MyInvois, the *word* is on the pricing page. Buyers searching “payment gateway e-invoice Malaysia” will land here.
- **DOKU backing.** Regional sandbox, more methods over time, a story for merchants who also sell in Indonesia.
- **24/7 local support** as a paid-plan inclusion. Software companies hate staffing this. SMEs buy it.

### Weaknesses versus Lazuar

- **Expensive FPX on percentage.** The “RM 1 or 1.5%” clause is the reason Billplz/CHIP win every comparison table.
- **Annual fee.** RM 199–349 is not large, but it is a commitment, and refunds are explicitly refused.
- **Recurring is gated to Advance+** and is not described as e-mandate. Tokenisation is card-shaped.
- **No BYOK, no merchant ledger, no dunning OS, no developer-grade subscription object.**
- **Platform transition risk.** “Boost & Shopback not yet on the new senangPay-DOKU platform”, “JCB later” — mid-migration roughness.
- **They take MDR + annual.** A Lazuar tenant who already has Stripe+Billplz keys does not need another acquirer.

### Rival type

**Direct rival** for SME checkout / payment form / quotation-to-pay. **Substitute** for informal sellers. **Not** a billing-OS rival. **Possible partner** only if DOKU APIs are clean enough to add as a BYOK connector (low priority vs Billplz/CHIP/Xendit).

### Features Lazuar should track

- Whether “e-invoice and quotation” becomes **MyInvois API** (DOKU has Indonesian e-faktur scars; they know this movie).
- Recurring: mandate type, retry, customer portal.
- Cross-border MY↔ID via DOKU as a single merchant account.
- IPP depth (0% bank instalments) — a conversion feature Lazuar should not rebuild, but should allow via the underlying gateway.

---

## Fiuu (ex Razer Merchant Services / MOLPay)

### Who they are

Fiuu is the 2024 rebrand of **Razer Merchant Services**, itself the 2018 rebrand of **MOLPay** (founded 2005). Still in the Razer orbit. Official site: [https://fiuu.com/](https://fiuu.com/). HQ / heritage: Malaysia, operating across SEA (LinkedIn: 8 countries; homepage: Malaysia, Singapore, Philippines and SEA). Stage: scaled regional acquirer. Positioning: “Powering Future Payments In Southeast Asia”, “leading online SEA payment gateway” + “largest in-person SEA payment network”.

**Vendor claims (homepage, fetched 16 August 2026):** $13B total payment volume FY2025; 110+ payment methods; clientele wall includes Google, Alibaba, TikTok, TikTok Shop, Trip.com, AirAsia, Grab, Starbucks, Adidas, Nike, 7-Eleven. Direct acquiring licence in Malaysia. “Processing high volumes of up to > 483,769 transactions per day” on a search-result snippet. Cash-over-counter via 7-Eleven etc. is a real differentiator.

**ICP:** Mid-market and enterprise ecommerce, marketplaces, super-apps, airlines, retail chains, anyone who needs **one MID for cards + FPX + wallets + BNPL + Alipay+ + crypto + OTC cash + terminals + Tap to Pay on iPhone**. Not the RM 1 FPX NGO.

**Relationship to Lazuar:** **Upstream rail (listed in Lazuar Phase 1 BYOK list) + substitute for “we just use Fiuu for everything”.** Direct rival only when a merchant uses Fiuu Payment Links + Recurring and decides they do not need a billing OS.

### Pricing model

**Unpublished on fiuu.com.** Airwallex’s Fiuu review (17 April 2026) states this explicitly: no public transaction fees, setup, or annual. Secondary numbers in circulation:

- SiteGiant promo (valid till 16 August 2026): one-time setup **RM 400 → RM 200**, yearly maintenance **RM 499 → RM 249** for new SiteGiant merchants.
- DHL Discover table (25 Feb 2026): setup RM 400–499, annual RM 99–499, FPX 2.4%–3.8% or RM 0.60, cards 2.4%. **Treat as stale/indicative.**
- EasyStore help still talks about a complimentary Fiuu account (free signup + free annual worth RM 899) for yearly EasyStore plans.

**Pricing model type:** take-rate + setup + annual, sales-quoted, volume-tiered. The opposite of Billplz’s public card.

### Feature list (from fiuu.com, fetched 16 August 2026)

Virtual Terminal (incl. **Tap to Pay on iPhone**, MY & SG); Mobile XDK; Tokenisation; Seamless (non-redirect) integration; Marketplace payment; **Recurring payment**; Gateway solutions; Instalment payment; Mass payment; Payment links; Restorify (carbon — ignore).

Channels: Apple Pay, Google Pay, e-wallets, Fiuu Cash (OTC), BNPL, **Direct Debit** (Affin, Bank Islam, HSBC, Maybank, StanChart, UOB logos), Alipay+ / Alipay, **FPX & FPX B2B**, DuitNow, **Crypto**.

In-person: reloads, bill payment, gift cards, offline e-wallet, physical terminals.

This is the widest Malaysian *channel* catalogue. Direct Debit on the Fiuu site is the second place after Curlec/Xendit where a major acquirer is advertising bank-account recurring. Depth (e-mandate vs partner bank debit) is not documented on the marketing page.

### Strengths versus Lazuar

- **Channel completeness.** If the RFP says “Alipay+, 7-Eleven cash, Apple Pay, crypto, IPP, terminals”, Fiuu ticks boxes Lazuar will never tick as software.
- **Enterprise proof.** Google / TikTok / AirAsia logos close deals Lazuar cannot enter.
- **Direct Debit + Tokenisation + Recurring** on one acquirer — a merchant can stay inside Fiuu for the whole subscription job if they accept Fiuu-grade billing UX.
- **Seamless (iframe/embedded) checkout.** Lazuar’s hosted-checkout thesis has to be *better* than Fiuu seamless, or merchants will just embed Fiuu.
- **In-person + online one vendor.** Omnichannel retail.

### Weaknesses versus Lazuar

- **Opaque price.** Founders who write comparison spreadsheets bounce to Billplz/CHIP/HitPay.
- **Setup + annual.** Hostile to the indie-hacker ICP.
- **Developer experience is RMS-era.** Docs exist; they are not a cultural export the way Stripe’s or even CHIP’s are.
- **No merchant ledger, no LHDN product, no BYOK orchestration, no WhatsApp dunning.**
- **Brand churn.** MOLPay → RMS → Fiuu in six years. Trust is in the acquiring licence and the TPV, not the name.
- **They hold the money.** Same BYOK inversion as every acquirer.

### Rival type

**Upstream rail + partner (BYOK)** for mid-market. **Substitute** for enterprises who want one throat to choke. **Not** a compliance-CaaS rival.

### Features Lazuar should track

- Direct Debit: is it FPX e-mandate, bank-specific ACH-like debit, or card-style token? Webhook model?
- Recurring product: retry, dunning emails, customer portal, usage-based.
- Any MyInvois / e-invoice launch (they will be asked by every MY enterprise in 2026).
- Marketplace / split-settlement features (XenPlatform competitor).
- Tap to Pay / Virtual Terminal creeping into “payment link for field sales” — adjacent to Lazuar hosted checkout.

---

## iPay88 / ADAPTIS (NTT DATA Payment Services)

### Who they are

iPay88 is the original Malaysian household-name gateway (Mobile88 Group, then a long enterprise era). In April 2025 NTT DATA Payment Services unveiled **ADAPTIS** as the unified service brand consolidating **iPay88** and **eGHL** (NTT DATA PR, 30 April 2025; rollout MY then TH/PH). As of 16 August 2026, `ipay88.com` still resolves and presents as NTT DATA Payment Services Malaysia / Adaptis e-Commerce. The legal and sales motion a merchant meets may say iPay88, eGHL, ADAPTIS, or NTT DATA depending on who last emailed them.

**ICP:** Enterprise, government, airlines, universities, large ecommerce, anyone whose procurement policy requires a 20-year-old local vendor with an account manager. LinkedIn for NTT DATA Payment Services claims 500,000+ payment touchpoints and 30 years across MY/TH/PH.

**Relationship to Lazuar:** **Upstream rail for enterprises + substitute.** Rarely a partner (onboarding is heavy; BYOK keys are not self-serve). Direct rival only in RFPs that list “hosted payment page + recurring + invoices”.

### Pricing model

**Unpublished.** Secondary (DHL Discover, 25 Feb 2026) quotes ADAPTIS setup RM 688–4,988, annual RM 500–800, FPX 1.7–2.4%, cards 1.7–2.4%, and separately notes packages with waived setup/annual. Xendit’s April 2026 table quotes an **iPay88 SME plan** at setup RM 527, annual RM 540, DuitNow QR 1.0–1.5%, FPX 2.7% or RM 0.60 (higher), local cards 2.40–2.70%, foreign 3.30%, wallets 1.50%. **None of this is on ipay88.com.** Treat as indicative. Model: setup + annual + MDR, negotiable.

### Feature list

Historically: 37+ channels, recurring, IPP, Alipay, regional (MY, PH, and the old iPay88 country sites), WooCommerce plugin (redirect checkout), virtual terminal. Under ADAPTIS: In-Store, e-Commerce, Financing, VAS, Bill Payments (NTT DATA / Instagram). HitPay’s 2022 comparison (stale) listed recurring as a pro and “limited international Visa/Mastercard” as a con — do not rely on that in 2026.

Developer experience is redirect-centric and enterprise-integration-centric. Not a hobbyist API.

### Strengths versus Lazuar

- **Procurement inertia.** Once a university or GLC is on iPay88, they stay.
- **Omnichannel under NTT DATA.** Terminals + online + financing + bill payments.
- **Regional legal entities** for TH/PH expansion via the same parent.
- **Brand recognition among non-technical Malaysian owners** over 40. “Just use iPay88” is still a sentence.

### Weaknesses versus Lazuar

- **Price and friction.** Setup, annual, sales-led, slow card onboarding (industry folklore, consistent across years of founder chats).
- **No public developer love, no billing OS, no LHDN product, no BYOK orchestration.**
- **Brand confusion during ADAPTIS migration.** Docs, plugins, and merchant portals will be inconsistent for 12–24 months.
- **Redirect checkout** as the default mental model. Modern SaaS wants embedded or hosted-but-branded.

### Rival type

**Upstream rail (enterprise) + substitute.** Not a partner for self-serve BYOK. Not a software rival.

### Features Lazuar should track

- ADAPTIS unified API (if they ship one modern API across iPay88 + eGHL, mid-market developers may stop recommending Billplz).
- Recurring / e-mandate under the new brand.
- MyInvois (every NTT DATA MY merchant will ask).
- Financing / BNPL attachment.

---

## GHL ePayments / eGHL

### Who they are

**GHL Systems Berhad** is a listed Malaysian payments company (terminals, acquiring, TPA). **eGHL** (GHL ePayments) was the online-gateway arm, founded ~2013, ASEAN-focused, PCI Level 1. As of 2025–2026 eGHL is the other half of the ADAPTIS unification with iPay88 under NTT DATA Payment Services. GHL the listed company still exists as a terminal / merchant-acquiring story; the *online checkout* product a founder meets is increasingly ADAPTIS/eGHL/iPay88 depending on the salesperson.

**ICP:** Brick-and-mortar + ecommerce omnichannel (franchises, petrol, F&B chains, hospitality). Weaker than iPay88 on “government collections”, stronger on **physical terminals + unified settlement**.

**Relationship to Lazuar:** **Upstream rail / substitute.** Same bucket as iPay88. Lazuar will almost never win a GHL terminal merchant on checkout software; it might win their *online subscription* or *LHDN* if the chain’s ecommerce team is separate from the acquiring team.

### Pricing model

**Unpublished.** Same sales-quoted setup + annual + MDR family as iPay88. Older comparison blogs put eGHL annual from ~RM 450. Do not use those numbers in a customer-facing comparison.

### Feature list

Online gateway (cards, FPX, wallets, Alipay historically), terminals, unified omnichannel reporting, WooCommerce/shopping-cart plugins, virtual terminal. Recurring exists in the enterprise sense (token billing), not as a self-serve billing OS.

### Strengths versus Lazuar

- Physical acceptance footprint and listed-company trust.
- Omnichannel reconciliation — the actual pain of a 40-outlet retailer.
- Now backed by the same NTT DATA motion as iPay88.

### Weaknesses versus Lazuar

- Not a developer product. Not a compliance product. Not BYOK software.
- Brand is being absorbed; documentation rot risk.
- Slow, sales-led, contract-shaped.

### Rival type

**Upstream rail + substitute (omnichannel retail).** Not a direct CaaS rival.

### Features Lazuar should track

- Whether ADAPTIS kills the eGHL API or keeps it in parallel (integration risk for any future BYOK connector).
- Unified online+offline reporting APIs (if they open them, commerce OS vendors will consume them and skip Lazuar).

---

## Curlec / Razorpay Malaysia

### Who they are

Curlec was founded in Kuala Lumpur in **2018** as a **Direct Debit / e-mandate** specialist. **Razorpay acquired a majority stake in 2022** (Razorpay’s first international move). By 2023–2026 it is a full-stack MY gateway still going to market as **Razorpay Curlec**. BNM-regulated merchant acquirer, PayNet member, PCI DSS Level 1 (Airwallex Curlec review, 13 April 2026, citing Curlec). Official pricing: [https://curlec.com/pricing/](https://curlec.com/pricing/).

**ICP:** The exact ICP Lazuar wants for Pillar-adjacent recurring — gyms, tuition, insurance-adjacent, rental (Rentguard case study), professional-services retainers, B2B SaaS billing Malaysian companies, anyone migrating paper mandates to e-mandates. Secondary ICP: SMEs who want payment links / pages / buttons without building a site.

**Relationship to Lazuar:** **The most dangerous direct rival on the recurring-billing half of the JTBD**, and simultaneously a **perfect BYOK partner** on the acquiring half. If Lazuar’s dunning + ledger + LHDN sit *on top of* Curlec e-mandates, both win. If Curlec ships a good enough subscription portal + invoices, Lazuar never gets the meeting.

### Pricing model

**Take-rate + optional setup.** Official pricing page (fetched 16 August 2026). Page hero says “from 1.5% per transaction” and a leftover “18% GST applicable” which is an India-template leftover — Malaysian SST is 8%; do not quote 18% GST to customers.

| | Basic | Premium | Enterprise (> RM 1M/mo) |
|---|---|---|---|
| Setup | **RM 0** | **RM 999** | Custom |
| Annual | None | None | Custom |
| Domestic cards | **2.40%** | **2.00%** | Custom |
| Foreign cards | **3.30%** | **3.10%** | Custom |
| FPX | **1.50% or RM 1, whichever greater** | **1.00% or RM 1, whichever greater** | Custom |
| BNPL (Atome) | **6.00%** | **5.00%** | Custom |
| TnG / Boost | **1.50%** | **1.30%** | Custom |
| GrabPay | **1.50%** | **1.50%** | Custom |

Bundled free with the gateway: Payment Links, Payment Pages, Payment Buttons, Invoices.

FAQ on the same page contradicts the table (“minimal setup fee of RM99 for Basic”) — another India-template leftover. **Trust the table (RM 0 Basic setup), flag the FAQ as dirty copy.**

Versus Billplz: Curlec FPX is *percentage with RM 1 floor*, so a RM 20 payment is RM 1 (same as CHIP) but a RM 200 payment is RM 3 on Basic (worse than Billplz’s RM 1.25 / RM 0.75). Curlec is priced for **higher tickets and subscriptions**, not for RM 15 digital goods.

Onboarding: 1–2 business days, SSM required, no unregistered sole props (Airwallex review, 13 Apr 2026). Settlement T+2.

### Feature list

- **FPX e-mandate / Direct Debit** — the category-defining product. RM 1 test authorisation, then merchant-initiated pulls. Batch upload of paper mandates. Subscriber dashboard.
- **Card-on-file recurring** alongside DD.
- Billing models (Airwallex review citing Curlec): fixed schedule, quantity-based, usage-based; trials, upfront charges, add-ons, upgrades/downgrades; **automated retries on failure**.
- Payment Links / Pages / Buttons / Invoices (no extra fee).
- Flash Checkout against “4 million+ saved cards” (vendor claim via Airwallex review).
- REST APIs, SDKs, Shopify + WooCommerce plugins.
- Payouts to MY bank accounts.
- Xero integration (Cheng & Co testimonial on the pricing page).
- **Not** a multi-currency account product. MYR settlement only.

Xendit’s 10 Aug 2026 guide’s knock on Curlec: e-wallet mandate support is limited vs Xendit; no SEA multi-market under one API; Razorpay’s centre of gravity is India.

### Strengths versus Lazuar

- **They own the best local recurring rail.** Lazuar cannot invent FPX e-mandate; only a PayNet participant can. Curlec *is* that participant with the most years on the problem.
- **Retries + subscriber dashboard + mandate migration** are years ahead of “email a new Billplz link”.
- **Same ICP stories Lazuar wants** (Rentguard, Union Strength, Funding Societies, tuition, gyms).
- **Xero sync** already in the wild — they are walking toward the accountant.
- **Razorpay capital and India playbook** (subscriptions, route, smart collect) can be ported.

### Weaknesses versus Lazuar

- **They are the acquirer.** A merchant with an existing Stripe + Billplz setup must *migrate* onto Curlec to get e-mandate. Lazuar can, in principle, orchestrate Curlec *or* CHIP *or* Xendit depending on the tenant’s keys.
- **No LHDN product.** Recurring tuition centres are exactly the businesses drowning in MyInvois. Curlec collects the money and leaves the XML to Xero/the accountant.
- **No double-entry merchant ledger as a product.** Processor reporting ≠ books.
- **FPX one-time pricing is not cheap** at mid tickets (1.5% vs Billplz flat).
- **MY-only.** Razorpay India does not help a MY SaaS expanding to Jakarta.
- **E-wallet recurring is the hole Xendit attacks.**
- **Copy quality / FAQ contradictions** are a smell that the MY site is a port.

### Rival type

**Direct rival on subscriptions/dunning. Upstream rail + partner on e-mandate. Substitute for “just use Curlec, they do recurring”.** This is the name Lazuar loses the *billing* deal to.

### Features Lazuar should track

- Dunning: channels (email vs WhatsApp), retry calendar vs salary dates, grace periods, customer self-serve update-mandate.
- Usage-based + seat-based billing completeness vs Stripe Billing.
- Any MyInvois / e-invoice.
- E-wallet mandates.
- Razorpay Route-style marketplace splits.
- Whether they allow **partners to drive e-mandate via API on a sub-account** (this is how Lazuar should integrate — as a platform — rather than forcing merchants to leave).

---

## Revenue Monster

### Who they are

Revenue Monster is a Malaysian unified-commerce / payment + loyalty + omnichannel vendor. Official site: [https://revenuemonster.my/](https://revenuemonster.my/). HQ Malaysia. Stage: SME-to-mid-market local. Positioning: payment gateway + QR + smart terminals + loyalty / membership / WhatsApp mini-programs / white-label / `alacarte.my` store.

**ICP:** F&B, retail, mid-size chains that want **online + in-store + loyalty + a lightweight store** from one vendor. Not indie SaaS. Not government collections.

**Relationship to Lazuar:** **Substitute (commerce OS with payments)** more than a checkout CaaS. Overlaps StoreHub more than it overlaps Stripe. Could be a BYOK connector (they have APIs and SDKs) but the merchant who is on RM is usually buying a suite, not keys.

### Pricing model

**Setup + MDR (unpublished MDR) + optional terminal rental.** Official pricing page (fetched 16 August 2026):

- **Advanced Plan:** **RM 499 one-time** setup. Extra settlement account RM 99 one-time. Terminal: **RM 50/month + RM 300 deposit**, or buy-off RM 1,300; SIM RM 120/year optional. Training: 1 session.
- **Corporate+:** personalised. Merchant wallet, membership, custom workflows, 2 training sessions.

MDR for cards / FPX / wallets is **not printed**. FAQ says “competitive rates”. Model: hybrid SaaS-ish setup + take-rate + hardware.

### Feature list

Online: FPX, local cards, e-wallets (TnG, MAE, ShopeePay, GrabPay, Boost, S Pay Global, M Cash, Setel, Alipay, WeChat Pay), BNPL (Grab PayLater, Atome), DuitNow QR, **LivePay**, **e-Invoice Payment Links**, **Recurring Payments**, All-in-One Merchant Portal App, `alacarte.my` store (limited-time offer), APIs, iOS/Android SDKs, plugins, Payment Link.

Corporate+: merchant wallet (cash balance), membership program, custom workflows & reporting.

White-label payment gateway is marketed on the homepage.

### Strengths versus Lazuar

- **Omnichannel + loyalty** in one SKU. A restaurant does not want Lazuar.
- **“e-Invoice Payment Links”** — the words are on the pricing page. If this is MyInvois-connected, they have combined two of Lazuar’s pillars inside a F&B suite.
- **White-label gateway** — a different ICP (other SaaS embedding RM).
- Hardware + app distribution.

### Weaknesses versus Lazuar

- **RM 499 setup, unpublished MDR, hardware gravity.** Wrong motion for a headless SaaS founder.
- **Developer brand is weak** versus CHIP/Xendit/Curlec.
- **Recurring is a checkbox**, not a public billing-OS story.
- **Suite bloat.** WhatsApp mini-programs and gamification are the opposite of Lazuar’s “kill vitamins” ADR.

### Rival type

**Substitute** (all-in-one merchant suite). **Occasional upstream rail** for F&B tenants who also have an online membership. Not a partner worth courting for BYOK unless a specific tenant demands it.

### Features Lazuar should track

- e-Invoice Payment Links: MyInvois UUID or just a PDF?
- Recurring + membership: is this gym-style or SaaS-style?
- White-label API (platform threat if they sell “embed us in your SaaS”).

---

## PayNet — DuitNow / FPX (rails, not a product)

### Who they are

**Payments Network Malaysia Sdn Bhd (PayNet)** is the national payments network. Largest shareholder: Bank Negara Malaysia. It operates **FPX**, **DuitNow** (RPP instant credit), **DuitNow QR**, **IBG**, and related rails. Settlement through RENTAS. Messaging on RPP is ISO 20022.

PayNet is **not a merchant product**. Merchants cannot “sign up for PayNet” the way they sign up for Billplz. They sign up with a **PayNet participant**: a bank seller-bank, or a Third-Party Acquirer / merchant acquirer (Billplz, CHIP, Curlec, Fiuu, Xendit-via-Payex, HitPay, etc.).

### Pricing model

PayNet charges participants, not end-merchants. The RM 0.75–RM 1.25 the merchant sees is the TPA’s retail price, which embeds PayNet + buyer-bank + acquirer economics. DuitNow QR MDR at the scheme level is often cited at **0.25%**, frequently **waived for micro/small merchants** (2026 explainers). That waiver is why every kedai kopi has a QR and why wallet-direct acquiring (Boost, TnG) can underprice card.

### Feature list (as rails)

- **FPX B2C / B2B** — redirect online banking, 30+ banks, not recurring.
- **FPX e-mandate** — the standing-order equivalent; only some participants expose it.
- **DuitNow Transfer** — account-to-account instant, proxy (mobile/NRIC/DuitNow ID).
- **DuitNow QR** — national QR, online and offline, cross-border linkages (SG, TH, ID, CN, KH; IN expected 2026).
- **RPP** — real-time retail payments platform underneath DuitNow.

### Strengths versus Lazuar

Irrelevant as a competitor. Strength as **infrastructure**: if PayNet is up, every TPA is up; if PayNet is down, Lazuar’s local checkout is down regardless of how good the dunning email is. CHIP publishes an FPX bank status page for a reason.

### Weaknesses versus Lazuar

PayNet will never ship hosted checkout, subscriptions, or MyInvois. It is a clearing system.

### Rival type

**Upstream rail only.** Never a rival. Never a partner in the commercial sense (Lazuar will not be a PayNet member in the MVP horizon). Lazuar’s dependency is **transitive**: Lazuar → TPA → PayNet.

### Features Lazuar should track

- **FPX e-mandate scheme rules** (consent, revocation, R-transactions). Product must honour bank-side mandate cancellation via gateway webhooks.
- **DuitNow QR online** as a checkout tile — should be a first-class method in the hosted checkout, not an afterthought.
- **Cross-border QR** — tourist and SG/JB corridor. A method tile, not a new product.
- **BNM Technology Requirements (March 2026, effective March 2027)** for payment-service regulatees. These bind the TPA, not Lazuar, but enterprise due diligence will ask Lazuar how it sits relative to the licensed entity.

---

## Boost (merchant acquiring)

### Who they are

Boost is Axiata’s e-wallet and merchant-acquiring face. Consumer app + **Boost Biz**. BNM e-money licensee. Official merchant pricing: [https://myboost.co/business/boost-biz-pricing](https://myboost.co/business/boost-biz-pricing).

**ICP:** Hawkers, F&B, retail, SMEs who want a QR standee and maybe a payment link. Not SaaS. Not developers.

**Relationship to Lazuar:** **Substitute acquirer** for offline and simple online. A social seller can take Boost QR and never need Billplz, CHIP, or Lazuar. **Upstream rail** when a gateway (Billplz, CHIP, Fiuu, RM, HitPay) offers “Boost” as a checkout tile — the merchant is not a Boost Biz merchant; the TPA is.

### Pricing model

**Official (fetched 16 August 2026):**

**Package fees:**

| Item | Fee |
|---|---|
| Boost Biz Activation (QR + materials + portal + settlement report + support) | **RM 100** one-time |
| International QR activation | **RM 20** one-time |
| Biz mPOS | **RM 15 / month / TID** |
| **Biz Payment Link** | **RM 100 / year** |
| Biz Booster | **RM 9.90 / month** |

**MDR:**

| Method | Boost DuitNow QR | International QR | Biz Payment Link |
|---|---|---|---|
| QR | **1.0%** | **1.0%** | **1.0%** |
| Online banking | — | **1.5% or min RM 1** | — |
| Local credit | **1.3%** | **2.5%** | — |
| Local debit | **0.8%** | **2.0%** | — |
| Foreign cards | **3.0%** | **3.0%** | — |
| Boost wallet | — | **1.0%** | — |
| GrabPay, TnG, ShopeePay, MAE, Gkash, MCash | — | **1.5%** | — |
| Alipay, WeChat Pay | — | **3.0%** | — |

### Feature list

DuitNow QR standee, multi-wallet acceptance, payment link (paid annual), mPOS, settlement reports, consumer-app distribution (cashback campaigns drive footfall). Merchant B2B wallet (up to RM 500k for BRN merchants, announced 2023). No developer billing OS. No LHDN. No subscriptions.

### Strengths versus Lazuar

- **Zero-education QR.** The buyer already has Boost or any DuitNow app.
- **Campaign traffic.** Axiata can put the merchant in front of consumers. Lazuar cannot.
- **Cheap debit/QR.** 0.8–1.3% in-store is a till argument, not a SaaS argument.

### Weaknesses versus Lazuar

- **Payment link is RM 100/year and still just a link.**
- **No API culture, no subscriptions, no dunning, no ledger, no MyInvois, no BYOK.**
- Settlement and support are consumer-fintech grade, not SaaS-grade.

### Rival type

**Substitute** for informal / offline collection. **Upstream rail** inside TPAs. Not a partner.

### Features Lazuar should track

- Boost Payment Link gaining recurring or e-invoice (unlikely, but a social-seller steal if it happens).
- Wallet-mandate APIs exposed to TPAs (this is how Xendit/HitPay do Boost recurring, if they do).

---

## Touch ’n Go eWallet (merchant acquiring)

### Who they are

TNG Digital (Touch ’n Go eWallet) is the consumer-wallet incumbent (~26 million verified users is the 2026 circulating figure). Merchant acquiring is **TNG eWallet Business / TNG DuitNow QR**, plus TNG as a *method* inside every serious TPA. Ant Group heritage. GO+ money-market yield is the consumer lock-in.

**ICP:** Same as Boost — offline MSME + large retail (petrol, parking, transit adjacency). Online, TNG is a checkout tile, not a destination.

**Relationship to Lazuar:** **Upstream rail** (must appear as a method on hosted checkout via the TPA). **Substitute** when the merchant says “just scan my TNG QR”.

### Pricing model

TNG’s own merchant MDR is **not consistently public** on a single clean page the way Boost’s is. Through TPAs, TNG-as-method is typically **1.0–1.5%** (CHIP wallets 1.4%; CHIP DuitNow QR 1.0%; Billplz wallets 1.5%; Curlec 1.3–1.5%; Boost’s table lists TnG at 1.5% when accepted via Boost’s international-QR column). Direct TNG merchant deals for chains are sales-quoted and often bundled with brand campaigns.

### Feature list

Consumer wallet, DuitNow QR accept, in-app merchant services, transit/parking/petrol super-app gravity, GO+. Recurring / “auto-reload” exists on the *consumer* side; **merchant-initiated TNG mandates** are a TPA feature (Xendit and HitPay claim them), not a self-serve TNG Billing product.

### Strengths versus Lazuar

- Default wallet for a generation of Malaysians.
- Offline ubiquity.
- Brand campaigns.

### Weaknesses versus Lazuar

- Not a checkout OS. Not a billing OS. Not a compliance OS.
- Online integration without a TPA is not how a SaaS founder wants to live.

### Rival type

**Upstream rail + informal substitute.** Partner only in the sense that Lazuar’s hosted checkout must show the TNG mark via the connected TPA.

### Features Lazuar should track

- TNG merchant-initiated recurring API availability through each BYOK connector.
- TNG e-invoice experiments (large acquirers will be pushed).

---

## GrabPay (merchant acquiring)

### Who they are

GrabPay is Grab’s wallet, BNPL (PayLater), and merchant-acquiring method across SEA. In Malaysia it is a must-have checkout tile and a PayLater conversion tool. Grab’s merchant story is super-app (food, rides, mart) more than “payment gateway for your SaaS”.

**ICP:** F&B and retail already on Grab; ecommerce that wants PayLater; any SEA-minded checkout.

**Relationship to Lazuar:** **Upstream rail** (method + PayLater). **Substitute** only for merchants whose entire business *is* Grab.

### Pricing model

Through TPAs: GrabPay wallet typically **1.4–1.5%** (CHIP 1.4%, Curlec 1.50%, Billplz wallets 1.5%). **Grab PayLater** is **6.0–8.0%** depending on tenor (senangPay 6.0%; Xendit’s own table 6.0–8.0%; HitPay secondary 6.5%). These are official-on-TPA-pages, not Grab.com, except where the TPA prints them.

### Feature list

Wallet checkout, PayLater, in-app Grab merchant tools, SEA coverage (SG/MY/PH/ID/TH/VN to varying degrees). Recurring mandates claimed by Xendit and HitPay. Not a developer billing OS.

### Strengths versus Lazuar

- PayLater conversion on RM 80–RM 800 carts.
- Regional brand. A SG+MY checkout that lacks GrabPay looks broken.
- Super-app distribution for F&B.

### Weaknesses versus Lazuar

- Not trying to be Checkout-as-a-Service for indie SaaS.
- PayLater is a take-rate that would destroy Lazuar’s “we don’t tax GMV” story if Lazuar ever became the acquirer. As BYOK, Lazuar just surfaces the method and the merchant pays Grab via the TPA.

### Rival type

**Upstream rail + BNPL substitute for conversion.** Not a CaaS rival.

### Features Lazuar should track

- GrabPay mandate recurring reliability (empty-wallet failure UX).
- PayLater as a *one-time* checkout option on Lazuar hosted checkout (must-have method tile, not a product line).

---

## HitPay (SG / MY / PH)

### Who they are

HitPay is a Singapore-founded (2016) SMB payments + POS + invoicing company, MAS-licensed (PS20200643), also registered in Malaysia as an approved MSB agent / registered merchant acquirer (their MY footer links BNM directories). Official MY page: [https://hitpayapp.com/my/](https://hitpayapp.com/my/). Vendor claims: 20,000+ businesses, US$1B+ processed, 10+ countries, 99.99% uptime.

**ICP:** SMBs who sell **online + in-person + WhatsApp** — F&B, retail, tuition, gyms, agencies, NGOs. The closest thing in SEA to “Square for Southeast Asia”. Explicitly not an enterprise acquiring RFP shop, and not a Stripe-Billing-for-SaaS shop.

**Relationship to Lazuar:** **The most dangerous direct rival on the “payment link + invoice + simple recurring + Xero sync” job.** They are what a non-technical Malaysian SME actually compares Lazuar to, if they ever see Lazuar. They are also a possible BYOK connector (API exists) but merchants on HitPay think they already *have* a checkout product.

### Pricing model

**Pure take-rate. Zero monthly, zero setup, zero cancellation.** Official MY page (fetched 16 August 2026) prints:

- DuitNow / ShopeePay: **from 1.2%**
- Cards: **1.2% + RM 1** (page hero; Xendit’s April 2026 table says 1.2% + RM 1.00 local, 3.0% + RM 1.00 foreign — consistent with the hero for local)
- Terminals: **RM 310–RM 1,750 one-time**, no monthly rental
- FPX: **not printed on the MY homepage hero**. Xendit’s table (17 Apr 2026) quotes HitPay FPX at **1.8% + RM 0.40**. HitPay’s own “rates” blog (3 Apr 2026) defers to the pricing page. **Treat FPX as “see HitPay pricing page / sales” rather than inventing a number in customer-facing Lazuar material.**
- Airwallex’s 28 Apr 2026 subscription-billing post claims **HitPay adds 0.2% per transaction for recurring billing**. Not verified on the MY homepage. Track it.

Settlement: MY page says “fast payout within 2 days” and “instant settlement notification”. HitPay blogs claim next-business-day on domestic.

### Feature list

Online checkout; POS + card terminals + **Tap to Pay Android MY**; payment links; invoicing with **Xero / QuickBooks / Zoho / NetSuite** sync; recurring billing (plans + self-signup links); static QR; online store; soundbox (SG page); cross-border payments; FX and payouts; REST API; webhooks; sandbox; MCP server for AI tooling; plugins for Shopify, WooCommerce, Wix.

Recurring honesty check (Xendit 10 Aug 2026, which is a competitor but matches HitPay’s own “recurring payment links” language): **FPX recurring is periodic payment links, not e-mandate.** E-wallet recurring (TnG, GrabPay, ShopeePay) and cards: yes. Usage-based / metered: no. Smart retry: weaker than Stripe/Xendit.

### Strengths versus Lazuar

- **The SME “one app” dream.** Link + invoice + POS + QR + recurring + Xero. Lazuar’s ADR 021 *refuses* POS, store, and vitamins. HitPay *is* the vitamin cabinet, and SMEs like vitamins.
- **Zero monthly fee** is an easy sentence. Lazuar’s SaaS + credits model needs a better sentence.
- **Xero sync is live.** Lazuar’s Xero sync is a roadmap item in ADR 021.
- **SG + MY + PH** under one account. JB corridor and SG agencies are native.
- **Onboarding speed and 7-day support** as a brand.
- **Tap to Pay / terminals** steal the omnichannel conversation.

### Weaknesses versus Lazuar

- **They are the acquirer.** No BYOK. A merchant with an existing Fiuu or iPay88 MID cannot “just add HitPay software”.
- **Recurring is SMB-grade**, not SaaS-grade. No e-mandate, no metered billing, no serious dunning OS.
- **No LHDN / MyInvois product** on the pages fetched. Invoices are commercial invoices, not IRBM-validated e-invoices.
- **No double-entry ledger as a product.**
- **Developer story is real but secondary.** The product is the dashboard, not the API.
- **Take-rate on everything.** A high-volume SaaS will outgrow HitPay’s economics and UX.

### Rival type

**Direct rival** for SME hosted checkout, payment links, invoicing, simple subscriptions. **Substitute** for POS-centric merchants. **Not** a partner (they will not happily be a dumb BYOK pipe). **The name Lazuar loses the non-technical SME deal to.**

### Features Lazuar should track

- MyInvois (the moment HitPay ships it, the SME compliance story collapses into HitPay).
- Recurring: any move from payment-link-recurring to e-mandate.
- The alleged 0.2% recurring surcharge.
- Accounting sync depth (Xero tax codes, credit notes, multi-currency).
- API + webhooks + MCP — they are courting the same indie hackers.
- Cross-border QR / multi-currency store.

---

## Xendit (ID origin, regional, MY via Payex)

### Who they are

Xendit is the SEA payments unicorn (ID origin, now ID/PH/MY/TH/VN/SG/HK + LatAm). **Full acquisition of Malaysia’s Payex in 2025**; licensed by BNM **through Payex PLT** for merchant acquiring (Xendit MY pages, 2025–2026). Official MY marketing: [https://www.xendit.co/en-my/](https://www.xendit.co/en-my/). Vendor claims: 15,000+ businesses globally; 99.999% uptime (subscriptions post); 100+ methods.

**ICP:** Venture-backed and would-be-venture-backed SaaS, marketplaces (XenPlatform), regional ecommerce, anyone who says “we’ll expand to Jakarta next year”. Secondary: MY SMEs via plugins.

**Relationship to Lazuar:** **Direct rival on the developer + subscriptions + regional job. Upstream rail + partner on BYOK (explicitly on Lazuar’s Phase 1 gateway list).** This is the name Lazuar loses the *technical founder / SEA expansion* deal to.

### Pricing model

**Take-rate + fixed fee. No setup, no annual** (Xendit’s own claim). Official pricing lives at [https://www.xendit.co/en-my/pricing/](https://www.xendit.co/en-my/pricing/) (structure: processing fee + method fee). The 17 April 2026 Xendit blog prints a MY table that is the most complete public Xendit-MY card in circulation:

| Method | Xendit’s published blog figures (17 Apr 2026) |
|---|---|
| DuitNow QR | RM 1.20 + RM 0.90 |
| FPX B2C | RM 1.20 + RM 0.90 |
| FPX B2B | RM 2.00 + RM 0.90 |
| Local credit | 2% + RM 0.90 |
| Local debit | 1.9% + RM 0.90 |
| Foreign cards | 3.8% + RM 0.90 |
| Wallets | 1.8–2.5% + (processing fee context) |
| Grab PayLater | 6.0–8.0% |
| SPayLater | 2.50% |

The same post’s later “how Xendit helps” bullets quote **FPX B2C RM 1.20, B2B RM 2.00, local credit 2.00%, local debit 1.20%** *without* restating the RM 0.90 processing fee. **There is an internal inconsistency on Xendit’s own blog.** For any customer-facing Lazuar comparison, open the live pricing page or get a quote. Directionally: Xendit is **more expensive than Billplz/CHIP on FPX** (especially with the RM 0.90) and in the same band as Curlec/Fiuu on cards.

### Feature list

Payments (100+ SEA methods); payment links; **Subscriptions** (cards, FPX e-mandate, TnG / GrabPay / ShopeePay mandates — Xendit’s 10 Aug 2026 claim); instalments; batch + automated payouts; fraud detection; **XenPlatform** (marketplace sub-accounts); invoicing via API & dashboard; plugins (WooCommerce, Shopify, EasyStore); SDKs; sandbox; 24/7 support.

Subscription feature claims (10 Aug 2026): daily/weekly/monthly/annual/custom; **usage-based via Update Cycle API**; configurable retry; webhooks on success/fail/cycle/cancel; grace period; auto-debit activation via dashboard (cards recurring request, 3-working-day SLA).

### Strengths versus Lazuar

- **One integration, six SEA countries.** This is the slide that kills Lazuar in a regional seed-deck meeting.
- **Complete MY recurring rails (as claimed):** e-mandate + cards + three wallet mandates. If true on a given account, this is strictly more rail coverage than Curlec or HitPay.
- **XenPlatform** is a marketplace product Lazuar has explicitly refused (ADR vitamins).
- **Developer brand.** Docs, status, Slack community folklore, “it just works in ID”.
- **Payouts 24/7** — the other half of a marketplace.
- **They already have the BNM licence via Payex.** Enterprise legal is easier than “our software posts to your Billplz keys”.

### Weaknesses versus Lazuar

- **Price on local FPX.** A tuition centre taking RM 80 fees will not pay RM 1.20 + RM 0.90 when Billplz is RM 0.75.
- **They are the acquirer.** BYOK inversion. A merchant who already has Stripe for foreign cards and Billplz for FPX does not want to rip both out for Xendit.
- **No LHDN product** on the pages fetched. Regional unicorns are late to country-specific tax XML. This is Lazuar’s opening.
- **No merchant double-entry ledger as a product.**
- **Subscription depth is gateway-grade**, not Chargebee-grade. Usage-based is an API to update a cycle amount, not a metering system with credits, minimums, and overage invoices that hit MyInvois.
- **MY is a 2025 landing, not a 2015 home market.** Support and FPX edge-cases will have ID-shaped instincts for a while.

### Rival type

**Direct rival** (developer CaaS + subscriptions + regional). **Upstream rail + partner** (BYOK). **The name Lazuar loses the technical / regional deal to.**

### Features Lazuar should track

- Whether FPX e-mandate is generally available or sales-gated in MY.
- Wallet-mandate failure UX and retry.
- Any MyInvois / e-faktur / PEPPOL talk (the day they hire a MY tax PM, the window narrows).
- XenPlatform fee schedule (platform threat).
- Invoicing product vs just payment links.

---

## Midtrans (Indonesia)

### Who they are

Midtrans is the Indonesian payment gateway, now part of **GoTo**. Official: [https://midtrans.com/](https://midtrans.com/). Founded 2012, Jakarta. Stage: scaled ID incumbent. IDR only. Not a Malaysian acquirer.

**ICP:** Indonesian enterprises and PT PMA subsidiaries that need **every ID method** (VA, QRIS, GoPay, ShopeePay, Indomaret/Alfamart OTC, cards, Akulaku, bank debit).

**Relationship to Lazuar:** **Not a MY rival.** **Regional substitute** when a Lazuar merchant opens an ID entity and someone says “just use Midtrans, everyone does”. **Not a partner** for MY. A possible *future* BYOK connector if Lazuar ever sells to ID entities (Phase 2+).

### Pricing model

**Take-rate, no setup, no monthly** (Midtrans FAQ). Official pricing page (crawled Aug 2026; VAT extra except some e-wallets):

| Method | Official / listed |
|---|---|
| Bank transfer / VA | **Rp 4,000** / txn |
| GoPay | **2%** |
| QRIS | **0.7%** |
| ShopeePay | **1.5%** |
| Cards | **2.9% + Rp 2,000** |
| OTC (Indomaret / Alfa) | **Rp 5,000** |
| Direct debit (Octo, e-pay BRI, Danamon) | **Rp 5,000** |
| BCA KlikPay | **Rp 2,200 + BCA fee** |
| Akulaku | **1.7%** |

### Feature list

Snap checkout, payment links / pay-via-chat, subscriptions / recurring (fixed amount, saved credentials), fraud detection, MAP dashboard, plugins, core API. Recurring page describes automatic charges on an interval — **card/token and selected ID methods**, not a Malaysian e-mandate.

### Strengths versus Lazuar

- **ID method completeness + GoTo/GoPay gravity.** If the customer is Indonesian, Midtrans converts.
- **Incumbent trust in Jakarta.**

### Weaknesses versus Lazuar

- **IDR / Indonesia only.** Useless for a MY-incorporated ICP.
- **No LHDN, no FPX, no MY e-mandate.**
- Recurring is not a billing OS.
- They hold the money in ID.

### Rival type

**Regional substitute / future upstream rail for ID.** Not a current direct rival. Do not build Midtrans integration before a real ID tenant exists.

### Features Lazuar should track

- GoTo’s regional ambitions (if Midtrans ever follows Xendit’s multi-country path).
- Indonesian e-faktur / Coretax features (relevant only if Lazuar’s ADR 021 India/ID tax pillar wakes up).

---

## PayMongo (Philippines)

### Who they are

PayMongo is the Philippine SMB/developer gateway. Stripe led their Series A (2020). Official: [https://www.paymongo.com/](https://www.paymongo.com/). HQ PH. Stage: scaled PH fintech.

**ICP:** Philippine SMEs, Shopify/Woo merchants, and PH SaaS. Methods: cards, GCash, Maya, GrabPay, QR Ph, online banking (BPI, UBP), some OTC via partners.

**Relationship to Lazuar:** **Regional analogue, not a MY rival.** Useful as a **product-mirror**: PayMongo Links + Subscriptions + “retries on failed payments” is the PH version of the job. If Lazuar expands to PH, PayMongo is the Billplz *and* the Curlec.

### Pricing model

**Take-rate, no setup, no monthly** on Standard (official pricing page). All prices exclusive of VAT. Custom for large volume. Exact method rates move; secondary 2025–2026 snapshots (HitPay comparison Aug 2025; Wise comparison) put local cards around **3.1–3.5% + ₱13–15**, GCash ~2.5%, Maya ~2.0%, GrabPay ~2.2%, QR Ph ~1.5%, online banking 0.7–0.8% or ₱15. **Confirm on paymongo.com/pricing before quoting.**

A **₱349/month** line also appears on the pricing page crawl (plus credit packs) — likely a software add-on (invoicing/POS or similar), not the gateway itself. Flag as “PayMongo now has a SaaS SKU; inspect before claiming they are pure-MDR”.

### Feature list

Payments API, PayMongo Links, invoicing, subscriptions (cards + Maya; official subscriptions page: plans via API, retries on failed payments), fraud tools, plugins. Dunning is “retries + webhooks”, not a campaign builder (secondary comparisons vs Xendit).

### Strengths versus Lazuar

- In PH: local methods + Stripe-ish DX + links.
- Subscriptions exist as a named product.

### Weaknesses versus Lazuar

- PH only (for this dossier’s ICP).
- No MY rails, no LHDN.
- Subscription/dunning depth is gateway-grade.

### Rival type

**Regional substitute / future upstream rail for PH.** Not a current MY rival. Study their subscriptions API as a **design reference**, do not integrate until a PH tenant exists.

### Features Lazuar should track

- Subscriptions: Maya + cards only, or GCash mandates next?
- Any e-invoicing (BIR) — the PH analogue of Lazuar’s LHDN bet.

---

## 2C2P (by Antom)

### Who they are

2C2P is the Bangkok-born regional enterprise payments platform, now **2C2P by Antom** (Ant Group / Alipay+ family). Official: [https://2c2p.com/](https://2c2p.com/). Positioning: 400+ methods, online + offline, OTC at 600,000+ locations, issuing, 3DS, bill payments, digital goods. Malaysia country page: [https://2c2p.com/countries/malaysia/](https://2c2p.com/countries/malaysia/).

**ICP:** Airlines (Malaysia Airlines, Capital A logos on the MY page), hotels (Minor), marketplaces (Lazada), IATA, large retail, anyone who needs **IPP 3–36 months**, **multi-currency pricing**, and a single regional integration. Not SMEs. Not indie SaaS.

**Relationship to Lazuar:** **Upstream rail for enterprises + substitute for “regional payments RFP”.** Not a partner for self-serve BYOK. Not a billing-OS rival.

### Pricing model

**Unpublished.** Sales-quoted, enterprise MSA. Older blogs quoting 2.5–3% cards / RM 1.50 FPX are unusable in 2026. Do not invent a 2C2P price.

### Feature list (MY page, fetched 16 August 2026)

Accept payments (server-to-server API, mobile SDKs, plugins); **IPP 3–36 months, 0% interest, bank takes repayment risk, merchant gets full amount upfront**; **multi-currency pricing** with locked FX, refund at original FX; methods: cards (Visa, MC, Amex, Diners, Discover, UnionPay, JCB), BNPL, wallets; local support; consolidated reporting.

### Strengths versus Lazuar

- **Enterprise + airline + hotel proof.**
- **IPP as a real product**, not a checkbox — this is how MY big-ticket ecommerce converts.
- **MCP / FX** for regional storefronts.
- **Antom / Alipay+** distribution.

### Weaknesses versus Lazuar

- Unreachable for the ICP that signs up with a credit card and a GitHub repo.
- No public subscription/dunning/LHDN/ledger story.
- Sales-led, long integration, MSA.

### Rival type

**Upstream rail (enterprise regional) + substitute.** Never a self-serve rival. Integrate as BYOK only if a specific enterprise tenant brings a 2C2P MID.

### Features Lazuar should track

- Whether Antom productises a **subscription** layer on top of 2C2P (Alipay has plenty of recurring in CN).
- MCP APIs (if Lazuar ever does Pillar 3 properly, displaying FX-locked prices is a checkout feature).

---

## Airwallex

### Who they are

Airwallex is the global business-account + payments + spend platform (Australia origin). In Malaysia: **Airwallex (Malaysia) Sdn Bhd, BNM MSB Class B (remittance)**, licence 00318 (their own legal footer). They also acquire / collect. Positioning: multi-currency accounts, local collection, payouts, cards, spend, and now **subscription management**.

**ICP:** Cross-border SMEs and scale-ups — exporters, agencies with SG/US clients, ecommerce that buys ads in USD and sells in MYR, anyone who hates bank FX. Not a kedai runcit. Not a zakat counter.

**Relationship to Lazuar:** **Direct rival on “collect + subscriptions + multi-currency” for the cross-border ICP. Substitute for treasury. Not a LHDN product. Possible partner only in the weak sense of “we sync payouts”.**

### Pricing model

**Take-rate + subscription-management fee. No setup, no monthly in MY** (Airwallex MY blogs, 2026; transferfees.io Aug 2026 read of the MY pricing page: no plan tiers in MY).

Public figures (Airwallex MY blogs 8 Jun 2026, 10 Apr 2026, 28 Apr 2026; World First review 28 Jul 2026):

| Item | Public figure |
|---|---|
| Domestic cards | **1.90% + RM 0.50** |
| Local payment methods | **from 1.4% + RM 0.50** |
| International cards | **~2.95% + RM 0.50** or **+1.05%** on top of domestic (sources differ — confirm on live MY pricing page) |
| Subscription management | **0.50% per successful card transaction** on top of processing |
| FX markup | **0.4%** major / **1.0%** others (MY entity; transferfees.io Aug 2026) |
| Local transfers | Free (claimed) |
| SWIFT | RM 30–90 (secondary) |

Effective domestic card + billing ≈ **2.40% + RM 0.50** plus the 0.50% subscription fee ≈ **~2.90% + RM 0.50** on card subscriptions (Xendit 10 Aug 2026 arithmetic). Still often cheaper than Stripe MY (3% + RM 1 + 0.7% Billing).

### Feature list

Global Accounts (hold 20+ / collect 130+ currencies — marketing figures move); 160+ methods including FPX, DuitNow, TnG, GrabPay (claimed); hosted checkout; no-code subscriptions (flat, per-unit, tiered; trials; proration); smart retries + reminders (blog, 6 Aug 2026); spend cards; payouts; Xero-class accounting adjacency. **No FPX e-mandate** (Xendit 10 Aug 2026). E-wallet recurring limited vs Xendit.

### Strengths versus Lazuar

- **Treasury + collect in one.** Lazuar will never hold 20 currencies. ADR 019 forbids becoming the money.
- **Cross-border ICP** is Airwallex’s home.
- **Card pricing better than Stripe MY.**
- **No-code subscriptions** for finance teams who will not wait for engineering.
- **Brand and content machine.** They publish the comparison posts everyone else ranks for.

### Weaknesses versus Lazuar

- **Licence is MSB remittance, not a full MY merchant-acquirer story in the Billplz sense.** Some methods will be partner-routed. Enterprise legal will ask.
- **No LHDN.** They write e-invoice *blogs*; they do not submit UBL 2.1 for your checkout.
- **No BYOK.** You become an Airwallex merchant.
- **Subscription fee is card-centric.** MY subscribers on FPX/wallets are not the product.
- **No WhatsApp dunning, no MY e-mandate, no double-entry merchant ledger as a tax product.**

### Rival type

**Direct rival** for cross-border SaaS/agencies. **Substitute** for FX/treasury. **Not** a partner for BYOK. **The name Lazuar loses the “we invoice in USD and SGD” deal to.**

### Features Lazuar should track

- MY method list (is FPX first-class or partner?).
- Subscriptions: non-card rails, dunning channels, usage-based.
- Any MyInvois (their content team is already on Peppol/e-invoice — product may follow).
- Whether they allow platform / connected-account structures (Stripe Connect analogue).

---

## EasyStore

### Who they are

EasyStore is the Malaysian Shopify-analogue. Cloud ecommerce OS, MY-first, plugins for every local gateway, and — critically — **built-in e-invoicing** marketed as “no extra cost” (EasyStore blog on the 2024 mandate). Pricing secondary (storestarter.co 20 Apr 2026): Standard RM 249 / Business RM 499 / Growth RM 899 / Success RM 1,199 per month — **confirm on easystore.co before quoting**. They also offer **EasyStore Payments** at 0% *platform* take-rate (gateway MDR still applies).

**ICP:** Malaysian DTC brands who want a store in BM/EN, local couriers, local gateways, and do not want Shopify’s third-party surcharge.

**Relationship to Lazuar:** **Substitute stack.** The merchant’s “Buy” button lives on EasyStore. Payments are a plugin. e-Invoice is EasyStore’s. There is no room for Lazuar hosted checkout unless EasyStore is used as a catalogue and checkout is delegated (they will not want that).

### Pricing model

**SaaS subscription + gateway MDR (not EasyStore’s).** No extra platform % if using EasyStore Payments (their claim). Plans are monthly SaaS.

### Feature list

Storefront, themes, products, coupons, subscriptions-as-ecommerce-feature (varies by app), all major MY gateways (Billplz, senangPay, Fiuu, Revenue Monster, HitPay, Stripe, PayPal, iPay88, ToyyibPay, CHIP, etc. — see EasyStore’s own comparison article, updated 24 Apr 2026), logistics, **e-Invoice automation**.

### Strengths versus Lazuar

- They own the store. Checkout is a feature, not a destination.
- e-Invoice at the order object — the right place for DTC.
- Local support, BM, courier integrations.

### Weaknesses versus Lazuar

- Not a billing OS for SaaS. Not headless by ideology (they will have APIs; they sell a store).
- Subscriptions are ecommerce subscriptions (boxes, memberships), not seat-based SaaS.
- A custom Next.js SaaS cannot “be an EasyStore”.

### Rival type

**Substitute (commerce OS).** Partner only if EasyStore ever opens a “delegate checkout to URL” that Lazuar could fill (do not bet on it).

### Features Lazuar should track

- EasyStore e-Invoice: consolidated B2C, buyer-requested individual, credit notes — this is the UX bar for DTC.
- EasyStore Subscriptions depth.
- EasyStore Payments becoming an acquirer (if they take MDR themselves, they become Fiuu-shaped).

---

## StoreHub

### Who they are

StoreHub Sdn Bhd (1072290-D) is the leading MY all-in-one **POS + ecommerce + loyalty** platform for F&B, retail, service. Vendor claim: 18,000–20,000+ businesses SEA. Official: storehub.com. **e-Invoicing is included in the standard plan at no extra cost**; merchant appoints StoreHub as **MyInvois intermediary** via MyTax (care.storehub.com article linked from their 5 Nov 2024 blog, still live 16 Aug 2026). Accounting sync: QBO, Financio, SQL.

**ICP:** Restaurants, retailers, salons, service shops. Opposite of Lazuar’s headless SaaS ICP — except when a StoreHub merchant also sells online memberships or wants a better online checkout than StoreHub’s.

### Pricing model

**SaaS + hardware.** e-Invoice API “no extra cost vs accounting tools that charge for API” (their claim). Payments via integrated partners (MDR extra).

### Feature list

POS, KDS, inventory, loyalty, staff, omnichannel store, integrated payments, **LHDN e-Invoice automated including monthly submissions**, accounting exports. Not a developer billing OS.

### Strengths versus Lazuar

- **They already won the offline SME compliance job.** A restaurant will never install Lazuar to file MyInvois.
- e-Invoice “set up once, runs automatically, monthly submissions included” is the exact sentence Lazuar wants to say to *online* merchants.
- 20k-business distribution.

### Weaknesses versus Lazuar

- Wrong surface (till, not hosted checkout for a SaaS).
- Not BYOK orchestration, not dunning OS, not developer API-first.
- Headless SaaS founders will not adopt a restaurant POS.

### Rival type

**Substitute** for F&B/retail compliance+payments. **Design reference** for “e-invoice just works”. **Not a partner.**

### Features Lazuar should track

- StoreHub e-Invoice field mapping and consolidated-invoice behaviour (copy the good parts).
- Any StoreHub move into **online memberships / SaaS-like recurring** (then they become a HitPay/Curlec hybrid).

---

## Shopify Malaysia

### Who they are

Shopify is the global commerce OS. **Shopify Payments is not available in Malaysia** as of 2026 (Airwallex 28 Apr 2026; Shopify’s own supported-countries list; Xendit’s 10 Aug 2026 Shopify-MY post). Every MY Shopify store uses a **third-party gateway** and therefore pays **Shopify’s third-party transaction fee on top of gateway MDR**.

**ICP:** DTC brands who want the Shopify app ecosystem, themes, and “we might sell to the US later”.

**Relationship to Lazuar:** **Substitute.** The Buy button is Shopify Checkout. Lazuar can only appear as (a) a custom payment app / hosted-payment-sdk if Shopify allows it, or (b) a post-purchase compliance/dunning layer that Shopify does not do. (b) is the only honest wedge: **Shopify does not submit MyInvois.**

### Pricing model

Shopify subscription (USD, well known) **+ third-party fee** because Payments is off:

| Shopify plan | Third-party payment fee (Airwallex 28 Apr 2026) |
|---|---|
| Basic | **2%** |
| Grow | **1%** |
| Advanced | **0.6%** |
| Plus | **0.2%** |

Plus gateway MDR (Billplz, Fiuu, HitPay, Stripe, etc.). Billplz’s own pricing adds **+0.3%** on its Shopify plugin. A Basic + Billplz Basic card sale can be 2% + 1.8% + 0.3% = **4.1%** before SST. This is why serious MY Shopify brands jump to Plus or leave for EasyStore.

### Feature list

Full commerce OS, Shopify Subscriptions apps, Shop Pay (not on MY Payments), thousands of apps including e-invoice middleware (Sufio, JomeInvoice, etc.).

### Strengths versus Lazuar

- They own checkout for a class of merchant Lazuar will not pry off.
- App store lets someone else build MyInvois (and they have).

### Weaknesses versus Lazuar

- **Punitive third-party fee in MY.**
- No native FPX-quality checkout without a gateway app.
- No native LHDN.
- Subscriptions are app-shaped, not ledger-shaped.

### Rival type

**Substitute (commerce OS).** Partner only as “Shopify app that is actually a CaaS + MyInvois layer” — a possible distribution play, not MVP.

### Features Lazuar should track

- Any rumour of **Shopify Payments MY** (would change the fee math and entrench Shopify).
- Quality of MY e-invoice apps (the bar for a Lazuar Shopify app).
- Shopify Subscriptions + local-gateway limitations (e-mandate usually missing).

---

## WooCommerce + Billplz / CHIP plugins

### Who they are

Not a company. A **stack**. WordPress + WooCommerce + the official or community plugin for Billplz, CHIP, ToyyibPay, senangPay, Fiuu, iPay88, Curlec, HitPay, Xendit, Stripe. Plus, optionally, **WooCommerce Subscriptions** (Woo.com paid plugin) or Sumo / YITH.

This is still the default for Malaysian agencies building a “website + shop” for RM 3k–RM 15k.

**Relationship to Lazuar:** **The incumbent substitute for “plug checkout into my own site”.** Lazuar’s entire headless thesis is a better version of this stack. If Lazuar is not easier than “install CHIP plugin + Woo Subscriptions”, the agency will not switch.

### Pricing model

WooCommerce core is free. WordPress hosting RM 20–200/mo. WooCommerce Subscriptions is a paid extension (USD, Woo.com). Gateway MDR as per the chosen TPA. Agency maintenance retainers are the hidden SaaS fee.

### Feature list

Whatever the plugin authors implemented. Typically: redirect or hosted-field checkout, callback/webhook to mark the order paid, refund buttons of varying quality. Recurring = Woo Subscriptions’ scheduler + gateway’s token or (more often) **a new FPX redirect every cycle**. Dunning = Woo Subscriptions emails. LHDN = a third plugin or the accountant.

### Strengths versus Lazuar

- **Infinite agency supply.** Every Malaysian web designer can install a plugin.
- **Total control** of the site.
- **Cheap** until it breaks.

### Weaknesses versus Lazuar

- **Recurring on FPX is a lie** unless the gateway plugin implemented e-mandate (almost none have).
- **No ledger.** Woo’s commerce tables are not double-entry.
- **No MyInvois** unless another plugin is added, and then you have two sources of truth.
- **Webhook/callback bugs** are a support genre. Lazuar’s job is to make this someone else’s problem.
- **PDPA + PCI** landmines when agencies “just log the response”.

### Rival type

**Substitute stack. The thing Lazuar replaces for custom sites and SaaS.** Not a partner. Plugins for Woo that *delegate* to Lazuar hosted checkout are a distribution idea (Phase 2).

### Features Lazuar should track

- CHIP and Curlec Woo plugins gaining **native e-mandate / card MIT**.
- WooCommerce / WordPress e-invoice plugins quality.
- High-Level / other page-builders adding MY gateways (adjacent substitute).

---

## LHDN MyInvois portal itself

### Who they are

**MyInvois** is IRBM / LHDN’s official e-invoicing platform. Portal: [https://myinvois.hasil.gov.my/](https://myinvois.hasil.gov.my/) (login via MyTax). It is the **system of record** for validated e-invoices in Malaysia. Two access modes: portal (manual) and API (direct or via intermediary).

**ICP:** Every Malaysian taxpayer in the mandate. The portal ICP is specifically **low-volume** businesses and anyone whose software is not ready.

**Relationship to Lazuar:** **Upstream rail for compliance** (the way PayNet is the rail for payments). **Substitute** for low volume (the merchant just types invoices into the portal and never buys Lazuar). **Never a rival** in the product sense — LHDN does not collect money and will not build dunning.

### Pricing model

**Free.** The government’s price is compliance risk, not RM.

### Feature list

Issue, validate, cancel, view, search e-invoices; consolidated invoices; self-billed invoices; credit/debit/refund notes; QR / UUID for buyer verification; taxpayer TIN lookup. Not an API product for mortals (the API exists; intermediaries wrap it). Not a checkout. Not a ledger.

### Strengths versus Lazuar

- **Free and official.** A 20-invoice-a-month consultant does not need Lazuar.
- **Source of truth.** If MyInvois says the invoice is valid, it is valid.

### Weaknesses versus Lazuar

- **Unusable at SaaS volume.** 55 fields × thousands of B2C checkouts is a joke.
- **No payment.** An invoice in MyInvois does not collect RM.
- **No dunning, no subscriptions, no hosted checkout, no webhooks into a SaaS.**
- **Consolidated B2C is a monthly chore** someone has to remember. Lazuar’s `B2cConsolidationJob` exists because this chore is where SMEs fail.

### Rival type

**Upstream compliance rail + low-volume substitute.** Partner in the legal sense (Lazuar is an intermediary / API client). The portal is what Lazuar must make obsolete for its ICP without ever fighting LHDN.

### Features Lazuar should track

- Schema versions (UBL 2.1 updates, new mandatory fields).
- Thresholds and consolidated-invoice policy changes (they have moved in 2025–2026).
- Intermediary appointment UX on MyTax (StoreHub already documents this; Lazuar must too).
- TIN validation API behaviour (Pillar 2 checkout).
- Rate limits and sandbox quirks (already in lazuar-pay `scripts/lhdn_sandbox`).

---

## StoreHub e-Invoice (compliance-adjacent, same firm)

Covered under StoreHub above. Restating the competitive point so it is not lost:

StoreHub is the **existence proof** that Malaysian SMEs will adopt e-invoice *if it is free, automatic, and attached to the system they already tap all day*. They will not adopt a standalone “e-invoice SaaS”. Lazuar’s implication: **e-invoice must ride along with checkout, not be a separate SKU the merchant has to remember.** ADR 023 hiding the LHDN UI is correct for Phase C; it is dangerous if merchants cannot *see* that compliance is happening. StoreHub’s “appoint us as intermediary, then forget” is the UX to copy, with a read-only audit trail.

---

## SQL Account

### Who they are

SQL Account is a long-standing Malaysian Windows-centric accounting suite, dominant in SME accountants’ offices alongside AutoCount. Not a cloud-native darling; installed, on-prem or RDP, deep local tax, SST, now **MyInvois**.

**ICP:** SMEs whose bookkeeper already runs SQL. F&B/retail via SQL POS variants. Not developers.

**Relationship to Lazuar:** **Partner (destination for the ledger) + substitute for the compliance half.** A merchant will not leave SQL because Lazuar exists. They might use Lazuar to *stop typing* checkout data into SQL.

### Pricing model

**Perpetual or module licences + annual maintenance**, sales-quoted. e-Invoice is typically a module or a version-gated feature, not a public SaaS page. **Unknown public price.** Secondary market: accountants bundle it into monthly bookkeeping retainers.

### Feature list

GL, AR/AP, SST, stock, multi-company, **e-Invoice submission to MyInvois**, self-billed, credit notes. Some payment-gateway connectors exist via partners, but SQL is not a checkout product.

### Strengths versus Lazuar

- Accountant incumbency. The person who files the tax return lives in SQL.
- Local tax depth (SST codes, withholding, industry modules) Lazuar should not attempt to fully replicate.

### Weaknesses versus Lazuar

- Not online checkout. Not subscriptions. Not dunning. Not API-first.
- UX is 2005. Founders hate it; bookkeepers love it.

### Rival type

**Partner / compliance substitute.** Build an export or (later) a connector. Do not compete for the bookkeeper’s desktop.

### Features Lazuar should track

- SQL’s MyInvois field coverage and error handling (what accountants complain about is the UX bar).
- Any SQL payment-collection add-on (then they nibble the checkout job).

---

## AutoCount

### Who they are

AutoCount is the other Malaysian accounting/POS incumbent (AutoCount Sdn Bhd). Strong in retail/F&B POS + accounting, with a more aggressive product cadence than SQL in the last decade. Cloud and on-prem options. **MyInvois integrated.**

**ICP:** SMEs, retailers, F&B, accountants who standardised on AutoCount.

**Relationship to Lazuar:** Same as SQL — **partner + compliance substitute.** Slightly more of a POS rival to StoreHub than SQL is.

### Pricing model

**Licence + maintenance / subscription SKUs**, sales-quoted. e-Invoice included or module-priced depending on edition. **Unknown public rate card.**

### Feature list

Accounting, POS, inventory, payroll (in some editions), e-Invoice, some ecommerce connectors. Not a developer billing OS.

### Strengths versus Lazuar

- Installed base + accountant channel.
- POS+accounts+e-invoice in one vendor for retailers.

### Weaknesses versus Lazuar

- Not hosted checkout for a SaaS. Not BYOK orchestration. Not dunning.
- Sales-led, implementation-heavy.

### Rival type

**Partner / compliance substitute / POS substitute.** Same play as SQL: export cleanly, do not displace.

### Features Lazuar should track

- AutoCount e-Invoice API / intermediary model.
- Any AutoCount “payment link” or “online invoice collect” feature (that is a HitPay nibble).

---

## FastAccount

### Who they are

FastAccount (and similarly named MY cloud-accounting tools in this cohort) sits in the **cloud-accounting challenger** set against SQL/AutoCount/Xero — Malaysian-made, MyInvois-native, sold to SMEs who want browser-based books without Xero’s price or SQL’s desktop. Public rate cards are sales-quoted or plan-page-volatile; treat price as **unknown** unless a live plan page is re-fetched at deal time.

**ICP:** Small professional-services firms, trading companies, and bookkeepers who want MyInvois without Xero.

**Relationship to Lazuar:** **Partner / compliance substitute.** Same as Xero but weaker brand and more localist.

### Pricing model

SaaS monthly, often with e-invoice in the core plan. **Confirm live.**

### Feature list

Cloud GL, invoices, MyInvois submission, SST, bank rec. Some have payment-link partnerships. Not a subscription billing OS.

### Strengths versus Lazuar

- Cheaper/localist alternative to Xero for the accountant.
- MyInvois is the reason they win deals in 2025–2026.

### Weaknesses versus Lazuar

- No checkout, no dunning, no BYOK, no developer platform.
- Brand and ecosystem thinner than Xero.

### Rival type

**Partner / compliance substitute.** Candidate for a “push validated invoices into FastAccount” integration after Xero.

### Features Lazuar should track

- Payment-collection add-ons.
- API quality (if they have a modern API, they are an easier partner than SQL).

---

## Xero Malaysia e-invoice

### Who they are

Xero is the global cloud-accounting default for “serious” MY SMEs and the firms that serve them. Official MY e-invoicing: [https://www.xero.com/my/initiative/e-invoicing-malaysia/](https://www.xero.com/my/initiative/e-invoicing-malaysia/). **MDEC-accredited Peppol service provider.** e-Invoicing **included in Starter, Standard, Premium — no add-on**. Accountants can register on behalf of clients. Connect via TIN to MyInvois.

**Official MY pricing (same page, fetched 16 August 2026):**

| Plan | Intro | Then |
|---|---|---|
| Starter | **USD 14.50 / mo** first 36 months | **USD 29 / mo** |
| Standard | **USD 25 / mo** first 36 months | **USD 50 / mo** |
| Premium | **USD 37.50 / mo** first 36 months | **USD 75 / mo** |

(Displayed with `$` on the MY page; Xero MY is USD-denominated in this fetch.)

**ICP:** SMEs with an accountant, agencies, SaaS companies that already think in Xero. Overlaps Lazuar’s ICP *on the finance-team side*.

**Relationship to Lazuar:** **The most important compliance partner in the file.** ADR 021 explicitly keeps Xero/QBO sync. Xero is also a **substitute** if the merchant believes “Xero + Billplz + a Zapier dunning email” is enough — which, for many, it is, until subscriptions and B2C consolidation volume explode.

### Feature list

Full cloud accounting; invoicing; bills; bank rec; payroll (where sold); **Peppol e-invoicing + MyInvois**; repeating invoices (not a payment-collection engine); Hubdoc-class capture in some plans. Payment services exist in some countries; in MY, collection is usually via a gateway integration (Curlec testimonials mention Xero; HitPay markets Xero sync).

### Strengths versus Lazuar

- **Accountant distribution.** The firm recommends Xero, not a checkout vendor.
- **Peppol + MyInvois in the subscription.** No utility-credit conversation.
- **Repeating invoices + statement chases** cover a lot of simple B2B retainers without a billing OS.
- Brand trust.

### Weaknesses versus Lazuar

- **Xero does not own the Buy button.** ADR 021’s exact sentence.
- Repeating invoices ≠ card MIT / e-mandate / wallet mandate. Someone still has to get paid.
- No hosted checkout, no SaaS entitlements, no WhatsApp dunning, no double-entry *at the gateway-fee-and-tax-line* the way Lazuar’s billing module intends.
- USD pricing + FX is annoying for MY micro-SaaS.

### Rival type

**Partner first. Substitute for simple B2B invoice-and-hope. Never a checkout rival.**

### Features Lazuar should track

- Xero’s MyInvois error UX and consolidated-invoice support.
- Xero App Store: any “Lazuar-class” checkout app appearing (that is a race).
- Xero Pay / payment-services MY (if Xero starts collecting, they become HitPay).
- Tax-rate and SST handling — Lazuar must map cleanly or accountants will reject the sync.

---

## Other PEPPOL / MyInvois middleware

This is a **category**, not one company. Names a Malaysian founder actually meets in 2026:

| Name | Shape | Notes |
|---|---|---|
| **JomeInvoice** | Ecommerce middleware | Shopify / Woo / EasyStore / Boutir → MyInvois. Credit/debit/refund notes, self-billing. Direct substitute for Lazuar’s *DTC* compliance story. |
| **Sufio** | Shopify invoicing | MY e-invoices for Shopify. Global product with a MY connector. |
| **Assist.biz** | SME e-invoice SaaS | Portal + integrations. Content-markets “how to register MyInvois”. |
| **EasyInvoice.my** | SME / retail | POS-adjacent, cheap daily pricing in some SKUs (secondary). |
| **Pagero / Thomson Reuters** | Enterprise Peppol access point | Multinational clearance. Not an SME checkout. |
| **B2Brouter / Storecove / Tickstar** | Peppol access points | Network plumbing. Relevant if Lazuar ever becomes a Peppol participant. |
| **Invoici (Xero-related explainer stack)** | Xero companion | Appears in Xero Central as “e-Invoicing with Invoici”. |
| **ClearTax MY** | Tax tech | India-origin, MY e-invoice content and tools. |
| **Financio** | MY cloud accounting | StoreHub lists it as an accounting sync target. |
| **QNE / Sage MY** | Accounting | Same bucket as SQL/AutoCount. |

**ICP:** Anyone whose *commerce* system does not speak MyInvois.

**Relationship to Lazuar:** **Direct rivals on the compliance half** (especially JomeInvoice / Sufio / Assist.biz) and **partners** if Lazuar decides not to be a Peppol access point and only does MyInvois API. They do **not** steal the checkout or dunning job unless they add payment links (watch Assist.biz and EasyInvoice for that creep).

### Pricing model

Mix of per-document, monthly SaaS, and “included in accounting”. Public pages move; treat as sales-quoted except where a live plan is fetched at deal time.

### Strengths versus Lazuar

- Specialists. They will track every LHDN schema change faster than a checkout company that treats LHDN as a hidden backend.
- Already in Shopify/Woo app stores — distribution.

### Weaknesses versus Lazuar

- **After-the-fact.** They invoice what already happened. They do not convert the checkout, do not retry the card, do not own entitlements.
- Two vendors (gateway + middleware) = two sources of truth. Lazuar’s pitch is one event → money + tax + ledger.

### Rival type

**Direct rival on compliance-only deals. Partner if they stay middleware. Substitute when bundled into EasyStore/Shopify.**

### Features Lazuar should track

- JomeInvoice / Sufio Shopify UX (the bar for any Lazuar commerce-OS app).
- Per-document pricing (Lazuar’s credit-wallet analogue).
- Any of them adding **payment collection** (then they become HitPay-shaped).

---

## Substitute stacks (WhatsApp + Billplz link + Excel + MyInvois)

This is the incumbent. It does not have a Series B. It has **every Malaysian SME**.

### The stack, step by step

1. Customer asks price on WhatsApp or Instagram DM.
2. Seller sends a DuitNow QR, a bank number, or a Billplz/ToyyibPay/senangPay/CHIP/HitPay link.
3. Customer pays. Seller receives a WhatsApp from the bank or a gateway SMS.
4. Seller types the order into a notebook, Google Sheet, or nothing.
5. Fulfilment is another WhatsApp (“tracking number”).
6. At month-end, if the seller is in the e-invoice mandate and has a fearful accountant, someone downloads a CSV from Billplz and re-keys a **consolidated e-invoice** into the MyInvois portal. If the buyer requested an individual e-invoice, it is a crisis.
7. If the seller runs a “subscription” (tuition, gym, “monthly retainer”), they send a new link every month and chase debtors in a broadcast list. There is no mandate, no retry, no grace period, no entitlement cut-off.

### Why this stack wins

- **Time to first payment: four minutes.** No KYC if they only use personal DuitNow. (BNM does not love this; it continues anyway.)
- **Zero SaaS fee.** MDR only if they used a gateway; 0% if they used personal DuitNow (against every ToS, still common).
- **The UI is WhatsApp**, which the customer already has open.
- **Trust is personal.** “Abang, I already transfer” is a protocol.

### Why this stack dies (Lazuar’s entry points)

| Trigger | What breaks | What they reach for | What Lazuar should be |
|---|---|---|---|
| They productise a **subscription** | Monthly chase does not scale; involuntary churn | Curlec, HitPay recurring, Woo Subscriptions | BYOK + real MIT rail + dunning |
| They hire a **developer** / launch a SaaS | Sheets are not an API; entitlements leak | Stripe, Xendit, CHIP API | Hosted checkout + webhooks + ledger |
| **LHDN mandate** hits their revenue band | Portal keying + RM10k individual invoices | Xero, StoreHub, JomeInvoice, accountant | Checkout-native MyInvois + consolidation job |
| They need **SST on the invoice** | Wrong tax, angry corporate buyer | Accountant + Xero | Tax-aware checkout + UBL |
| A **corporate buyer** demands TIN + LHDN QR before paying | WhatsApp link looks unprofessional | senangPay quotation, Xero invoice + Billplz | Pillar 2: TIN validate → pay → validated e-invoice |
| Chargebacks / “I paid” disputes | No ledger, no webhook log | Bigger gateway + accountant | Double-entry + webhook audit |
| They expand to **SG/ID** | Personal DuitNow does not collect SGD | HitPay, Xendit, Airwallex | Do not over-claim; partner rails |

### How to compete without becoming WhatsApp

Lazuar must **not** build a WhatsApp CRM (ADR 021 kill list). Lazuar **must** be a link the seller can paste *into* WhatsApp that is better than a Billplz bill: branded, tax-aware, subscription-capable, with a receipt that is a real e-invoice when required. Dunning WhatsApp (roadmap) is allowed because it protects the transaction, not because Lazuar is a chatbot company.

### The Excel layer

The spreadsheet is the merchant’s ledger. Lazuar’s double-entry module is a *replacement* for that spreadsheet, not a dashboard of gateway settlements. If the Ops UI cannot answer “what is my net cash, my SST, my LHDN obligation, my failed renewals this week?” better than a sheet, the substitute stack stays.

---

## Positioning map

### Two-axis map (software depth × local money+tax)

Think of a plane:

- **X-axis — Local money + tax reality:** left = global/card/FX; right = FPX + DuitNow + e-mandate + MyInvois + SST.
- **Y-axis — Software depth on the JTBD:** bottom = dumb pipe / QR; top = checkout + subscriptions + dunning + ledger + tax documents + developer API.

```
                         SOFTWARE DEPTH
                                 ▲
                                 │
                                 │  [Lazuar target]
                                 │   Chargebee-shaped
                                 │   but MY-native
                                 │
                    Xendit Sub   │   Curlec
                    Stripe Bill  │
                    Airwallex Sub│
         HitPay ─────────────────┼────────────── CHIP (if e-invoice real)
         senangPay catalog       │   Billplz Catalog
                                 │
                    Fiuu / iPay88│
                    2C2P         │
                                 │
         Midtrans / PayMongo     │   ToyyibPay
         (wrong country)         │
                                 │
  Airwallex treasury             │   Boost / TnG QR
  Shopify / EasyStore            │   MyInvois portal
  (own the store,                │   WhatsApp + DuitNow
   not the billing OS)           │
                                 │
     global / FX / card          │          FPX / QR / LHDN
     ◄───────────────────────────┴──────────────────────────►
                    LOCAL MONEY + TAX REALITY
```

**Nobody is in the top-right today.** That is the whole company.

- **Curlec** is highest on MY recurring, medium on software, low on tax.
- **Xendit** is high on software + regional, medium-high on MY recurring (claimed), low on tax.
- **HitPay / senangPay / Billplz Catalog** are medium on software, high on SME distribution, low on tax and true MIT.
- **CHIP** is climbing: modern API, recurring, claimed e-invoice receipts, still an acquirer.
- **Xero / StoreHub / JomeInvoice** are high on tax, zero on checkout-as-a-product (StoreHub has a till, not a SaaS checkout).
- **Fiuu / iPay88 / 2C2P** are high on methods, low on software depth for this JTBD.
- **Lazuar today (honest):** mid software (hosted checkout + subscriptions + email dunning + ledger + LHDN backend), high on tax *architecture*, BYOK (unique on this map), UI for LHDN hidden, WhatsApp dunning not shipped. The **positioning** is top-right; the **product** is mid-right. The gap is the roadmap.

### Competitive sets (who we lose deals to)

| Deal type | Names we lose to | Why | How to not lose |
|---|---|---|---|
| “I just need a link today” | Billplz, ToyyibPay, CHIP links, HitPay, WhatsApp DuitNow | 4-minute time-to-cash | Do not fight. Offer a link that is *as fast* and quietly better (receipt, tax, customer object). |
| Non-technical SME, online + offline | **HitPay**, senangPay, Revenue Monster, StoreHub | One app, POS, Xero, no monthly (HitPay) | Do not build POS. Win the ones who feel LHDN + subscriptions, not the ones who feel the till. |
| Malaysian subscriptions (tuition, gym, retainer, B2B SaaS) | **Curlec**, then Xendit, then HitPay links | E-mandate + retries | Integrate Curlec/Xendit as BYOK; beat them on dunning + ledger + LHDN. |
| Technical founder, SEA deck | **Xendit**, Stripe, Airwallex | One API, six countries, docs | Be honest: Lazuar is MY-depth. Offer BYOK to Xendit for regional, Lazuar for tax+ledger+dunning. |
| Cross-border agency / exporter | **Airwallex**, Wise, Stripe | Multi-currency accounts | Do not become a bank. Partner at the “invoice + collect + file LHDN export” layer. |
| Enterprise / GLC RFP | **iPay88/ADAPTIS, Fiuu, 2C2P, GHL** | Procurement, terminals, AM | Do not RFP as an acquirer. RFP as software on their existing MID (BYOK). |
| DTC brand on a store | **EasyStore, Shopify + plugin, Fiuu** | They already have a cart | Shopify/EasyStore *app* for MyInvois + failed-renewal, not a second checkout. |
| “Just do my e-invoice” | **Xero, StoreHub, JomeInvoice, MyInvois portal** | Accountant already chose | Never sell e-invoice alone. Sell “the payment creates the invoice”. |
| Islamic / NGO collections | **ToyyibPay**, Billplz | Shariah, RM 0 NPO | Do not copy. If we need this ICP, BYOK ToyyibPay and stay out of their fatwa. |
| Informal micro | **WhatsApp + DuitNow** | Free, familiar | Ignore until a trigger in the table above. |

### Jobs-to-be-done matrix

| Job | Best incumbent | Lazuar’s right to win | Must-have to win |
|---|---|---|---|
| Accept a one-off MY payment on my site | Billplz / CHIP / Woo plugin | Weak (BYOK adds friction) | 5-minute BYOK + hosted page that looks more branded than Billplz |
| Accept a one-off payment without a site | HitPay / Catalog / senangPay / WhatsApp QR | Weak-medium | Link UX + WhatsApp paste + receipt |
| Accept cards from foreigners | Stripe / Fiuu / Airwallex | Medium (BYOK Stripe) | Stripe connector + tax as export/zero-rated |
| Auto-collect a MY subscription | **Curlec e-mandate** / Xendit | Strong if orchestrated | Real MIT, not a monthly link |
| Recover a failed renewal | Email from Woo / Curlec retry / nothing | **Strongest** (stated pillar) | Retry calendar + email now + WhatsApp later + grace + entitlement |
| Issue LHDN e-invoice per B2B payment | Xero / portal / JomeInvoice | **Strongest** | TIN validate at checkout, UBL, QR on receipt |
| Consolidate B2C for MyInvois | Accountant + Excel + portal | **Strongest** | `B2cConsolidationJob` actually running, visible audit |
| Know net cash after fees and tax | Spreadsheet / Xero after the fact | Strong | Double-entry that posts gateway fees + SST + LHDN state |
| Plug checkout into my SaaS | Woo plugin / Xendit / Stripe | Strong | Webhooks, HMAC, customer portal, entitlements |
| Pay out affiliates | CHIP Send / Billplz Payment Order / Xendit | **None — do not build** | Partner |
| Take payment at a counter | HitPay / StoreHub / Boost QR / CHIP mini | **None — do not build** | Ignore |
| Hold USD and pay ads | Airwallex | **None — do not build** | Ignore |

### Local moat thesis (FPX + LHDN + dunning + BYOK)

Four pieces, only valuable **together**:

1. **FPX (and DuitNow, and the wallet tiles)** — table stakes. Not a moat. Every TPA has them. Lazuar’s version of this moat is **not owning FPX** but **orchestrating whichever TPA the merchant already passed KYC with**. That is the BYOK inversion. The moat is *anti-lock-in*, which is the opposite of every acquirer’s moat.

2. **LHDN** — a real moat if (and only if) it is **automatic at the payment event**, including B2C consolidation, B2B TIN validation, credit/debit/refund notes on refunds, and an audit trail an accountant will trust. Hidden UI is fine if the artefacts are undeniable (UUID, QR, error queue). If LHDN stays a half-finished backend, CHIP “e-Invoice receipts” or HitPay+Xero will close the gap with worse technology and better distribution.

3. **Dunning** — a real moat in MY because **the rails fail in MY-specific ways**: empty TnG wallet, FPX session abandoned, e-mandate R-transaction, card expired, salary cycle on the 25th–30th. A retry engine that knows those failure codes and talks to the customer on **WhatsApp** (the only inbox that opens) is not something Billplz will build well. Curlec will build retries; they will not build a campaign OS attached to entitlements and tax documents.

4. **BYOK + ledger** — the structural moat. Taking MDR makes you a financial institution. Lazuar’s ADRs refuse that. The ledger makes gateway fees, SST, and LHDN state *reconcilable*. Nobody in this file sells that as software on top of the merchant’s own MIDs.

**Destroyers of the moat:**

- CHIP or HitPay or Curlec shipping **good MyInvois**.
- Xendit hiring a MY tax PM.
- LHDN making the portal so good (or accountants so cheap) that automation is optional.
- Lazuar becoming an acquirer “just this once”.
- Lazuar building vitamins (POS, store builder, community) and stalling on dunning/LHDN.

### Must-match versus never-copy

**Must-match (table stakes to be in the consideration set):**

- FPX B2C + B2B tiles via BYOK.
- DuitNow QR tile (online).
- At least TnG + GrabPay + ShopeePay as tiles when the connected TPA has them.
- Cards via Stripe (and/or the TPA’s card).
- Hosted checkout that is mobile-first and pasteable into WhatsApp.
- Payment links + a minimum invoice object.
- Webhooks that a junior developer can verify in 15 minutes.
- Subscription object with states a SaaS can hang entitlements on.
- Failed-payment retry + customer-facing update-payment-method flow.
- Refunds that create the right credit/debit/refund note *and* the right ledger lines.
- SST-aware amounts.
- MyInvois submission for B2B and consolidated B2C, even if the UI is hidden.
- Audit log an accountant can export to Xero/CSV.

**Must-match soon (or we lose named deals):**

- FPX e-mandate via Curlec and/or Xendit BYOK (not a Lazuar-built rail).
- Wallet mandates via Xendit/HitPay/CHIP if/when the connector supports them.
- Email dunning that is not a single template.
- WhatsApp dunning (roadmap, but it is the MY channel).
- Xero sync (ADR 021).
- Buyer TIN validation before high-ticket pay.
- Customer portal (update mandate, download e-invoice, cancel).

**Never-copy (licence, distraction, or someone else’s job):**

- Becoming a PayNet TPA / holding merchant funds / taking MDR.
- POS, tap-to-pay, soundboxes, terminal rental (HitPay, StoreHub, CHIP mini, GHL).
- Store builders, catalogues-as-Shopify (Billplz Catalog, EasyStore, senangPay Digital Catalog).
- Loyalty, gamification, WhatsApp mini-programs (Revenue Monster).
- Capital / advances / Shariah financing (CHIP Advance, Toyyib Seedflex, ADAPTIS financing).
- Multi-currency accounts, SWIFT, corporate cards (Airwallex).
- Marketplace held-funds and seller KYC (XenPlatform, Fiuu marketplace) — unless a single tenant pays for it as a project, it is a vitamin.
- Carbon (Fiuu Restorify). Gold, wasiat, waqf (Toyyib+).
- Consumer-wallet campaigns (Boost, TnG, Grab).
- 0% bank IPP as a *principal* (2C2P, senangPay, Fiuu) — surface it if the TPA has it; do not underwrite it.
- Full GL / payroll / stock (Xero, SQL, AutoCount).
- Fighting LHDN or building a second MyInvois portal.

---

## Features to track (IDs)

Use these IDs in the parent tracker. Each is a **watch item**, not a commitment to ship.

| ID | Feature | Who has it / is shipping it | Why it matters to Lazuar | Suggested response |
|---|---|---|---|---|
| **LP-SEA-001** | FPX e-mandate / Direct Debit as a first-class API | Curlec (deep); Xendit (claimed); Fiuu Direct Debit (unclear) | Without this, “subscriptions” in MY are a link farm | BYOK to Curlec/Xendit; do not apply to be a TPA |
| **LP-SEA-002** | Card MIT + account-updater / expiry reminders | Curlec, CHIP, Xendit, Stripe, Fiuu tokenisation, Airwallex | 15% of fails are expiry (Xendit 10 Aug 2026) | Orchestrate; surface “update card” portal |
| **LP-SEA-003** | E-wallet mandates (TnG, GrabPay, ShopeePay) | Xendit, HitPay; Curlec limited | Consumer-sub rail | Connector feature flags |
| **LP-SEA-004** | Smart retry calendar (salary-date aware) | Stripe Billing (cards); Xendit configurable; Curlec retries | Highest-ROI billing feature | **Must-build** in dunning engine |
| **LP-SEA-005** | WhatsApp dunning / reminders | Informal stack (manual); nobody excellent | MY inbox of record | Roadmap; utility credits |
| **LP-SEA-006** | Customer self-serve portal (mandate, invoices, cancel) | Stripe, Curlec subscriber dashboard, HitPay | Cuts support; PDPA-friendly | Must-match soon |
| **LP-SEA-007** | Usage / seat / hybrid billing | Curlec, Xendit Update Cycle, Airwallex, Stripe | SaaS ICP | Ledger-native; do not fake it in the gateway |
| **LP-SEA-008** | Hosted payment links (branded) | Everyone | Table stakes | Must-match |
| **LP-SEA-009** | Quotation → payment link | senangPay, HitPay invoicing, Xero+link | B2B pillar | Invoice object + pay button |
| **LP-SEA-010** | Digital catalog / mini-store | Billplz Catalog, senangPay, HitPay, RM alacarte | SME vitamin | **Never-copy**; allow a single SKU link |
| **LP-SEA-011** | POS / tap-to-pay / phone-as-terminal | HitPay, CHIP mini, Fiuu VT, Boost mPOS, GHL, StoreHub | Wrong ICP | Ignore |
| **LP-SEA-012** | Split payments / marketplace | Billplz, Fiuu, Xendit XenPlatform, RM, CHIP Send | Tempting | Never-copy as a product; maybe a ledger split later |
| **LP-SEA-013** | Payouts / mass payment | Billplz Payment Order, CHIP Send, Xendit, Fiuu, Curlec | Adjacent | Partner; do not hold funds |
| **LP-SEA-014** | MyInvois individual e-invoice at checkout | StoreHub, Xero, JomeInvoice, CHIP “receipts” (verify), RM “e-Invoice Payment Links” (verify) | Pillar 2 | **Core** |
| **LP-SEA-015** | MyInvois consolidated B2C job | StoreHub (claimed monthly), accountants, Lazuar ADR 021 | Pillar 1 | **Core**; make visible in audit UI |
| **LP-SEA-016** | TIN validation before pay | LHDN API; Xero; almost no gateway | Pillar 2 conversion | **Core** for high-ticket |
| **LP-SEA-017** | Credit / debit / refund notes on refunds | Accounting tools; few gateways | Broken tax if missing | **Core** with refunds |
| **LP-SEA-018** | Peppol send (not just MyInvois) | Xero, Pagero, access points | Corporate buyers’ inbound invoice | Later; not MVP |
| **LP-SEA-019** | Xero / QBO sync | HitPay, Curlec, StoreHub, EasyStore | Accountant acceptance | Must-match soon |
| **LP-SEA-020** | SST line-item + tax codes | Accounting tools; rare at checkout | Legal invoice | **Core** |
| **LP-SEA-021** | Double-entry merchant ledger | Lazuar (intent); nobody in the acquirer set | Reconciliation moat | **Core**; keep honest |
| **LP-SEA-022** | BYOK multi-gateway failover | Almost nobody locally | Unique | **Core positioning** |
| **LP-SEA-023** | Developer webhooks + HMAC + sandbox | CHIP, Xendit, HitPay, Billplz (older), Curlec | SaaS ICP | Must-match; beat on docs |
| **LP-SEA-024** | Woo / Shopify / EasyStore apps | All TPAs | Distribution | Woo/Shopify *delegate checkout* later |
| **LP-SEA-025** | 0% IPP / BNPL tiles | senangPay, Fiuu, 2C2P, CHIP Atome/SPayLater, Curlec Atome | Conversion, not billing | Surface via TPA; never underwrite |
| **LP-SEA-026** | Stablecoin collect, MYR settle | CHIP (1.5%); Fiuu crypto | Pillar 3 rhyme | Watch; tax classification is the product |
| **LP-SEA-027** | Cross-border DuitNow QR | CHIP, PayNet linkages, HitPay borderless QR | Tourist / SG corridor | Method tile via TPA |
| **LP-SEA-028** | Multi-currency accounts | Airwallex | Wrong licence | Never-copy |
| **LP-SEA-029** | Financing against receivables | CHIP Advance, Toyyib Seedflex, ADAPTIS | Wrong licence | Never-copy |
| **LP-SEA-030** | White-label gateway | Revenue Monster, some enterprise Fiuu | Platform threat | Ignore unless a reseller motion exists |
| **LP-SEA-031** | Shariah positioning | ToyyibPay, CHIP Advance | Islamic ICP | Optional copy, not a product |
| **LP-SEA-032** | Instant / T+0 FPX payout | Billplz Enterprise, CHIP Send (payouts) | Cash-flow sales objection | Not Lazuar’s; the TPA’s |
| **LP-SEA-033** | Flash / network stored-card checkout | Curlec “4M cards” | Conversion gimmick | Do not copy a closed card network |
| **LP-SEA-034** | ADAPTIS unified API | NTT DATA 2025–26 | Enterprise BYOK feasibility | Watch docs only |
| **LP-SEA-035** | Shopify Payments launching in MY | Shopify (not as of Aug 2026) | Would entrench Shopify and cut 2% tax | Watch; would change EasyStore math too |
| **LP-SEA-036** | Gateway-native MyInvois (CHIP, HitPay, Billplz, Curlec, Xendit, Fiuu) | CHIP claimed; others not | **Moat-killer if real** | Monthly re-check of each TPA |
| **LP-SEA-037** | Intermediary appointment UX on MyTax | StoreHub documents it | Onboarding friction | Copy the guide, reduce clicks |
| **LP-SEA-038** | Usage-based + LHDN (invoice the overage correctly) | Nobody | SaaS + tax | Unique if shipped |
| **LP-SEA-039** | Entitlement webhooks (access on/off with grace) | Stripe Billing; Xendit events; Woo | SaaS ICP | **Core** with dunning |
| **LP-SEA-040** | BNM 2027 tech requirements landing on TPAs | All licensed acquirers | Due-diligence questionnaire | Document how BYOK inherits the TPA’s compliance |

---

## Implications for Lazuar

### 1. Stop talking about FPX as if it is a feature

Every name in the first half of this file has FPX. The founder already has FPX. Lazuar’s sentence is not “we support FPX”. Lazuar’s sentence is **“keep the Billplz / CHIP / Curlec / Stripe keys you already have; we make the subscription actually collect, the failed payment actually come back, and the LHDN document actually exist.”**

### 2. Name the rivals correctly in sales

- Versus **Billplz / CHIP / ToyyibPay / senangPay**: we are not cheaper FPX. We sit on you.
- Versus **Curlec**: you own the mandate; we own the ledger, the tax document, and the dunning campaign. Or: bring your Curlec keys.
- Versus **HitPay**: you are the better SME app. We are the better SaaS/compliance backend. Do not add a till to “beat HitPay”.
- Versus **Xendit / Airwallex / Stripe**: you are the better regional money movement. We are the MY compliance and recovery layer. BYOK them.
- Versus **Xero / JomeInvoice**: you file tax. We create the taxable event at the button.
- Versus **iPay88 / Fiuu / 2C2P**: we will not win the RFP as an acquirer. Ask for a software line item on the existing MID.

### 3. The only two deals that matter in 2026

1. **Malaysian subscription businesses** who are sending monthly Billplz links and bleeding involuntary churn. Competitor: Curlec. Weapon: e-mandate via BYOK + dunning + entitlements + LHDN.
2. **Malaysian online businesses hitting MyInvois volume** whose accountant is threatening them. Competitor: Xero+CSV, JomeInvoice, StoreHub (if they are retail), the portal. Weapon: payment-event → validated XML, including B2C consolidation.

Everything else (POS, catalogs, regional collecting, financing) is a vitamin or someone else’s licence.

### 4. BYOK connector priority (engineering)

In order of how often they steal or enable the JTBD:

1. **Billplz** — already the default; webhooks are the industry dialect.
2. **CHIP** — better API, recurring, claimed e-invoice; default *modern* local rail.
3. **Stripe** — foreign cards, card MIT, global buyers.
4. **Curlec** — e-mandate. Without this connector, Lazuar cannot honestly sell “auto-debit in Malaysia”.
5. **Xendit** — regional + wallet mandates + claimed e-mandate.
6. **Fiuu** — only when a tenant brings an existing MID.
7. HitPay, senangPay, iPay88, Revenue Monster — demand-driven, not roadmap-driven.

### 5. Pricing implications

Do not pick a take-rate. The entire competitive set that looks like “payments” monetises GMV; the entire set that looks like “accounting” monetises seats. Lazuar’s SaaS + credits model is coherent **only if** the credits map to things the merchant already understands as costly (LHDN submissions, WhatsApp messages) and the SaaS fee is cheaper than the junior they would hire to do Excel + portal + chase.

HitPay’s “RM 0 / month” will be used against Lazuar in every SME meeting. The counter is not a free plan that bankrupts support. The counter is: **HitPay does not file MyInvois and does not e-mandate.** If that sentence becomes false (LP-SEA-036), revisit pricing the same week.

### 6. Honest product gaps versus this landscape (as of 16 August 2026)

From the lazuar-pay README watermark + this landscape:

- WhatsApp dunning is roadmap; HitPay/Curlec/Xendit already collect; the informal stack already chases on WhatsApp. **Gap.**
- LHDN UI is hidden; StoreHub/Xero/JomeInvoice *show* the merchant a UUID. Hidden is fine; **an audit queue is not optional**.
- No Curlec connector means no honest e-mandate story. **Gap.**
- No Xero sync means accountants will keep a parallel book. **Gap.**
- Hosted checkout must be as fast to create as a Billplz bill or the informal stack wins. **UX gap, not a feature gap.**
- Multi-gateway failover is the unique story; if the product is “one gateway at a time”, the story is a slide.

### 7. What to tell the company

The local/SEA landscape is **crowded at the pipe** and **empty at the junction of pipe + recovery + tax + books**. That junction is Lazuar. The companies that can occupy it without changing their DNA are **CHIP** (if e-invoice is real), **Curlec** (if they hire a tax team), **Xendit** (if they hire a MY tax PM), and **HitPay** (if they add MyInvois + e-mandate). Track those four on a monthly cadence (LP-SEA-036 plus LP-SEA-001/003/014). Everyone else is a rail, a till, a store, or an accountant.

Do not become them. Use them.

---

*End of dossier. Research date 16 August 2026. Re-fetch official pricing before any customer-facing comparison table; several vendors (Xendit blog vs Xendit pricing page; Curlec FAQ vs Curlec table; Fiuu/iPay88/2C2P entirely sales-quoted) already disagree with themselves.*
