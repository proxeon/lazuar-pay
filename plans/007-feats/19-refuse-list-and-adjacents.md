# 19 — Refuse list and adjacent traps

**Program:** `plans/007-feats` — competitor features vs **Lazuar Pay** (this repo).  
**Document:** `19-refuse-list-and-adjacents.md`  
**Date:** 16 August 2026  
**Status:** Full uncondensed analysis. **No product code.** Not a ship ticket. Not a rewrite of ADRs.  
**Workspace of law:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` (ADRs, modules, watermark).  
**Workspace of tracker:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats` (`00-checklist-tracker.md`, `20-sequencing-and-tracker-schema.md`).  
**Author role:** Subagent 19 of 20 — the file that keeps the tracker honest by naming what we will **not** implement even when a famous competitor has a screenshot of it.

**Standing constraints this file must not contradict** (`plans/007-feats/README.md`):

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar's SaaS fee.
- Do not sell WhatsApp dunning or LHDN e-invoice as live product until those loops are closed and (for LHDN) un-hidden.
- Do not become a website builder, marketplace, POS, or ERP to “match competitors.”
- Wrap rails (Stripe, Billplz, CHIP, later Xendit) — do not rebuild acquiring.
- Aura (salon) is a **customer** of Hub, not a competitor. System A (Paddle SaaS) and System B (Hub guest money) stay separate.

**This file is the refuse constitution.** `00-evaluation.md` may index it. `00-checklist-tracker.md` may cite it. Neither may quietly promote a **Refuse (Wave R)** row into Wave 0–4 because a competitor column is `Y`.

---

## How to read this document

This note answers one product-ops question:

> Competitors (creator tools, checkout tools, salon OS, marketplaces, POS, ERP, course platforms, MoRs, gateways) have dozens of surfaces Lazuar Pay does not have. Which of those surfaces are **gaps**, and which are **other companies** that we must refuse — or delay so hard that treating them as Wave 1 is a lie?

It is **not**:

- A rewrite of ADR 015, 018, 019, 021, 022, or 023. Those documents remain law. This file **applies** them to the competitor tracker.
- Permission to delete LHDN backend, Billing ledger, Commerce dunning, or BYOK adapters. Those are the product.
- Permission to treat ADR 014’s fifteen apps as a backlog. That catalog is **historical ambition**.
- A claim that LHDN, WhatsApp dunning, or Xero sync are live sellable surfaces. Backend ≠ product.

Letter collision reminder (do not mix):

| Plane | Who pays whom | Processor | This file’s job |
|-------|---------------|-----------|-----------------|
| **A. SaaS** | Tenant → Lazuar (when we charge for the software) | **Paddle MoR** (Aura Plan today; Pay’s own commercial collection later) | Never move this onto Pay-held funds. Never take GMV to fund $0 SaaS. |
| **B. Buyer → merchant** | Buyer → merchant | **Lazuar Pay BYOK** (Billplz / Stripe / CHIP / Razorpay) | Never hold the funds. Never become the acquirer. |
| **C. Desk / cash / proof** | In person | Tenant’s till, or Qashier hardware | Never route through Pay. Never mint a cash drawer. |

If a competitor feature requires collapsing A/B/C, it is a trap even if the screenshot is beautiful.

Aura appears in this file only as (1) a **Hub customer** whose money plane must stay System B, and (2) a **warning** about traps already paid for (Channels, hardware, marketplace). Aura salon features are not Pay gaps.

---

## Index

| § | Section | What it answers |
|---|---------|-----------------|
| Method | How refuse vs delay was decided | Sources, tests, vocabulary |
| Historical ambition vs ADRs | What we once promised | ADR 014 → 015 → 018 → 019 → 020 → 021 → 022 → 023 |
| Refuse catalog | What we will not build | Twelve refuse families, fully argued |
| Delay catalog | What we might build later | Escrow, e-sign, GSTN/Coretax, Xero *sync*, extra rails |
| Adjacent lookalikes | Features that *sound* like the product | Thin versions vs clones |
| Partner instead | Who we name in sales and docs | One partner per refused job |
| How this constrains the tracker | Wave **R** vs Later vs keep | IDs `LP-002`…`LP-210`, promotion, scoring |
| Source index | Paths and ADRs | So a later editor can audit |
| Close | One-page recap | What a solo founder is allowed to want |

---

## Method

### What “refuse” means here

**Refuse** = we will not implement this as a first-party Lazuar Pay product surface, even if:

- a competitor markets it on their pricing page,
- a prospect asks for it in a demo,
- ADR 014 or ADR 020 still lists it,
- leftover code, TypeSpec residue, or README Phase 1–3 copy mentions it,
- it would be “mostly reuse” of Commerce / Billing / portal routing.

A refuse item may still appear in the tracker. It appears as Wave **R**, Lazuar cell **R**, and it **does not count** in any gap score.

**Delay** = we accept the job as *possibly* ours, but not until a named prerequisite is true (usually: MY checkout + dunning + LHDN-on-MY are real and loved). Delay items are Wave **4** or unscheduled Later, and they still need a pain paragraph before they enter an earlier wave.

**Keep (not refuse, not delay-as-fiction)** = ADR 021 already named these as the product: checkout, BYOK gateways, double-entry ledger, dunning that protects the transaction, LHDN at the point of sale (backend now; UI when ADR 023 is reversed), Xero *sync* (not Xero *replacement*).

The difference between delay and refuse is **company shape**, not calendar. Escrow is delay because it is still “high-ticket checkout + compliance.” A website builder is refuse because it is a different company.

### The four tests a refused job fails

A competitor capability is **refused** if it fails two or more of these:

1. **Transaction-or-compliance test (ADR 021).**  
   “If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.”

2. **Solo-founder-scale test (ADR 015 / 019).**  
   Does the job create an infinite frontend surface (WYSIWYG, themes, drag-and-drop, asset CDN, A/B tests, mobile app store, hardware SKUs) that a funded design tool or OEM already owns?

3. **Money-plane test (ADR 019 BYOK).**  
   Does the job require us to hold funds, underwrite risk, take a percentage of GMV, vault PANs, or become a licensed payment institution in Malaysia?

4. **Two-sided-market test (ADR 018 + tracker `LP-203`).**  
   Does the job require a consumer graph, ranking of tenants, trust & safety desk, or “come for demand, stay for the take-rate”?

Delay items typically pass (1) and fail only *timing* or *jurisdiction readiness*. Refuse items fail (2), (3), or (4) even if they pass (1) in a slide deck.

### Sources read (not summarized away)

#### Product law — Lazuar Pay

| Source | Absolute path | What it freezes |
|--------|---------------|-----------------|
| Product watermark | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` | Shipping product = ADR 021 + 023. Historical ambition = ADR 014 + 020. Community/Vault removed (022). Honest capability today: BYOK + commerce + ledger + email dunning + LHDN **backend**. WhatsApp dunning and compliance UI are not guaranteed demoable. |
| ADR 014 | `docs/architecture-decision-log/014-apps.md` | Historical 15-app superapp catalog. Watermarked 2026: do not implement without reversing 021/023. |
| ADR 015 | `docs/architecture-decision-log/015-avoiding-the-cms-trap.md` | No CMS, no WYSIWYG, no marketing asset hosting. Headless payment links. Persuasion lives off-platform. |
| ADR 016 | `docs/architecture-decision-log/016-platform-domain-strategy.md` | Three-tier domains (`api` / `ops` / `portal`). Custom creator domains go to **static edge**, not our SSR. |
| ADR 017 | `docs/architecture-decision-log/017-portal-frontend-architecture.md` | Portal vertical slices assumed 15 apps. Still useful for checkout isolation; community/vault routes are dead ambition. |
| ADR 018 | `docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md` | Marketplace *if ever* is structured metadata + Astro discover + blind checkout. Not a CMS. Not Phase 1. |
| ADR 019 | `docs/architecture-decision-log/019-checkout-as-a-service-pivot.md` | Identity is CaaS. Thin fulfillment wrappers. BYOK not MoR. Utility wallet, not take-rate. |
| ADR 020 | `docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md` | Long-range wishlist. Phase 2/3 (escrow, e-sign, DRM, Wise, BNPL, crypto, national KYC) are not current scope. Community bouncer **contradicts** 021. |
| ADR 021 | `docs/architecture-decision-log/021-compliance-caas-pivot.md` | Compliance-first. Kill giveaways, community DRM, website/link-in-bio. Keep WhatsApp dunning + Xero sync. Three tax pillars. |
| ADR 022 | `docs/architecture-decision-log/022-remove-community-vault-modules.md` | Hide then remove Community & Vault. Code is **further than the ADR text**: backend module dirs are gone. |
| ADR 023 | `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` | Hide LHDN/B2B UI. Do not delete backend. Compete on FPX + dunning until compliance UI returns. |
| Intent vs code | `docs/001-gaps/20-architecture-intent-vs-implementation.md` | WhatsApp = console stub. Xero = absent. GSTN/Coretax = absent. Marketplace = absent. Community/Vault = deleted. Escrow/e-sign/Keygen/crypto = absent. |
| Billing golden rule | `apps/lazuar-api/Modules/Billing/README.md` | Payments = dumb pipe. Commerce = access. Billing = truth. Not a gateway. Not Xero. |
| Live modules | `apps/lazuar-api/Modules/` | One, Commerce, Payments, Billing, Lhdn, Communications, Messaging, CRM, Ops. **No** Community, Vault, Bio, Funnel, Academy, Giveaway, Affiliate, Sponsor, Pipeline. |

#### Tracker law — this program

| Source | What it freezes |
|--------|-----------------|
| `plans/007-feats/README.md` | BYOK not MoR; wrap rails; no builder/marketplace/POS/ERP; Aura is a customer. |
| `plans/007-feats/00-evaluation.md` | Four jobs only: take money, run subscriptions, stay invoiced, unlock via webhooks. Mixing rival *kinds* produces a bad roadmap. |
| `plans/007-feats/00-checklist-tracker.md` | Wave **R** already stamped on MoR, acquiring, BNPL-as-us, settlement reports, Stripe Tax/Avalara, SMS, marketing blasts, HubSpot-CRM, and `LP-200`–`LP-207`. `LP-208`/`LP-210` delay. `LP-209` waits on MyInvois sold. |
| Sibling reports `01`–`18`, `20` | Evidence. This file does not wait on them to name refuse; it is the refuse constitution they must not contradict. |

#### Competitor facts used (public, mid-August 2026)

Named because a refuse list without names becomes a sermon:

- **Creator / checkout:** Gumroad, Lemon Squeezy / Stripe, Paddle, Polar, FastSpring, Shopify, Stan Store, Whop, Podia, Kajabi, Teachable, Thinkific, Circle, Skool, Mighty Networks, Linktree, Beacons, Bento, Carrd, Framer, Webflow, ClickFunnels, Systeme.io, Kit (ConvertKit), Mailchimp, Klaviyo, KingSumo / Gleam, Memberstack, Keygen, BTCPay Server, Coinbase Commerce, Escrow.com, DocuSign / PandaDoc / Dropbox Sign.
- **Local rails / POS / tax:** Billplz, CHIP, HitPay, Fiuu, Xendit, Midtrans, Razorpay/Curlec, ToyyibPay, SenangPay, iPay88, StoreHub, Qashier, AutoCount, SQL Accounting, Xero, QuickBooks, LHDN MyInvois, GSTN (IN), Coretax (ID), InvoiceNow (SG).
- **Subscription engines:** Chargebee, Recurly, Maxio, Lago, Stripe Billing.
- **Salon / marketplace (adjacent only):** Fresha, Booksy, Treatwell, Square — they train prospects to expect Discover, hardware, and take-rate. Those are still refuse.

Prices, take-rates, and help-center wording drift. The **company shape** does not.

### Vocabulary used below

| Term | Meaning in this file |
|------|----------------------|
| **First-party app** | A Lazuar-hosted product surface (ops module + portal routes + schema) that the tenant “turns on.” |
| **Thin fulfillment** | After money clears: fire a webhook, mint a signed URL, send an email, write a ledger line. No community feed, no course player, no page builder. ADR 019’s intended remaining shape. |
| **Vitamin** | ADR 021’s word for marketing/presentation software with low barriers and high churn. |
| **MoR** | Merchant of Record — the legal seller of record who holds funds, files tax, and eats chargebacks (Paddle, Lemon Squeezy, Polar, FastSpring). |
| **Payfac / acquirer** | The party that onboards merchants to card schemes or FPX and settles money (Stripe, Billplz, HitPay, Xendit, Square). |
| **BYOK** | Tenant pastes *their* gateway keys. Money never sits in a Lazuar pooled account. |
| **CMS trap** | ADR 015: rich text + assets + themes inside the cash register. Also the engineering black hole of builders. |
| **Solo founder scale** | One person (or a tiny team) can keep the deterministic core correct. Infinite UI surfaces destroy that. |
| **Partner** | The named external product we recommend instead of building. Not an integration commitment unless a later program says so. |
| **Wave R** | Tracker mark: refused. Not a later-maybe. |

### Honesty rules

- **Code and ADR 021/023 win** over ADR 014/020 when they disagree.
- **“Keep Xero”** means *sync to Xero*, not *become Xero*. Absence of Xero code is a **delay of a keep** (`LP-121`), not a refuse (`LP-206`).
- **“Keep WhatsApp dunning”** means a **transactional recovery channel** (`LP-074` / `LP-155`), not a blast product (`LP-157`). The channel is currently a console stub. That is an honesty gap on a **keep**, not permission to build Mailchimp.
- Leftover `use-product-associations.ts` (Community/Vault stub), telegram fields in generated types, and Communications templates that still say “community” are **cleanup**, not a reopen.
- README Phase 1 listing GSTN/Coretax as if current is **doc drift**, not a Wave 1 row.

---

## Historical ambition vs ADRs

This section exists so a later engineer cannot say “but 014 still lists Academy at priority 4.” The watermark is already on the file. The *argument* is here.

### The five identities the repo has worn

Lazuar has written down five successive identities. They still coexist in markdown. Only the last two are allowed to create tracker rows.

| Era | Document | Identity | What it wanted us to become |
|-----|----------|----------|-----------------------------|
| **Superapp** | ADR 014 (and 016/017 routing for 15 modules) | Ops-page of 15 B2C apps + core infra | Link-in-bio, forms, events, consults, giveaways, vault, academy, funnel, invoices, pipeline, community, broadcast, affiliate, sponsor, support |
| **Anti-CMS cash register** | ADR 015 | Headless payment links | No WYSIWYG, no marketing CDN, persuasion off-platform |
| **Network (later)** | ADR 018 | SEO marketplace on Astro | Structured metadata catalog, *not* a builder; Phase 2+ |
| **CaaS** | ADR 019 | Sovereign checkout + fulfillment engine | Thin wrappers after pay; BYOK; utility wallet |
| **Wishlist** | ADR 020 + README Phase 1–3 | Integration catalog | Gateways, tax authorities, Xero, WhatsApp, then escrow/e-sign/DRM/Wise/BNPL/crypto/KYC |
| **Compliance CaaS** | ADR 021 | Transaction + government tax only | Kill vitamins. Three tax pillars. Keep dunning + Xero. |
| **Deletion** | ADR 022 | Community & Vault are gone | Not “thin wrappers.” Removal. |
| **Pure CaaS MVP** | ADR 023 | Ship checkout + dunning *today* | Hide LHDN/B2B UI. Do not delete backend. |

Root README (16 Aug 2026) already states the resolution:

> Shipping product (MVP) = **ADR 021 + ADR 023**. Historical ambition = ADR 014 + ADR 020. Do not implement “15 apps,” community DRM, or link-in-bio from those docs without an explicit reverse of ADR 021/023.

This file is that resolution applied to **competitor envy**.

### What ADR 014 actually catalogued (so we can refuse it by name)

ADR 014 is still the most dangerous document in the repo because it is specific, complete, and flattering. It named:

**Core (keep as infrastructure, not as 15 apps):**

- `One` — CIAM / workspaces  
- `Payments` — gateway adapters (explicit: “Never build a gateway — wrap existing ones”)  
- `Billing` — double-entry ledger  
- `CRM` — client profiles (data foundation, not HubSpot marketing)  
- `Messaging` — transactional dispatch  
- `LHDN` — MyInvois  
- `Ops` — hibernating AI console  

**Acquisition apps (almost all refuse or partner):**

1. Bio — Linktree clone  
2. Form — Typeform clone  
3. Event — Eventbrite clone (historically validated with a Docker bootcamp; **the checkout job remains**, the *event marketing site* does not)  
4. Consult — Calendly clone  
5. Giveaway — KingSumo clone (**ADR 021 explicit kill**)  

**Fulfillment apps:**

6. Vault — Gumroad clone (**ADR 022 removed**)  
7. Academy — Kajabi clone (**refuse as first-party player**)  
8. Funnel — ClickFunnels clone (**ADR 015 / 021 kill**)  
9. Invoice — Stripe Invoicing / Xero-ish (**B2B invoice *as tax document* is keep; invoice *as accounting suite* is refuse**)  
10. Pipeline — Pipedrive clone (**refuse**)  

**Retention apps:**

11. Community — Skool clone (**ADR 021 kill + ADR 022 remove**)  
12. Broadcast — Mailchimp clone (**refuse as marketing cloud; transactional templates stay**)  
13. Affiliate — Rewardful clone (**delay at most; mass payouts are Phase 3 wishlist**)  
14. Sponsor — Passionfroot clone (**refuse**)  
15. Support — Intercom clone (**platform tickets may exist; tenant helpdesk suite is refuse**)  

ADR 014 even published kill criteria (revenue thresholds per module). Those criteria assumed we would *launch* the modules. ADR 021 superseded the launch. The kill criteria are historical.

### What each later ADR forbade or narrowed

#### ADR 015 — the CMS trap (still held)

Live-testing funnels/events showed that **adding description fields to checkout dropped conversion**. The document forbids:

- WYSIWYG (TipTap, Quill, Lexical) for public product descriptions  
- Asset hosting (S3/CDN) for marketing images  
- Building marketing pages inside `ops`  

It allows: deterministic cash-register fields (what, price, payer, pay).  
Mitigation written then: open-source Astro templates. Those templates are **still missing**. Missing templates are not permission to build Framer.

#### ADR 016 — custom domains and 15 subdomains

The app-centric idea (`vault.lazuar.com`, `community.lazuar.com`, mapping `creator.com` onto SSR) was already rejected as a DevOps trap (wildcard SSL, Caddy sprawl, namespace collisions). Creator domains belong on **Cloudflare Pages / Webflow / Framer**. Our three hosts stay `api` / `ops` / `portal`.

Any competitor feature that requires “map the tenant’s apex domain onto Lazuar-hosted **marketing** pages” is refuse (or a static-export partner), even if Webflow-like products treat it as table-stakes.

Tracker nuance: `LP-017` Custom domain **on checkout** is Wave 3 Later (hosted cash-register hostname), **not** a Framer clone. Do not let Wave 3 become a website host.

#### ADR 018 — marketplace as a *later architecture*, not a now product

ADR 018 tried to future-proof a network without violating 015: Product Hunt-style metadata, optional Markdown, Astro `storefront-page`, CQRS projection to a global catalog, then **handoff to blind checkout**. Explicitly:

- Marketplace is not required to survive today.  
- If a creator wants visual control, they build their own landing page and link to portal.  
- Marketplace listing is uniform on purpose.

ADR 021 then tightened the identity to compliance CaaS. A marketplace is a **two-sided content product**. It fails the transaction-or-compliance test *as a company*, even if the *checkout handoff* would be clean. This file therefore **refuses first-party marketplace / multi-vendor** (`LP-203`), and treats ADR 018 as an architectural *note* if a future company (not this solo product) ever wants discover. It is not a tracker wave.

#### ADR 019 — the CaaS pivot (identity we still use)

Two realities forced the pivot:

1. Building 15 frontends competes with Framer, Webflow, Linktree, Kajabi — “UI/UX exhaustion,” “low-leverage infinite maintenance,” violates solo-founder scale.  
2. The bleeding neck is **financial and fulfillment backend**: SEA rails (Stripe is weak at FPX), LHDN XML, dunning, post-pay webhooks.

Decisions that are still law:

- We are not 15 website builders.  
- An “app” is a **thin fulfillment wrapper** after checkout (the *idea* — Community/Vault as first-party apps were later deleted).  
- **BYOK over MoR.** Lemon Squeezy / Paddle take 5–8% and hold funds. We do not.  
- Monetize SaaS fee + prepaid utility credits (LHDN submit, WhatsApp dunning), **not** GMV.  
- Developers are a primary audience (HMAC outbound webhooks).

Trade-off accepted: lose non-technical users who want a builder. Mitigation: templates + docs for Linktree/Framer. Still the correct trade.

`00-evaluation.md` restates this as four jobs: take money, run subscriptions, stay invoiced, unlock via API. Everything else is envy.

#### ADR 020 — the wishlist that must not be read as a backlog

Phase 1 (“un-fireable core”) mixes **true product** with **over-claim**:

| Wishlist line | Law after 021/023 | Code 2026-08-16 |
|---------------|-------------------|-----------------|
| Local BYOK gateways | Keep | Stripe, Billplz, CHIP, Razorpay. Not Fiuu, SenangPay, Xendit, Midtrans, Cashfree |
| Gov tax LHDN + GSTN + Coretax + InvoiceNow | LHDN keep; others **delay until MY is real** | LHDN only |
| Xero / QBO sync | **Keep** (021) | **Zero code** (`LP-121` Wave 4) |
| WhatsApp Cloud dunning | **Keep** as recovery channel | Console stub + email path (`LP-074` Wave 4, not Y) |

Phase 2 (high-ticket): escrow, e-sign, Telegram/Discord bouncer, Keygen.

- Bouncer = **refuse** (021 kill + 022 delete). ADR 020 §7 is **superseded**. `LP-204`.  
- Escrow + e-sign = **delay** (021 pillar 2 still mentions them). `LP-208` Wave 4.  
- Keygen = delay-or-partner; not a first-party license server.

Phase 3: Wise MassPay, B2B BNPL, Bitcoin/Web3, Singpass/MyDigital ID. All **delay at best**; crypto is **near-term refuse** (`LP-207`); BNPL as *our* product is refuse (`LP-039`); mass payouts delay (`LP-210`).

ADR 020’s escrow section still says Vault holds the digital asset. Vault is gone. Do not revive Vault to make escrow “complete.”

#### ADR 021 — the kill list we are executing

Quote (do not water down):

> We are explicitly abandoning the “Jack of all trades” feature factory. **Lazuar is exclusively a Compliance-First Checkout Engine (Compliance CaaS).**  
> If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.

Explicit kills:

- Viral giveaways & lead-gen forms → KingSumo  
- Community DRM (Telegram/Discord bouncers) → Zapier  
- Website / link-in-bio builders → Framer or Astro  

Explicit keeps:

- WhatsApp dunning (failed payment = no tax + lost revenue)  
- Xero / cloud accounting **sync** (CFO loop)

Three pillars (all *tax-at-checkout*, not new companies):

1. B2C consolidation (monthly `ConsolidatedInvoice`) — backend job exists; UI hidden.  
2. B2B TIN + instant invoice — APIs exist; TIN/quote UI hidden; escrow/e-sign mentioned as *offerings* for trust, not as “build Escrow.com.”  
3. Cross-border zero-rated classification — **not evidenced** as a first-class engine. USDC/Web3 in the pillar text is **aspiration**, not a near-term build.

ICP accepted: professional digital businesses / agencies / high-volume creators who feel tax and ops pain — **not** “buy me a coffee” beginners.

#### ADR 022 — Community and Vault are not sleeping

Phase 1 was “hide.” Reality (gap analysis + `Modules/` listing): **backend module directories are absent.** Frontend still has a no-op `use-product-associations.ts` and some naming collisions under Communications.

This matters for the refuse list: you cannot “just uncomment” Community DRM. Rebuilding it is a new company decision that would have to **explicitly reverse 021 and 022**. The tracker must not have a build wave for that (`LP-204` = R).

#### ADR 023 — hiding the moat is not refusing the moat

UI lobotomy hides invoicing, legal/billing profile, B2B TIN, quote checkout, tax-invoice download. Backend dark matter stays.

**Do not misread 023 as “we refused LHDN.”** We delayed the *surface*. Multi-country tax is a different delay (jurisdiction). Building GSTN while LHDN UI is still `[MVP-HIDE]` is how a solo founder dies.

023’s GTM mitigation was “compete on Billplz (FPX) + WhatsApp dunning.” WhatsApp is not production-true. That is a **keep-item honesty gap**. It is not a reason to build Broadcast campaigns to “look complete.”

### Contradiction map (so the tracker does not inherit them)

| Conflict | Older text | Newer law | Tracker rule |
|----------|------------|-----------|--------------|
| 15 apps vs CaaS | ADR 014, 016, 017 | 019, 021, 023 | No Later rows for Bio/Funnel/Academy/Giveaway/Community/Vault as first-party apps |
| Marketplace later vs compliance-only | ADR 018 | ADR 021 | `LP-203` Wave R |
| Community bouncer in Phase 2 | ADR 020 §7 | ADR 021 kill + 022 delete | `LP-204` Wave R. Partner Zapier/Make |
| Escrow needs Vault | ADR 020 §5 | 022 Vault gone | If escrow is ever done, it is **status + partner API**, not a file locker |
| README Phase 1 lists GSTN/Coretax as current | README / 020 | 021 + 023 + code | `LP-209` Wave 4, after MyInvois is sold |
| WhatsApp sold as MVP | 019/020/021/023 | Console stub | Keep the *job* (`LP-074`); do not expand to `LP-157` |
| Xero listed as keep | 021 | No code | `LP-121` Wave 4; **refuse replacement** `LP-206` |
| HitPay has POS / store | HitPay marketing | 007 README | `LP-200`, `LP-202` Wave R — wrap their *rail* someday, do not clone their suite |

### What the live Pay monorepo actually is (refuse against *this*, not against a dream)

As of 16 August 2026, `apps/lazuar-api/Modules/` contains: **One, Commerce, Payments, Billing, Lhdn, Communications, Messaging, CRM, Ops**.

There is no marketplace schema, no page builder, no Telegram bouncer, no course player, no POS hardware protocol, no Xero client, no GSTN module, no crypto adapter, no Escrow.com client, no DocuSign client, no card vault of our own.

CRM is **profiles**, not campaigns (`LP-168` is R for HubSpot growth; thin `ClientProfile` stays). Communications is **templates + transactional dispatch remnants**, not Mailchimp. Messaging WhatsApp is a **façade over a console logger**. Payments is **adapters**. Billing is **ledger**. Lhdn is **UBL + PKI**. Commerce is **products, checkout, subscriptions, dunning**.

That is the company. The refuse catalog protects it.

---

## Refuse catalog

Each family below is written so a tracker editor can paste a **Wave R** rationale without re-deriving the ADRs.

Format per family:

- **What we are refusing** (scope, including lookalikes)  
- **Why competitors have it** (named, with the economic reason — not “they are big”)  
- **Why copying kills solo-founder scale**  
- **What we do instead** (thin remainder, if any)  
- **Partner to name**  
- **Tracker / ADR lock**

---

### R1 — Website, funnel, and link-in-bio builders

#### What we refuse

First-party:

- Drag-and-drop landing pages, funnel steps, upsell pages, countdown pages, exit-intent popups  
- Link-in-bio hubs with themes, avatars, scheduled links, click heatmaps as a *product*  
- WYSIWYG product descriptions on checkout or portal  
- Marketing image / video CDN inside Pay  
- Custom fonts, themes, and “brand kits” that turn portal into a site builder  
- Apex/custom-domain hosting of **marketing** pages on Lazuar SSR (ADR 016 already forbade this)  
- A/B testing of creative, pixel managers, “website analytics” suites  

Lookalikes that are **also refuse** if they become builders:

- “Just a simple about block on the checkout” (ADR 015: this *lowered* conversion)  
- “Tenant blog so LHDN invoices have a branded home”  
- “Bio page because Instagram requires one link” as a Lazuar-hosted app  

Not refuse (do not confuse):

- Checkout **logo/colors** (`LP-025`) as a cash-register skin.  
- Optional **open-source Astro templates** we do not host as a CMS.  
- `LP-017` checkout custom domain as a later hostname for the register, not a marketing site.

#### Why competitors have it

| Competitor | Why the builder is the company |
|------------|--------------------------------|
| **ClickFunnels / Systeme.io** | They sell *persuasion infrastructure*. The page *is* the product. Checkout is an add-on. |
| **Kajabi / Podia** | Course + email + site in one lock-in. The site keeps the audience on their domain so billing churns less. |
| **Linktree / Beacons / Stan Store** | Instagram’s one-link constraint. They monetize traffic with a directory, email capture, and take-rate store. |
| **Framer / Webflow / Carrd** | Design tools. Infinite surface is their *moat*, not a distraction. |
| **Gumroad / Shopify** | Storefronts are how they become the merchant’s public face and then tax GMV. |
| **HitPay** | Online store + payment links in one SMB suite. Store exists to put more volume on *their* acquiring. |
| **Fresha / Booksy consumer sites** | Marketplace SEO needs a public page per venue. The “website” is a supply-side listing. |

They have builders because **presentation is how they acquire and retain**. Funded design teams exist to fight other funded design teams. AI site generators made the vitamin even cheaper and the churn even higher (ADR 021’s explicit warning).

#### Why copying kills solo-founder scale

1. **Infinite surface.** Components, mobile breakpoints, accessibility, SEO, form spam, custom CSS escape hatches, “can I paste my GTM,” “can I use my own font.” This is a second full-time product.  
2. **ADR 015 is empirical, not aesthetic.** Descriptions on checkout pushed buyers from execution back to evaluation. Building a better CMS would make the *cash register worse*.  
3. **Wrong talent loop.** Pay’s leverage is C#, SQL, UBL, X.509, ledger invariants, webhook idempotency. A builder hires design-system people and still loses to Framer.  
4. **Infrastructure tax.** Asset CDN, image transforms, preview environments, custom-domain SSL — ADR 016 already burned this once.  
5. **Support death.** “My hero image is blurry on iPhone SE” is not a ticket a compliance engine can afford.  
6. **ICP lie.** The users who *need* a builder are the users ADR 021 explicitly walked away from.  
7. **HitPay-envy is a category error.** They are an acquirer + SMB suite (`00-evaluation` §3.1). Cloning their store is how we become a worse HitPay and a missing MyInvois engine.

Solo-founder math: one quarter of TipTap + themes is one quarter not spent on Billplz fulfillment honesty, dunning correctness, or LHDN consolidation. Those are the un-fireable jobs.

#### What we do instead

- **Blind checkout** on `portal`: name, amount, method, tax fields when 023 is reversed.  
- **Docs + examples** showing `href` to a Pay checkout URL from Framer, Webflow, Astro, WordPress, Linktree.  
- Optional **open-source Astro templates** (promised in 015/019, not yet a repo) — templates are *code we do not host as a CMS*.  
- Commerce product fields stay **structured** (title, price, currency, fulfillment target). Not Markdown novels.

#### Partner to name

**Framer, Webflow, Carrd, or a static Astro/Next site on Cloudflare Pages** for persuasion. **Linktree / Beacons** if the only job is Instagram’s one link — the button points at Pay. Do not partner-build a bio app.

#### Tracker / ADR lock

- ADR 015, 016, 019, 021 kill line “Website / Link-in-Bio Builders.”  
- Tracker: **`LP-200`**, **`LP-201`** Wave **R**.  
- Kill phrase: “Creators won’t switch unless they can build the page here.” They should not switch their page. They should switch their *cash register*.

---

### R2 — Community DRM / Telegram / Discord bouncers

#### What we refuse

- First-party Community app (feeds, member directory, posts, comments, reactions, magic-link community portal)  
- Telegram or Discord **bots that invite and kick** based on subscription status (“the bouncer”)  
- Zoom-room DRM as a first-party space  
- Re-adding `Modules/Community` or uncommenting fulfillment target `internal:community` as a product  
- “Skool clone” retention engine  
- Any roadmap line that treats community DRM as Phase 2 of *this* company (ADR 020 §7 is dead)

Not refused (different jobs):

- A **HTTPS fulfillment webhook** that *a tenant’s own bot* or Zapier can consume (`subscription.cancelled` → they kick). That is CaaS (`LP-132`).  
- Human `wa.me` / Telegram links as **text fields** on a product (a URL, not a bot we operate).

#### Why competitors have it

| Competitor | Why DRM-as-product exists |
|------------|---------------------------|
| **Skool / Circle / Mighty Networks / Heartbeat** | The community *is* the SKU. Billing exists to gate humans. |
| **Whop** | Discord/Telegram access is the merchandise. They win indie info-product culture. |
| **Kajabi / Memberstack** | Membership site = content + community + gate. |
| **Telegram “Group Help” / paid bots / many SEA info-sellers** | Manual kick is painful; the bot *is* the business. |
| **Polar** (partial) | Entitlements + Discord integrations appear because developer MoRs sell access, not tax. |
| **Historical Lazuar Community module** | ADR 014 called it the retention engine and tied dunning to it. |

Competitors have bouncers because **recurring revenue on chat platforms is a fulfillment problem they chose to own**. It creates daily operational tickets (wrong kick, bot permissions, Telegram ToS, ban evasion, shared accounts).

#### Why copying kills solo-founder scale

1. **ADR 021 already classified it as a vitamin / automation platform.** “Let them use Zapier.”  
2. **ADR 022 deleted the module.** Revival is not a toggle. It is a new monolith.  
3. **You inherit chat-platform ToS.** Telegram bot spam, Discord privileged intents, sudden API changes, users creating alt accounts — a support org.  
4. **Wrong reliability target.** A failed kick is a social incident. A failed LHDN submit is a legal incident. We can staff one of those.  
5. **It recreates the 15-app frontend.** Member profiles, feeds, moderation, blocked words, admin roles inside the *space* — a second Slack.  
6. **Dunning already moved to Commerce.** The only *keep* reason Community existed (retry failed payments) is no longer Community-shaped.

Solo-founder math: a competent Telegram bouncer is a product (auth, idempotent invite links, kick reconciliation, audit, rate limits, multi-group, Ban/Unban races). That is a year. Zapier + a $9 bot already exists.

#### What we do instead

- Commerce subscription lifecycle events → **outbound HMAC webhook** (make this reliable; it is currently a single-URL silent-match MVP — Wave 0, `LP-132`/`LP-133`).  
- Tenant runs **their** bot, Make scenario, or Whop.  
- Do not store Telegram user IDs as a first-class DRM ledger in Pay.

#### Partner to name

**Zapier or Make** for invite/kick. **Whop / Circle / Skool** if they want a hosted community. **Native Telegram bots** the creator already uses. Not Lazuar-hosted Spaces.

#### Tracker / ADR lock

- ADR 021 kill + ADR 022 + ADR 020 §7 superseded.  
- Tracker: **`LP-204`** Wave **R**.  
- Kill phrase: “Dunning isn’t complete until we kick them from Telegram.” Dunning is complete when they **pay or the subscription ends**. Access enforcement is the tenant’s app.

---

### R3 — Marketplace / multi-vendor / Discover

#### What we refuse

- `lazuar.com/discover` as a **consumer marketplace**  
- Multi-vendor carts (one checkout, many sellers, platform split)  
- Ranking, featured listings, paid ads, “boost,” review graphs as acquisition  
- Cross-tenant search, geo density, “near me”  
- Platform take-rate on GMV to fund $0 SaaS  
- A two-sided trust & safety desk (fake shops, fake reviews, dispute theatre)  
- Multi-vendor “App Store” of creators inside Pay  
- xenPlatform / Stripe Connect **marketplace split-of-funds** as *our* product  
- ADR 018 implemented as a *now* product

Not refused:

- **Tenant-owned** public checkout link (`portal.lazuar.com/{slug}/…`) — that is a storefront, not a marketplace.  
- Structured product metadata *inside one tenant* (title, price, tags) for *their* catalog.

#### Why competitors have it

| Competitor | Why marketplace is the company |
|------------|--------------------------------|
| **Fresha / Booksy / Treatwell / StyleSeat** | They sell **demand**. Software locks supply. Take-rate *is* the P&L. |
| **Gumroad Discover / Etsy / Amazon** | SEO + ads. The catalog *is* the moat. |
| **Xendit xenPlatform / Stripe Connect** | Split payments across vendors. That is an acquiring/marketplace money program. |
| **HitPay** (partial) | Some marketplace/split features exist to keep GMV on their MID. |
| **ADR 018’s own rationale** | “Come for the tool, stay for the network,” SEO aggregation. |

They have it because **network effects require a second customer** (the buyer who does not yet know the merchant). That customer wants price, reviews, and availability. The merchant wants yield and ownership of the client. Those interests conflict. Marketplace companies hire for that conflict.

#### Why copying kills solo-founder scale

1. **Different company (ADR 021).** Two customers, opposite interests, ranking your own tenants.  
2. **Chicken-and-egg spend.** Treatwell-class consumer ads in one country already dwarf a solo P&L. Malaysia does not give this away for free.  
3. **Collides with BYOK + 0% GMV.** You cannot fund Discover without a take-rate or a huge SaaS price. Both destroy the current story (`LP-001`, `LP-004`).  
4. **ADR 015/018 tension.** A marketplace is a CMS of listings. Even 018’s “Markdown only” still needs moderation, abuse, SEO, duplicate shops, stolen photos.  
5. **Trust & safety is a department.** Chargebacks across vendors, stolen accounts, underage products, prohibited categories.  
6. **Engineering: global catalog vs tenant isolation.** ADR 018 already admitted querying 10,000 isolated tenant tables would crush the DB — hence a projection. That projection is a second product.  
7. **Xendit/Stripe split pay is R4 in costume.** Multi-vendor settlement is holding or directing other people’s money.

Solo-founder math: one honest MY checkout + LHDN beats a mediocre Discover. A mediocre Discover also makes every tenant an enemy the week you rank their rival first.

#### What we do instead

- Own the **link**. Instagram, WhatsApp, Google Business, Linktree → Pay URL.  
- Pay: the cash register behind whoever already has demand.

#### Partner to name

If a merchant wants demand: **Fresha / Google / Instagram / Shopee** — *they* can list there. If they want marketplace *settlement*: they want **Xendit xenPlatform or Stripe Connect**, which we will not become. If a creator wants a catalog of *other people’s* products: they are not our ICP.

#### Tracker / ADR lock

- Tracker: **`LP-203`** Wave **R**. Also **`LP-002`/`LP-003`** if the “marketplace” is really take-rate + holding funds.  
- ADR 018 deferred + ADR 021 identity.  
- Kill phrase: “We need Discover to acquire merchants.” Acquisition is sales, partners, and a working FPX link. Not a two-sided graph.

---

### R4 — Becoming a payment gateway, holding funds, or Merchant of Record

#### What we refuse

- Pooled settlement accounts in Lazuar’s name  
- Taking a **percentage of guest/creator GMV** as the business model  
- Acting as **Merchant of Record** for tenant sales (Paddle/Lemon/Polar/FastSpring shape)  
- Acting as a **payfac / acquirer** (Stripe Connect platform charges, Adyen for Platforms, “Lazuar Pay issued MIDs”)  
- Holding buyer funds, delayed payout, “wallet balance” that is *their sales money*  
- Staff/affiliate **instant wallets** funded from GMV  
- Replacing Paddle (System A) with Hub Billing so Pay becomes anyone’s MoR  
- $0 SaaS funded by processing take  
- Storing PAN / CVV / becoming a PCI card vault  
- Applying for a Malaysian **payments license** to compete with Billplz/CHIP/Stripe/HitPay/Xendit on acquiring  
- “Platform balance” that mixes SaaS credits (`TenantCreditBalance`) with merchant settlement  
- First-party **settlement / payout reports** as if we paid them (`LP-095`)  
- **KYC onboarding for *our* acquiring** (`LP-007`) — we are not the underwriter  
- **Stripe Tax / Avalara-class global tax remittance** (`LP-120`) — that is MoR tax, not LHDN-at-POS

Not refused:

- **BYOK adapters** (Billplz, Stripe, CHIP, Razorpay, later others). Tracker mark **W** = wrap.  
- **Utility credit wallet** for *our* metered actions (LHDN submit, WhatsApp recovery) — prepaid software usage, not escrow of theirs (`LP-005`).  
- **Paddle as a SaaS MoR for selling *Lazuar/Aura seats*** — System A on purpose.  
- Recording **gateway fees** in the ledger as math, not as money we deducted.

#### Why competitors have it

| Competitor | Why they hold money |
|------------|---------------------|
| **Paddle / Lemon Squeezy / Polar / FastSpring** | MoR: they are the legal seller. They file VAT/GST, eat chargebacks, take 5%+. Creators avoid entity setup. |
| **Stripe Connect / Adyen platforms** | Interchange + platform fee. This is how marketplace SaaS gets paid without a subscription. |
| **Square / Toast / Qashier** | Hardware + acquiring. Lock the rail, then rent software cheap or free. |
| **HitPay / Xendit / Billplz / CHIP** | They *are* licensed local/regional gateways. That is their charter. |
| **Shopify Payments** | Vertical payfac. GMV is the company. |

They have it because **float, interchange, and take-rate print more than SaaS seats** — if you have licenses, capital, fraud ops, and a treasury team.

`00-evaluation.md` already notes the LHDN collision: MoR means *they* are the seller of record, which **breaks MyInvois** (the Malaysian seller must issue the e-invoice) and is hostile to FPX settlement into the merchant’s own bank.

#### Why copying kills solo-founder scale

1. **ADR 019 decided BYOK specifically to avoid this.** “Lazuar assumes zero financial liability for chargebacks or fraud.” Reverse that and you *are* the fraud desk.  
2. **Malaysia is not a weekend money-transmitter.** Bank Negara licensing, AML/CFT, safeguarding of client money, audit, capital — a different corporation.  
3. **Chargebacks and FPX disputes** become our P&L. A solo founder cannot underwrite a creator’s card-testing ring.  
4. **Tax identity explodes.** MoR means *we* are the seller in every country the buyer sits. That is Paddle’s entire company. It also **steals the LHDN identity** from the merchant.  
5. **PCI.** Vaulting cards is a continuous compliance product. We wrap Stripe; we do not become Stripe.  
6. **Support becomes cash-out support.** “Where is my payout” is the most expensive ticket in payments. BYOK makes that Billplz’s ticket.  
7. **We would fight our own rails.** Billplz/CHIP/Stripe are how we exist. Competing with them is how we lose FPX.

Solo-founder math: one BNM query or one month of card-testing can erase a year of SaaS seats. The ledger exists so we can *describe* money, not so we can *hold* it.

#### What we do instead

- Tenant pastes **their** keys.  
- Money → tenant merchant account.  
- We sell software + metered compliance/dunning credits.  
- Tracker cells for rails stay **W**, not Y-we-acquired.

#### Partner to name

**Billplz / CHIP / HitPay / Stripe / (later) Xendit** for acquiring. **Paddle or Polar** if a creator wants MoR *elsewhere*. We will never be that partner.

#### Tracker / ADR lock

- ADR 019 §2, Payments module “never build a gateway,” tracker **`LP-002`**, **`LP-003`**, **`LP-007`**, **`LP-095`**, **`LP-120`**.  
- Kill phrase: “Let’s take 2% like HitPay/Fresha to grow faster.” That is a bank, not a checkout engine.

---

### R5 — Full ERP / accounting suite (Xero replacement)

#### What we refuse

- General ledger for **expenses, payroll statutory, fixed assets, inventory COGS layers, bank feeds, supplier bills** as a first-party accounting product  
- Replacing Xero, QuickBooks, AutoCount, SQL Accounting, or a bookkeeper’s Excel  
- Multi-entity consolidation, management accounts, board packs  
- “Lazuar Books”  
- Building a chart of accounts designer, bank reconciliation UI, or GST return *inside Pay as an accounting suite*

Not refused (these are the **keep**):

- **Double-entry ledger of *our* checkouts** (cash, fee, gross, tax, deferred revenue when that epic exists)  
- Tax liability **as it arises at the sale** (SST line, LHDN document linkage)  
- **Xero / QBO sync** (ADR 021 keep) — *export/journal push*, not a replacement (`LP-121`)  
- CSV / journal export a bookkeeper can import **before** Xero exists (`LP-097`)  

#### Why competitors have it

| Competitor | Why they grow books |
|------------|---------------------|
| **Xero / QuickBooks / AutoCount** | Accounting *is* the product. Bank feeds + accountant channel. |
| **Shopify / Square / StoreHub** | Once they own the sale, they want the close. Switching cost becomes infinite. |
| **Stripe Sigma / Revenue Recognition** | Developers already trust the rail; reports upsell. |
| **Chargebee (partial)** | Revenue recognition / SaaS metrics sit next to billing engines; still not a GL. |

They have it because **the CFO/accountant is a veto vote** (ADR 020 said this clearly). The correct response to a veto is **sync**, not “we will be Xero in two quarters.”

#### Why copying kills solo-founder scale

1. **Accounting software is a 20-year company.** Bank feeds alone are a partnership marathon.  
2. **ADR 021 keep line is sync.** Replacing Xero contradicts the decision that made compliance CaaS viable: we own *the sale and the tax filing at the sale*, then hand the book to the system of record accountants already use.  
3. **Scope explosion:** AP, payroll (EPF/SOCSO/EIS), assets, inventory costing, multi-currency revaluation, audit trails accountants will swear on.  
4. **We will be wrong.** A slightly wrong checkout ledger is a bug. A slightly wrong general ledger is a professional-negligence product.  
5. **Billing module already states what it is not:** not a gateway, not access control, not Xero.

Solo-founder math: Xero’s partner API + a correct journal mapping is months. A Xero clone is a career.

#### What we do instead

- Make `billing.LedgerLines` **true** (fees not always zero; refunds honest).  
- Ship **Xero sync** when the ledger is trusted (ADR 021 keep; currently zero code — this is a *delay of a keep*, not a refuse).  
- Until then: accountant-grade **CSV**.  
- Honest MRR from the ledger (`LP-161`) is metrics, not ERP.

#### Partner to name

**Xero** (primary, named in 021). QuickBooks if the tenant is already there. **AutoCount / SQL Accounting / a human bookkeeper** for MY SMEs who will never touch Xero. StoreHub only if they are already a retailer.

#### Tracker / ADR lock

- ADR 021 keep Xero; Billing README golden rule.  
- Tracker: **`LP-206`** Wave **R** (replacement). **`LP-121`** Wave **4** (sync).  
- Kill phrase: “Accountants want us to be their only system.” They want a feed they can trust. Give them a feed.

---

### R6 — POS hardware (drawers, printers, scanners, terminals we OEM)

#### What we refuse

- Selling or certifying cash drawers, receipt printers, barcode scanners, customer-facing displays, or PIN pads as **Lazuar hardware**  
- ESC/POS driver farms, OPOS, vendor-specific SDKs as a product line  
- Becoming Qashier / Square Terminal / StoreHub / HitPay POS hardware  
- “Lazuar Tap” card reader  
- Bundling an acquiring terminal to lock processing (Toast/Square/HitPay play)

Not refused:

- Printing via **browser print / PDF**.  
- Telling the tenant to put a **Qashier/StoreHub/iMin** device on the counter.  
- Aura’s software till is **Aura’s** problem (System C). Pay does not grow a till to “help Aura.”

#### Why competitors have it

| Competitor | Why hardware exists |
|------------|---------------------|
| **Square / Toast** | Hardware is the land grab. Software can be cheap because interchange pays. |
| **Qashier / StoreHub** | MY/SG SME counter. Once the drawer is screwed under the desk, switching cost is physical. |
| **HitPay** | Tap-to-pay / POS kits exist to lock SG/MY SMB volume onto their MID. |
| **Stripe Terminal** | Same play, developer-shaped. |

They have it because **atoms lock accounts harder than bits**, and because acquiring margin subsidizes the kit.

#### Why copying kills solo-founder scale

1. **Supply chain, RMA, warranty, import, GST on goods, dead pixels.** A software founder becomes a distributor.  
2. **Driver hell.** Every Android box and Epson variant is a ticket.  
3. **Collides with BYOK.** Hardware POS companies need *their* rail. We refuse to be the rail (R4).  
4. **Malaysia already has incumbents.** Qashier/StoreHub/HitPay will always be better at drawers than a checkout engine.  
5. **007 README already forbids POS.** Cloning HitPay’s hardware because they sit in a competitor column is the exact failure mode `00-evaluation` §3 warns about.

Solo-founder math: one firmware bug in a drawer is a field visit. We do not do field visits.

#### What we do instead

- Hosted checkout + Billplz/CHIP/Stripe hosted pages.  
- Desk money never calls Pay.

#### Partner to name

**Qashier** for counters that want a terminal. **StoreHub** if they are actually retail/F&B. **HitPay POS** if they already bought it — we still do not become it. **iMin / generic Android POS** if they already own one. We do not resell.

#### Tracker / ADR lock

- Tracker: **`LP-202`** Wave **R**.  
- Kill phrase: “HitPay and Square do hardware and payments; we should too.” They are banks with a card reader. We are a cash register with a tax engine.

---

### R7 — CRM marketing automation / email blasts / campaign clouds

#### What we refuse

- Tenant **Campaigns, Automations, Message log** as a product  
- Meta WhatsApp Cloud **marketing** + credit packs + blast UI  
- Mailchimp-class: lists, segments, drip creative, A/B subject lines, revenue attribution dashboards  
- In-app two-way inbox (buyer↔merchant chat)  
- “AI receptionist” / chatbot as a growth suite  
- Lead-gen form builders (Typeform clone) and **viral giveaways** (KingSumo) — ADR 021 explicit kill  
- Pipedrive-style **Pipeline** as a first-party sales CRM  
- Pixel + journey builder + “win-back studio”  
- First-party **SMS** product (`LP-156`)  
- Growing thin `ClientProfile` into HubSpot (`LP-168`)

Not refused:

- **Transactional** messages that protect the sale: receipts, magic links, dunning (email now; WhatsApp when the **keep** is real).  
- CRM **profiles** (email, phone, TIN, anonymization) — the thin table that already exists.  
- Template bodies for those transactional jobs (`LP-152`).  
- Human `wa.me` / mailto.

#### Why competitors have it

| Competitor | Why marketing cloud exists |
|------------|----------------------------|
| **Kajabi / Kit / Mailchimp / Klaviyo** | Email *is* the product. They tax sends or seats. |
| **HubSpot** | Inbound company. CRM is a wedge for marketing hub. |
| **HitPay** (partial) | SMB suite adds reminders and light CRM to keep the merchant in-app. |
| **Paddle / Chargebee** (partial) | Dunning emails exist; some grow “campaigns” language. Still not Mailchimp. |
| **KingSumo / Gleam** | Viral acquisition as SKU. |
| **Historical Lazuar Broadcast + Giveaway + Form + Pipeline** | ADR 014 retention/acquisition story. |

They have it because **audience ownership is a second product** with its own pricing (sends, contacts, WhatsApp conversation-window fees). It is also how they become the system of record for “people,” not just “payments.”

#### Why copying kills solo-founder scale

1. **ADR 021 killed giveaways and lead-gen forms by name.**  
2. **Aura already paid to delete Channels.** Rebuilding from Pay Communications leftovers is how ghost engines fire at 2am.  
3. **WhatsApp marketing is a compliance product** (Meta policy, opt-in, 24h window, template review). It is not a delight feature.  
4. **Deliverability is a team** (SPF/DKIM/DMARC, list hygiene, spam complaints).  
5. **Asia’s real channel for informal sellers is the owner’s personal WhatsApp.** Software that tries to *own* that inbox loses to the phone.  
6. **Broadcast as Mailchimp recreates the CMS trap** (template builders, image hosting, preference centers).  
7. **SMS in Malaysia is a telco product** with sender-ID registration and content rules. Not a weekend channel.

Solo-founder math: Klaviyo’s *only* job is this. We would be a worse Klaviyo and a worse MyInvois submitter.

#### What we do instead

- Receipts + dunning + (later) WhatsApp **utility** template for failed pay.  
- Profiles with consent flags (PDPA-minded, not a CDP).  
- Outbound webhooks so *they* can dump buyers into Kit.

#### Partner to name

**Kit, Mailchimp, Resend audiences, or the owner’s WhatsApp.** **KingSumo / Gleam** for giveaways. **Tally / Typeform** for forms. **HubSpot / Pipedrive** if they are actually a B2B sales team.

#### Tracker / ADR lock

- ADR 021 kills; tracker **`LP-157`**, **`LP-156`**, **`LP-168`** Wave **R**.  
- Do not launder blasts through `LP-074` / `LP-155`. Those are recovery/transport.  
- Kill phrase: “HitPay/Kajabi have campaigns.” They are a suite or a course company. We are a cash register.

---

### R8 — Course platforms and membership sites as first-party apps

#### What we refuse

- Academy: modules, lessons, video player, drip, quizzes, certificates, comments (`ADR 014` Academy)  
- Membership **site** (logged-in content area, paywall of pages, community+course combo)  
- Hosting video (encoding, CDN, watermarking, piracy cat-and-mouse) as a product  
- “Kajabi / Teachable / Thinkific / Skool inside Pay”  
- Rebuilding **Vault** as a Gumroad storefront (file locker + product page + versioning + license keys UI) — module already removed  
- First-party **software license server** (Keygen clone) as a reason to become an LMS-adjacent DRM company

Not refused:

- **Selling** a course or membership **as a Commerce product** (price, recurring, coupon, dunning).  
- **Fulfillment** = webhook / signed URL / “here is your Kajabi invite.”  
- Buyer portal that shows **what they paid for** and a link — not a player (`LP-170`–`LP-172`).

#### Why competitors have it

| Competitor | Why the player exists |
|------------|-----------------------|
| **Kajabi / Teachable / Thinkific / Maven** | Course experience *is* the SKU. High ARPU, high support. |
| **Skool** | Course + community as a single habit loop. |
| **Gumroad / Podia / Whop** | File delivery + simple paywall; they take GMV. |
| **Memberstack / Circle** | Gate + site, billing via Stripe. |
| **Polar** (partial) | Digital product entitlements sit next to MoR checkout. |

They have it because **content hosting creates daily engagement** (and daily tickets: “module 4 won’t play on Safari”). Billing is the unsexy half; they use it to tax the sexy half.

#### Why copying kills solo-founder scale

1. **Video is a media company.** Storage, transcoding, players, mobile, resumes, speed checks, DRM watermarks.  
2. **ADR 019 redefined apps as thin wrappers; ADR 022 then deleted Vault.** The remaining honest shape is webhook-after-pay.  
3. **Academy was priority 4 in a dead catalog.** It fails ADR 021’s transaction-or-compliance test (the *sale* passes; the *player* does not).  
4. **Support is pedagogical.** “I can’t find lesson 7” is not a ledger bug.  
5. **Piracy support** (shared logins, screen capture) is endless and off-mission.

Solo-founder math: Teachable already lost to Kajabi and Skool with hundreds of people. We will not win on video.

#### What we do instead

- Checkout + subscription + dunning + LHDN.  
- `https` fulfillment target.  
- Optional signed R2 URL **as a primitive** (presigned upload already moved toward One) — **not** a storefront, not a product page builder, not a versioned Gumroad.

#### Partner to name

**Kajabi, Teachable, Thinkific, Skool, Circle, Whop, or a custom Next.js course they already have.** **Keygen.sh / Cryptlex** if they sell desktop software — *they* integrate; we fire the webhook.

#### Tracker / ADR lock

- ADR 014 Academy/Vault historical; 019 thin wrapper; 021/022.  
- Tracker: **`LP-205`** Wave **R**.  
- Kill phrase: “Creators need a place to put the course.” They have one. They need a place to **collect MYR and file LHDN**.

---

### R9 — Crypto settlement as a near-term product

#### What we refuse *now* (near-term refuse)

- First-party USDC/USDT RPC checkout, BTCPay-in-our-cloud, Coinbase Commerce as a marketed rail in 2026–2027 CaaS  
- “Pay with Bitcoin” on the default checkout  
- Treasury, on-ramp, off-ramp, or holding tokens  
- Using crypto to *skip* LHDN classification work  
- Marketing Web3 as a pillar while LHDN UI is still hidden

Delay *possible* only after: MY BYOK + dunning + LHDN-on-MY are boring, a named tenant has a real cross-border volume problem, and we still do **not** hold keys for them (point to BTCPay they host). Even then it is a **partner adapter**, not a chain company. Until a new ADR reverses this, the tracker stays **R**.

ADR 021 pillar 3 mentioned USDC/Web3 as *one* way to do cross-border rails. That sentence is **not** a 2026 build order. Cross-border *tax classification* (zero-rated export, `LP-119`) is the compliance job; the rail can stay Stripe international cards for a long time.

#### Why competitors have it

| Competitor | Why crypto appears |
|------------|-------------------|
| **BTCPay Server / Coinbase Commerce** | Niche: chargeback-phobic digital goods, donors, special communities. |
| **Stripe / Polar** (experiments / niche) | Marketing to nomads; rarely the P&L. |
| **ADR 020 Phase 3** | Aspirational borderless story. |

They have it because **a loud minority of internet merchants ask**, and because “zero chargebacks” is a good slide. Volume in MY SME / typical digital business is tiny next to FPX.

#### Why copying kills solo-founder scale (especially *now*)

1. **Key management is a bank.** If we host wallets, we are a custodian. If we do not, we are a docs page for BTCPay — which is the partner answer.  
2. **Chain ops:** reorgs, stuck txs, wrong network (USDT-TRC20 vs ERC20), price volatility during the invoice window, sanctions screening.  
3. **LHDN still wants MYR tax documents.** Crypto does not remove UBL. It adds FX valuation arguments.  
4. **ADR 023 said time-to-market beats the ultimate moat.** Crypto is the opposite of TTM.  
5. **Reputation:** a compliance CaaS that leads with USDC looks like a toy to the accountant who must file MyInvois.

Solo-founder math: one mis-attributed USDT payment is an irrecoverable support nightmare. Billplz bill IDs are boring. Boring is the product.

#### What we do instead

- FPX / cards / (later) more SEA gateways.  
- Ledger already claims “Bitcoin looks the same to the ledger” as *architecture* — that is a **journal shape**, not a rail.  
- Cross-border: Stripe international + correct **zero-rate tax codes** when pillar 3 is real (`LP-119` Wave 4).

#### Partner to name

**Self-hosted BTCPay** or **Coinbase Commerce** if a tenant insists — they paste a fulfillment/confirmation webhook. We do not run nodes.

#### Tracker / ADR lock

- ADR 020 Phase 3; 021 pillar 3 aspiration; 023 TTM.  
- Tracker: **`LP-207`** Wave **R**. Reopen only with a written reverse, not a wave sneak.  
- Kill phrase: “Pillar 3 says USDC.” Pillar 3 says **export tax classification**. The rail is replaceable.

---

### R10 — Connecting as a Stripe competitor (acquiring, Connect, “Lazuar Cards”)

#### What we refuse

- Marketing Pay as “Stripe for Southeast Asia” in the **acquiring** sense  
- Stripe **Connect platform** economics (application fees, destination charges we control, instant payouts we fund)  
- Issuing cards, bank accounts, capital, or “Lazuar Balance”  
- Underwriting merchants (KYB as *our* money program)  
- Competing with Stripe, Adyen, Billplz, HitPay, or Xendit on **who touches the card scheme / FPX switch**  
- Building a second webhook universe because we became the processor  
- Using Aura Connect UX as a wedge to become the acquirer  
- Cloning Xendit xenPlatform

Not refused:

- A **Stripe adapter** (BYOK). Tenant’s Stripe account (`LP-041`).  
- Speaking Stripe-like **developer language** (HMAC webhooks, checkout sessions) as a *software* UX — without being the money mover.  
- Competing with Stripe **Billing + Tax** on *SEA compliance and FPX orchestration*, which is ADR 019’s actual claim.  
- **Wrapping** Xendit later as a rail (`LP-045` Wave 4, mark W when it exists).

#### Why competitors have it

| Competitor | Why they are the acquirer |
|------------|---------------------------|
| **Stripe** | Developer acquiring + issuing + treasury. The API *is* the bank. |
| **Xendit / HitPay / Billplz / CHIP** | Licensed local/regional acquiring / e-money. |
| **Stripe Connect platforms** (Shopify, marketplace SaaS) | They sit on Stripe and skim. Still money-program ops. |

They have it because **they spent a decade on licenses, banking partners, and fraud ML**. “Connect” is not a weekend OAuth screen; it is an underwriting factory.

#### Why copying kills solo-founder scale

1. **Same as R4**, plus a specific ego trap: our docs already say “sovereign checkout.” Sovereignty is **orchestration + ledger + tax**, not BIN sponsorship.  
2. **Connect support** is “my connected account is restricted.” That is Stripe’s worst queue. We cannot staff it.  
3. **We would fight our own partners.** Billplz/CHIP are how we exist in MY. Competing with them is how we lose FPX.  
4. **`00-evaluation` is explicit:** Xendit is a licensed gateway we should **wrap**, not clone.

Solo-founder math: Stripe’s Malaysia coverage is still the reason Billplz exists. Our job is to **compose** them, not replace them.

#### What we do instead

- Best **BYOK orchestration** + idempotent webhooks + ledger + LHDN.  
- Honest Connect for **Aura tenants pasting keys**, not platform onboarding to *our* MIDs (`LP-143` is provision, not acquiring).

#### Partner to name

**Stripe** (global cards). **Billplz / CHIP** (MY FPX). **Xendit** later as a wrap. Never “Lazuar Acquiring.”

#### Tracker / ADR lock

- ADR 014 Payments: “Never build a gateway — wrap existing ones.” ADR 019 BYOK.  
- Tracker: **`LP-003`**, **`LP-007`**, rail rows marked **W**.  
- Kill phrase: “We’re already taking payment; we should be Stripe.” We are taking *metadata and tax*. They are taking *money*.

---

### R11 — Adjacent refuse that people will try to sneak back via ADR 014

These are not always in the tracker’s section N, but **will appear in competitor matrices**. They are refuse for the same reasons.

#### R11a — Viral giveaways and lead-gen form builders

ADR 021 kill, named KingSumo. Gleam, KickoffLabs, Typeform, Tally, Google Forms exist. Acquisition vitamins. Partner: KingSumo / Tally. Do not add a Giveaway family to the tracker.

#### R11b — Calendly-as-Pay (horizontal scheduler)

Pay is not a meeting product. Aura is a vertical salon OS **and a Hub customer**. Building Consult as a first-party Calendly clone inside Pay recreates 15 apps. Partner: Calendly / SavvyCal.

#### R11c — Intercom/Crisp tenant helpdesk

Platform support tickets for *us* helping tenants can exist. A tenant-facing Intercom clone (canned responses, satisfaction CSAT product, knowledge base CMS) is a vitamin. Partner: Crisp, Intercom, email.

#### R11d — Affiliate networks and mass payouts as a *money* product

Tracking `?ref=` on a checkout is a thin later maybe (`LP-210` is Wave 4 **delay**). **Wise MassPay / paying 500 affiliates from a pooled account** is R4. Partner: Rewardful / Tolt + Wise the *tenant* operates.

#### R11e — B2B BNPL / “we advance the creator”

Capchase / Pipe / Funding Societies are **lenders**. Underwriting 5-figure invoices is a balance sheet. Tracker **`LP-039`** Wave **R** (BNPL as our product). Processor-hosted Atome on a Billplz page is *their* UI, not ours to name or reconcile.

#### R11f — National KYC as *our* identity product

Singpass / MyDigital ID / Aadhaar are **government programs** with audits and liability. Using them someday to prefill TIN (ADR 020) is a delay at most. Building a KYC bureau is refuse. **`LP-007`** already refuses KYC-for-our-acquiring. Partner: the government’s own button, or a licensed e-KYC vendor, never our store of biometrics.

#### R11g — Multi-vertical superapp in this monorepo

ADR 021. Tuition, courts, clinics, F&B, gyms “because checkout is reusable.” Reuse is a lie that creates a Calendly. Aura salon OS stays a **customer**, not a module we grow into Pay.

#### R11h — Medspa EMR / gym OS

Wrong vertical. Pay must not grow “HIPAA mode” or class packs to win a Zenoti RFP.

#### R11i — Custom domain marketing sites and wildcard SSL on portal

ADR 016. Competitors (Shopify, Kajabi) do this with huge DevOps. We send `creator.com` to Cloudflare Pages. Distinct from `LP-017` (checkout hostname).

#### R11j — SMS product

Tracker **`LP-156`** Wave **R**. Telco relationships, sender IDs, content filtering. Transactional email + later WhatsApp utility cover the keep jobs.

#### R11k — Stripe Tax / Avalara global remittance

Tracker **`LP-120`** Wave **R**. That is MoR/tax-engine-for-the-world. Our tax job is **LHDN (and later other *government e-invoice* authorities)**, not calculating 100 countries of VAT as the seller.

---

## Delay catalog

Delay items are **not Wave R**. Treating them as Wave 0–2 is still a lie. Each has a **gate**. Until the gate is true, the tracker wave stays **4** or **—**, and sales copy must not claim them.

---

### D1 — Escrow for high-ticket B2B (Phase 2, delayed)

#### What it is

A checkout option that routes a large invoice through **Escrow.com** (or similar): funds secured, inspection period, then release. ADR 021 pillar 2 named buyer hesitation on $10k links. ADR 020 §5 described a “Pay with Escrow” button.

#### Why competitors / wishlist have it

High-ticket consulting, agency retainers, and micro-M&A do not fit card rails (limits, chargeback windows, trust). Escrow.com exists because that market is real. Chargebee-class CPQ sometimes grows “collect then release” language; it is still not our MVP.

#### Why it is delay, not refuse

It **facilitates a transaction** (ADR 021 test). It does not require a CMS, a marketplace, or us holding funds **if** we broker to Escrow.com and never operate the escrow account ourselves.

#### Why it is not now

- Vault-holding-the-asset story is dead (022).  
- We do not have a trusted LHDN B2B UI yet (023). Escrow without a tax invoice is half a product.  
- Escrow.com integration is a state machine (create transaction, fund, inspect, disburse, dispute) plus support.  
- ICP of current GTM is FPX + subscriptions + MY tax, not $15k SaaS transfers.

#### Gate

1. Pure CaaS checkout is loved (Wave 0–1 loops closed).  
2. ADR 023 reverse: TIN + quote + tax invoice download work (Wave 2).  
3. A **named** high-ticket tenant asks in writing.  
4. Design: **no Lazuar-held funds**; no Vault revival; fulfillment = webhook + “release” event.

#### Partner

**Escrow.com** (named in 020). Not a first-party escrow license.

#### Tracker

**`LP-208`** Wave **4**. The checklist already says delay, not refuse. Do not pull it into Wave 1.

---

### D2 — Embedded e-sign (Phase 2, delayed)

#### What it is

Checkout presents an MSA/NDA; signature captured; then pay. ADR 020 §6; ADR 021 pillar 2 “merge legal and financial workflows.” Bundled with escrow on `LP-208`.

#### Why competitors have it

PandaDoc, DocuSign, Dropbox Sign, and some CPQ tools win enterprise because legal and finance are the same deal. High-ticket B2B *does* stall on “email the PDF, wait, then send Stripe.”

#### Why delay, not refuse

It facilitates a **transaction** (unblock pay) and can stay a **partner embed** (their iframe, their certificate of completion, our session id).

#### Why not now

- Signature law, certificate storage, and “what if they sign but don’t pay” are product work.  
- Most MY ICP (creators, agencies on FPX) do not need this to close RM 50–500 checkouts.  
- Building our own PKI for *contracts* (as opposed to LHDN XML we already do) is a second cryptography product.

#### Gate

Same as D1, plus: do **not** write a first-party sign engine. Embed PandaDoc or Dropbox Sign. Store only envelope id + signed-at on the session.

#### Partner

**PandaDoc or Dropbox Sign** (DocuSign if the tenant already is).

#### Tracker

**`LP-208`** (same row as escrow). Not Wave 0–2.

---

### D3 — Multi-country tax beyond LHDN (GSTN, Coretax, InvoiceNow)

#### What it is

ADR 020 Phase 1 listed India GSTN IRP, Indonesia DJP Coretax, Singapore IMDA InvoiceNow as if they were current. README still diagrams `LHDN / GSTN`. Code: **LHDN only**.

#### Why competitors / docs have it

E-invoicing is rolling across Asia. A “Compliance CaaS for Asia” slide wants a map. StoreHub and some ERPs already market multi-country tax because they have offices and partners there. Chargebee/Stripe Tax look “global”; they are **not** GSTN.

#### Why delay, not refuse

The **job** (government e-invoice at the point of sale) is exactly our identity — **in another jurisdiction**. It is not a vitamin. Distinct from `LP-120` (Avalara/Stripe Tax remittance), which is refuse.

#### Why copying *now* would kill solo-founder scale

1. **Each authority is a product.** Different auth, schemas (UBL vs JSON IRP), sandboxes, legal entities, QR rules, cancellation windows, consolidation rules. LHDN alone is a module with XSDs and SDKs.  
2. **ADR 023 hid LHDN UI** so we could ship checkout. Opening GSTN while Malaysian tenants cannot click “Tax Invoice” is theatre.  
3. **Gap analysis already said:** “Do **not** claim multi-country tax until GSTN/Coretax are real modules.”  
4. **Support languages, legal advice, and on-call for three tax authorities** is an NGO.

#### Gate

1. LHDN **UI un-hidden** and used by real MY tenants (023 reverse + soak). Tracker: MyInvois rows `LP-110`–`LP-116` become sellable, not just `B`.  
2. B2C consolidation and B2B TIN paths are boringly correct.  
3. A **named** IN or ID tenant with volume, or a partner who owns that authority’s legal relationship.  
4. New module (not a pile of `if country==`). New sandbox. New SDK decision.

Until then, **do not** add Wave 1–2 work for GSTN “parity.” Do not put GSTN in README as a live capability.

#### Partner

Local **GST Suvidha Provider** (India), Indonesian tax platforms, or the tenant’s ERP. We are not their interim filing company.

#### Tracker

**`LP-209`** Wave **4**. Checklist note already: waits until MyInvois is a sold feature.

---

### D4 — Xero / QuickBooks *sync* (keep, delayed implementation)

This is **not** refuse. It is the most important **keep that is not built**.

#### Gate

Ledger fees/refunds honest; document numbers stable; a finance epic owns mappings. Billing README already parks `RevenueRecognitionJob` until an owner exists.

#### Refuse boundary

See R5. Sync ≠ replacement (`LP-121` vs `LP-206`).

#### Tracker

**`LP-121`** Wave **4**. May become a Pay implementation program after Wave 2 invoices exist.

---

### D5 — Additional SEA gateways (Xendit, Fiuu, Midtrans, Cashfree, SenangPay)

#### Why delay

ADR 020 lists them. Code has Stripe, Billplz, CHIP, Razorpay. Each adapter is a week of happy path and a month of webhook misery (ADR 004 is a post-mortem of exactly this). `00-evaluation` says wrap Xendit later, do not clone xenPlatform.

#### Gate

MY rails (Billplz/CHIP) production-true (Wave 0 money loops). Then add a gateway when a **named tenant** cannot take money without it. Do not pre-build India/Indonesia acquiring to look “regional.”

#### Tracker

**`LP-045`** Xendit adapter Wave **4**. **`LP-032`–`LP-036`** extra MY methods Wave **4** (usually appear on the *processor* page, not as our acquiring). **`LP-044`** Razorpay/Curlec deepen for e-mandate Wave **4**.

---

### D6 — WhatsApp as *dunning channel* (keep, not a blast product)

#### Why this sits in delay *implementation* rather than refuse

ADR 021 keep. Currently `ConsoleMessagingService`. Building **Meta Cloud transactional templates** for failed-pay recovery is allowed and desired.

#### Refuse boundary

The moment the UI grows audiences, campaigns, or credit packs for marketing, it becomes R7 (`LP-157`).

#### Gate

Email dunning variables actually fill (`LP-073`, `LP-153`); then one WhatsApp utility template; credits deduct for real; no blast list.

#### Tracker

**`LP-074`**, **`LP-155`** Wave **4**. Do not mark **Y**. Do not file under marketing.

---

### D7 — Keygen / software license fulfillment

Thin fulfillment (call Keygen on pay/cancel) can be a **later adapter**. First-party license server is R8-adjacent refuse.

#### Gate

Developer ICP actually using Pay M2M APIs (Wave 1 DX: `LP-132`–`LP-137`). Partner: **Keygen.sh**.

---

### D8 — National digital ID *prefill* (not a KYC bureau)

Singpass / MyDigital ID as a **checkout prefill for legal name + TIN** could help pillar 2. Operating a biometric KYC store is R11f refuse. `LP-007` stays R.

#### Gate

LHDN B2B UI live; official sandbox access; no local copy of government identity beyond what the invoice needs.

---

### D9 — ADR 023 reverse (LHDN / B2B UI)

Not a competitor clone. **Our own moat, hidden on purpose.**

#### Gate

Checkout + dunning cash-flow validated (Wave 0–1); then un-hide invoicing, legal profile, TIN, quotes, tax invoice download (Wave 2: `LP-022`, `LP-102`–`LP-106`, `LP-110`–`LP-116`, `LP-122`). Backend is largely waiting.

#### Tracker

Wave **2**, mark **B** until un-hidden and sold. Not Wave R.

---

### D10 — Affiliate *attribution* (not payouts)

`?ref=` on checkout + commission **accrual in ledger** might later exist. Mass payout (R11d) stays refused as a money program if *we* send the money.

#### Gate

Ledger trusted; tenant pays affiliates themselves (export + Wise).

#### Tracker

**`LP-210`** Wave **4**. Checklist already: delay, not refuse, stay off MVP.

---

### D11 — Other Later items that are *not* refuse (do not steal their waves)

For completeness, so refuse-list readers do not accidentally R them:

| ID | Job | Why it is not refuse |
|----|-----|----------------------|
| `LP-017` | Checkout custom domain | Hostname for the register, not a site builder |
| `LP-018` | Overlay/embed checkout | CaaS UX, still our checkout |
| `LP-054`–`LP-063` | Trials, pause, proration, seats | Billing depth (Wave 3) |
| `LP-091`–`LP-093` | Refunds | Money honesty |
| `LP-119` | Export zero-rate | Pillar 3 tax codes, not USDC |
| `LP-138` | Official Payments SDK | DX, not a vitamin |

Order-bump (`LP-015`) and abandoned-checkout mail (`LP-016`) sit on the **edge** of R7. They stay Wave 3 only if they remain *one transactional job* (one extra SKU, one reminder). The day they grow a funnel builder, they become `LP-201`.

---

## Adjacent lookalikes (do not confuse with the product)

These are the sentences that trick a solo founder into un-refusing.

| Lookalike someone will pitch | Sounds like | Actually is | Verdict |
|------------------------------|-------------|-------------|---------|
| “Markdown description on checkout” | Harmless | CMS trap (015) | Refuse |
| “Tiny bio page so the link looks legit” | Marketing hygiene | Link-in-bio app | Refuse (`LP-201`) |
| “Telegram notify on pay” | Fulfillment | Fine as **webhook / their bot** | Thin OK; bouncer refuse (`LP-204`) |
| “Member area” | Portal | Course/community site | Refuse player (`LP-205`); keep receipt portal |
| “Wallet” | Credits | If it’s `TenantCreditBalance` for *our* meters: OK (`LP-005`). If it’s *their* sales proceeds: R4 | Split carefully |
| “Invoices” | Xero | If LHDN tax invoice: keep (Wave 2). If AP/AR suite: `LP-206` | Split |
| “POS” | Hardware | Software mark-paid is Aura System C. Drawer: `LP-202` | Split |
| “WhatsApp” | Dunning | Recovery template: keep. Blast: `LP-157` | Split |
| “Marketplace of templates” | ADR 018 lite | Still two-sided content | Refuse (`LP-203`) |
| “Connect” | Stripe | Key paste / Aura provision: keep. Underwriting: `LP-003`/`LP-007` | Split |
| “Multi-currency” | Global | FX + tax codes: delay (`LP-096`/`LP-119`). Crypto: `LP-207` | Split |
| “AI ops chat” | Modern | Hibernating Ops module; not a builder, not a CRM | Do not productize to dodge refuse |
| “Any app cashier” | CaaS maturity | Fine as **M2M checkout** (`LP-136`). Not as Hub marketing before Wave 0 proof | Honesty, not a new app |
| “HitPay has a store” | Table-stakes | Acquirer suite | Refuse (`LP-200`) |
| “Paddle does VAT everywhere” | Compliance | MoR remittance | Refuse (`LP-002`/`LP-120`) |
| “Custom domain” | Shopify | Marketing host vs checkout host | `LP-201` vs `LP-017` |

---

## Partner instead

This is the sales and docs cheat-sheet. One job → one default partner. Naming a partner is **not** an integration promise.

| Job the prospect asks for | We say | Default partner | Why this partner |
|---------------------------|--------|-----------------|------------------|
| Pretty landing page / funnel | We are the buy button | **Framer** or **Webflow**; **Carrd** if cheap; **Astro on Cloudflare Pages** if they can deploy | They are design tools; ADR 015 mitigation |
| Instagram one link | Put our checkout URL on a bio tool | **Linktree** or **Beacons** | Commodity; do not rebuild |
| Online store + inventory | We sell the checkout | **Shopify / EasyStore / StoreHub** | HitPay-store envy is a trap |
| Forms / quizzes | Not our app | **Tally** or **Typeform** | ADR 021 kill |
| Viral giveaway | Not our app | **KingSumo** or **Gleam** | ADR 021 kill |
| Course player / drip / certs | We sell the seat | **Kajabi / Teachable / Skool** | R8 |
| File delivery storefront | We can webhook a signed URL; we are not Gumroad | **Gumroad** or their own R2 + site | Vault removed |
| Community + kick/ban | Webhook on cancel | **Zapier/Make** + their bot; or **Circle / Whop / Skool** | R2, 021, 022 |
| Software license keys | Webhook | **Keygen.sh** | D7 |
| Email newsletters / blasts | Transactional only here | **Kit** or **Mailchimp** | R7 |
| WhatsApp marketing | No | Their **WhatsApp Business** phone | R7 |
| Two-way inbox | No | **WhatsApp** itself | `wa.me` |
| SMS | No | Their telco / Twilio they operate | `LP-156` |
| Helpdesk | We are not Intercom | **Crisp** / **Intercom** | R11c |
| Accounting / year-end | We sync later; we are not books | **Xero** (keep); else **AutoCount** + bookkeeper | R5 / D4 |
| Supplier bills / stock | No | **StoreHub** or their accountant | ERP |
| Cash drawer / printer / terminal | No | **Qashier** / **StoreHub** / **HitPay POS they already own** | R6 |
| Card acquiring / FPX | BYOK | **Stripe** / **Billplz** / **CHIP** / **HitPay** / later **Xendit** | R4, R10 |
| I don’t want to be the merchant | We won’t be either | **Paddle** or **Polar** / **Lemon Squeezy** | MoR is their company; warn LHDN break |
| Marketplace demand | We don’t rank you | **Google / Shopee / Instagram** | R3 |
| Marketplace split pay | We don’t hold funds | **Xendit xenPlatform / Stripe Connect** — they go there, we do not become it | R3+R4 |
| High-ticket trust | Later, partner | **Escrow.com** | D1 |
| Sign then pay | Later, embed | **PandaDoc** / **Dropbox Sign** | D2 |
| India GST e-invoice | Not until MY is real | Their **GSP / CA** | D3 |
| Indonesia Coretax | Same | Local tax vendor | D3 |
| Crypto | Not now | **BTCPay** they host | R9 |
| Affiliate payouts | We may track later; we don’t send money | **Rewardful/Tolt** + **Wise** | `LP-210` |
| BNPL | Processor page only | **Billplz Atome / Grab PayLater** or **Funding Societies** | `LP-039` |
| National ID | Not our bureau | **MyDigital ID / Singpass** official | D8 / R11f |
| Horizontal scheduling | We are not Calendly | **Calendly** | R11b |
| Salon floor OS | That’s Aura, a Hub customer | **Aura** | Plane split |
| Global VAT remittance | We file MY e-invoice for *you* as seller | **Paddle** if they want MoR; **not us** | `LP-120` |
| Subscription engine depth (proration, usage) | Later our Commerce; or they graduate | **Chargebee / Stripe Billing** if they outgrow us | Wave 3, not refuse |

### How to say no without sounding incomplete

Recommended sentence for docs and sales:

> Lazuar Pay is the cash register, ledger, dunning, and Malaysian e-invoice engine. We do not host your website, community, course, marketplace, or bank account. We connect to the tools that already do those jobs, and we make sure the money and the tax document are true.

If a prospect leaves because they wanted Kajabi or HitPay’s store, that is **correct ICP filtering** (ADR 021 trade-off). `00-evaluation.md` already says no single incumbent owns take-money + subscriptions + LHDN + webhooks. That remainder is enough.

---

## How this constrains the tracker

This is the section `00-checklist-tracker.md` and `20-sequencing-and-tracker-schema.md` must obey when someone tries to “add the missing Pay features from Gumroad/Paddle/Kajabi/Stripe/HitPay.”

### Principle 0 — Competitor `Y` is not a gap if the job is Wave R

If `07-merchant-of-record.md` says “Paddle is MoR and we are not,” the cell is **Y for them, R for us**, not a Wave 1 ticket.

**Theirs / Later** is forbidden on trap rows. Later implies temptation.

Gap score (if `00-evaluation` or `20` ever computes one) **excludes** Wave **R** and **W** (wrap) and **—**.

### Principle 1 — Historical ADRs cannot mint waves

| Document | May create tracker rows? |
|----------|--------------------------|
| ADR 015, 019, 021, 022, 023 | Yes — as **R** or as **keep/delay** with gates |
| ADR 014 app catalog | **No** new Wave 1–3 rows |
| ADR 018 marketplace | **No** wave; `LP-203` stays R |
| ADR 020 Phase 2/3 | Only `LP-208`/`LP-209`/`LP-210`/`LP-121`/`LP-045`/`LP-074` as Wave 4 with **gates**; bouncer/crypto-now/MoR as R |

A PR that says “implement Academy because 014 priority 4” is malformed.

### Principle 2 — This tracker is Pay-centric; Aura is a customer

The living matrix is **Lazuar Pay vs Billplz / CHIP / HitPay / Xendit / Stripe / Paddle / Chargebee / Polar**.

- Do **not** import Aura salon rows (rooms, walk-in, commission, `/book`).  
- Do **not** use Aura’s need for a till as permission for `LP-202`.  
- `LP-143` Connect/provision is **integration**, not “become Aura.”

### Principle 3 — Money planes stay tagged

Any new row that touches money must declare: SaaS fee (A) vs buyer→merchant (B) vs desk (C).

- Rows that mix A and B (`LP-002` take-rate MoR, $0 SaaS + processing) stay R.  
- Pay utility credits (`LP-005`) are **software metering**, not plane B settlement.  
- Rail rows are **W** (wrap), never “we acquired.”

### Mapping: this catalog → living checklist IDs

The checklist already stamped most of these. This file is the **rationale**. Do not invent a parallel taxonomy.

| This file | Tracker ID | Wave | Notes |
|-----------|------------|------|-------|
| R1 builders | `LP-200`, `LP-201` | R | Checkout skin `LP-025` and host `LP-017` are *not* these rows |
| R2 community DRM | `LP-204` | R | Webhooks `LP-132` remain keep |
| R3 marketplace | `LP-203` | R | |
| R4 MoR / hold funds / acquirer | `LP-002`, `LP-003`, `LP-007`, `LP-095` | R | `LP-120` Avalara/Stripe Tax is the remittance cousin |
| R5 Xero replacement | `LP-206` | R | Sync is `LP-121` Wave 4 |
| R6 hardware POS | `LP-202` | R | |
| R7 marketing / SMS / HubSpot CRM | `LP-157`, `LP-156`, `LP-168` | R | Thin ClientProfile stays; do not grow it |
| R8 course / membership CMS | `LP-205` | R | Commerce product + webhook is not this row |
| R9 crypto near-term | `LP-207` | R | `LP-119` zero-rate is not crypto |
| R10 Stripe-competitor acquiring | `LP-003`, `LP-007`, W on `LP-030`–`LP-046` | R / W | |
| R11e BNPL as us | `LP-039` | R | Processor page may still show Atome |
| D1/D2 escrow + e-sign | `LP-208` | 4 | Delay, off MVP |
| D3 GSTN/Coretax | `LP-209` | 4 | After MyInvois sold |
| D4 Xero sync | `LP-121` | 4 | Keep |
| D5 extra rails | `LP-032`–`LP-036`, `LP-044`, `LP-045` | 4 | Wrap |
| D6 WhatsApp dunning channel | `LP-074`, `LP-155` | 4 | Keep; stub; not `LP-157` |
| D9 023 reverse | `LP-022`, `LP-102`–`LP-116`, `LP-122` | 2 | Mark **B** until sold |
| D10 affiliate attribution | `LP-210` | 4 | Delay; no pooled payouts |

### Promotion rules (research → row)

When `01`–`20` or a later dossier wants a new capability:

1. Run the **four tests** (transaction-or-compliance, solo-founder, money-plane, two-sided).  
2. If it matches a Wave **R** ID above → stop. Competitor `Y` is evidence they are a different company.  
3. If it matches a Wave **4** delay → copy the **gate** into the note. No gate, no earlier wave.  
4. If it is a thin fulfillment of a refused app (webhook to Kajabi) → it is **not** `LP-205`. It is webhook hygiene (`LP-132`).  
5. If ADR 014 is the only citation → reject the row.  
6. Never assign Waves 0–2 to `LP-200`–`LP-207`, `LP-002`, `LP-003`, `LP-039`, `LP-095`, `LP-120`, `LP-156`, `LP-157`, `LP-168`.  
7. Changing R → 4 requires **owner-ratified reverse of the cited ADR**, written in a new ADR, not a Slack vibe.  
8. `LP-208`/`LP-210` may stay 4 forever. They do not automatically become Wave 2 when LHDN UI ships.

### Scoring and evaluation constraints

`00-evaluation.md` must not say:

- “We lag Kajabi on courses.”  
- “We lag HitPay on store + POS.”  
- “We lag Paddle on MoR / global VAT.”  
- “We lag Xero on accounting.”  
- “We lag GSTN because README mentioned it.”  
- “We lag Stripe because we do not acquire.”

It **may** say:

- “We lag our own ADR 021 keep: Xero sync, WhatsApp dunning channel, LHDN UI.”  
- “We lag Billplz-class *honesty* of fulfillment and refunds.”  
- “We lag Commerce M2M APIs and webhook retries for developers.”  
- “We lag Chargebee on proration/seats (Wave 3), which is still our category.”

Those are **keeps, hygiene, and billing depth**, not vitamins.

### What implementers are allowed to want (so this file is not nihilism)

A solo founder reading a refuse list can feel like the product shrank to nothing. The remainder is still a company:

| Allowed desire | Why it is not a trap |
|----------------|----------------------|
| Dead-accurate BYOK checkout | R4/R10 say wrap, not ignore, payments |
| Idempotent webhooks + public event catalog | ADR 019 developer promise; Wave 0 |
| Dunning that actually recovers revenue | 021 keep; Wave 0 |
| WhatsApp **utility** template for failed pay | Keep, not `LP-157` |
| LHDN UBL that submits and comes back with QR | The moat; 023 only hid UI |
| B2C consolidation on the 28th | Pillar 1 |
| B2B TIN validate + instant invoice | Pillar 2 (surface delayed) |
| Ledger that balances including real fees | Billing golden rule |
| Utility credits that meter LHDN/WA | 019 monetization |
| Xero journal push | 021 keep |
| Escrow/e-sign **later** as embeds | `LP-208` |
| Aura using Hub for guest money | Customer, System B |

If a feature is not in that table and not a gated delay, default **refuse**.

### Header flags this file requires on the living tracker

Keep (or restore if someone “cleans up”) on `00-checklist-tracker.md`:

| Flag | Value |
|------|--------|
| Marketplace / multi-vendor | **R** (`LP-203`) |
| Pay as MoR / acquirer | **R** (`LP-002`, `LP-003`) |
| Community / Vault / bouncer | **Killed** (022) / **R** (`LP-204`) |
| Website builder / link-in-bio | **R** (`LP-200`, `LP-201`) |
| Course / membership site first-party | **R** (`LP-205`) |
| POS hardware | **R** (`LP-202`) |
| Marketing cloud / SMS | **R** (`LP-157`, `LP-156`) |
| Crypto rail | **R** (`LP-207`) |
| GSTN / Coretax claim | **Do not claim** (`LP-209` Wave 4) |
| Xero replacement | **R** (`LP-206`) |
| Xero sync | **Wave 4 keep** (`LP-121`) |
| LHDN UI | **Hidden (023)** — Wave 2, mark B — not refused |
| WhatsApp dunning channel | **Keep / stub** — Wave 4 — not a campaign product |
| Escrow / e-sign / affiliates | **Delay Wave 4** (`LP-208`, `LP-210`) |

### Anti-goals for the tracker itself

1. Do not grow a “15 apps” section to “complete the matrix.”  
2. Do not score Paddle MoR features as table-stakes gaps.  
3. Do not score HitPay Store / POS as table-stakes gaps.  
4. Do not use leftover Community TypeSpec fields as evidence the app is Later.  
5. Do not file “any SaaS cashier” marketing before Wave 0 money loops.  
6. Do not mark `LP-209` as P0 because a competitor in India has GSTN.  
7. Do not treat missing Astro templates as permission for a hosted CMS.  
8. Do not reopen Vault to make escrow or file delivery “complete.”  
9. Do not pull `LP-208` into Wave 2 “because invoices are legal.” Quotes are Wave 2; escrow is still Phase 2.  
10. Do not change `LP-039` from R to 4 because Billplz’s hosted page shows Atome. That is the processor’s SKU.

### Worked examples (so verdicts stay consistent)

| Request | Wrong verdict | Right verdict |
|---------|---------------|---------------|
| “Gumroad has a product page builder” | Wave 1 | **R** `LP-200` + `LP-205` |
| “Skool kicks unpaid members” | Wave 4 Phase 2 | **R** `LP-204` |
| “Lemon/Paddle/Polar handles VAT as MoR” | Table-stakes gap | **R** `LP-002` — partner them; warn LHDN |
| “HitPay has tap-to-pay” | Wave 2 POS | **R** `LP-202` |
| “Kajabi has email sequences” | Wave 3 CRM | **R** `LP-157` |
| “README says GSTN” | Wave 2 tax | **Wave 4** `LP-209` after MyInvois sold |
| “021 says Xero” | R (confused with `LP-206`) | **Wave 4 keep** `LP-121` |
| “021 says escrow” | Wave 1 B2B | **Wave 4** `LP-208` after 023 |
| “021 says USDC” | Wave 1 global | **R** `LP-207` |
| “We need Connect like Stripe” | Wave 1 payments | **R** if acquiring (`LP-003`); **keep** if paste Billplz keys |
| “Un-hide tax invoice button” | Competitor gap | **Wave 2** our ADR reverse, mark B until sold |
| “Chargebee has proration” | Refuse (over-reading this file) | **Wave 3** — still our category |
| “Add Xendit” | Clone xenPlatform | **Wave 4 wrap** `LP-045` |

---

## Source index

### ADRs (absolute)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/014-apps.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/015-avoiding-the-cms-trap.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/016-platform-domain-strategy.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/017-portal-frontend-architecture.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/018-marketplace-and-structured-content-strategy.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/020-lazuar-platform-integration-roadmap.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/022-remove-community-vault-modules.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`  

### Pay code / honesty

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` — watermark + Phase 1–3 wishlist  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/20-architecture-intent-vs-implementation.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/README.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/` — live module list  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/hooks/use-product-associations.ts` — ADR 022 stub  

### This program

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/README.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-evaluation.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/20-sequencing-and-tracker-schema.md`  

---

## Close

Lazuar once wrote itself a fifteen-app future (ADR 014), a marketplace sequel (ADR 018), and a three-phase integration novel (ADR 020). Then it wrote the corrections: **no CMS** (015), **CaaS not builders** (019), **compliance or nothing** (021), **Community/Vault deleted** (022), **hide the tax UI until the cash register ships** (023).

Competitors will continue to show website builders, Telegram bouncers, Discover tabs, MoR checkouts, Xero clones, hardware kits, Mailchimp, Kajabi players, USDC buttons, GSTN maps, HitPay stores, and “we’re Stripe.” Those screenshots are **true**. They are also **other companies**.

**Refuse** the vitamins and the bank. **Delay** escrow, e-sign, and foreign tax authorities until Malaysia is real. **Keep** the dumb-pipe gateways, the ledger, the dunning job, the LHDN XML, and the Xero *sync*. **Partner** everything that is a presentation layer, a chat platform, a lender, or an OEM.

The tracker stays honest when a `Y` in someone else’s column can still be a **Wave R** in ours — and when ADR 014 cannot open a wave.

---

**Document status:** Complete uncondensed analysis for program `007-feats` subagent 19. No product code. Next editor: keep `LP-200`–`LP-207` and `LP-002`/`LP-003` on Wave **R**; do not promote `LP-208`/`LP-209`/`LP-210` ahead of their gates.
