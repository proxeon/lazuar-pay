# 20 — Sequencing and tracker schema

**Program:** Competitor-feature tracker for **Lazuar Pay** (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`).  
**File role:** Constitution for the living checklist. Defines **rows** (feature IDs), **columns** (competitors), **cell/status vocabulary**, **waves**, **rubric**, and the **implement-later** rule.  
**Not:** a commitment to ship any row.  
**Not:** an implementation plan, ticket list, or replacement for `plans/001-backend/001-backend-solidification-checklist.md`.  
**Not:** a rewrite of ADRs. ADR 021 and ADR 023 remain product law.  
**Sibling analyses:** `01`–`19` of this program may still be landing. This file does **not** wait on them. It gives them a stable ID space and a promotion rule into `00-checklist-tracker.md`.  
**Date:** 2026-08-16.  
**Status:** Full uncondensed analysis — schema + seed catalog + sequence. **No product code. This folder is tracker only.**

**Depends on (do not re-litigate):**

| Source | What it freezes for this schema |
|--------|----------------------------------|
| [`docs/001-gaps/00-what-we-need-to-do-next.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/00-what-we-need-to-do-next.md) | Three audiences (humans / integrators / gateways). Close money + machine-integration loops before adding modules. Phases of meaning A–D. Success criteria without asterisks. Open decisions D1–D5. |
| [`docs/architecture-decision-log/021-compliance-caas-pivot.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md) | Lazuar is exclusively a Compliance-First Checkout Engine. Kill vitamins (giveaways, community DRM, link-in-bio). **Keep** WhatsApp dunning and Xero. Three tax pillars (B2C consolidation, B2B TIN+instant invoice, cross-border zero-rated). |
| [`docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md) | Ship Pure CaaS first. Hide LHDN/B2B UX with `[MVP-HIDE]`. Backend dark matter stays. Compete meantime on Billplz/FPX + recovery. Reverse is un-comment, not rebuild. |
| [`docs/architecture-decision-log/019-checkout-as-a-service-pivot.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md) | Headless CaaS. BYOK, not Merchant of Record. Prepaid utility wallet (do not tax GMV). Outbound HMAC webhooks as the integrator unlock. |
| [`docs/architecture-decision-log/022-remove-community-vault-modules.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/022-remove-community-vault-modules.md) | Community/Vault are killed, not postponed. Do not re-add Telegram bouncers or first-party file vault as competitor matching. |
| Root `README.md` product-truth watermark | Shipping product = ADR 021 + 023. ADR 014/020 are historical ambition. Honest capability: BYOK + commerce subs + ledger + email dunning + LHDN **backend**. WhatsApp and full compliance UI are not guaranteed demoable. |
| [`plans/001-backend/001-backend-solidification-checklist.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md) | Phases 0 / A / B / C are **in-repo complete with residuals**. Phase D (WhatsApp, dunning intelligence, LHDN re-surface, Xero, extra rails) is **not** started. D3/D4/D5 already decided (platform keys in One; multi-endpoint webhooks; Commerce integrator v1 = webhooks + public checkout, not full M2M admin). |
| Gap reports `docs/001-gaps/01`–`20` | Evidence for seed `Ours` values. When a later `01`–`19` *of this competitor program* disagrees with a seed cell, **the new report wins** and the tracker is patched. This file’s seed is a starting catalog, not frozen archaeology. |
| [`plans/007-feats/README.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/README.md) | This folder is the living tracker. Implementation happens later. BYOK, not MoR. Do not sell WhatsApp dunning or LHDN as live until loops are closed / un-hidden. Aura is a customer of Hub, not a competitor column. |

If a later `01`–`19` file names a capability this catalog missed, **add a row** with the next free ID in that category. Do not invent a second taxonomy.

---

## Method

This file answers one product-ops question:

> How should Lazuar Pay turn competitor research into a **living checklist** a small team can sequence — without copying Stripe’s company shape, Paddle’s Merchant-of-Record economics, Chargebee’s enterprise billing surface, Billplz’s “dumb pipe” ceiling, or the 15-app super-app we already killed?

It was written on 2026-08-16 **without** sibling reports `01`–`19` of this competitor-feature program. Those reports are expected to fill **cells** and deepen **domain sequence**. They must not invent a second ID scheme. Ground truth for “what we actually have” was taken from the Lazuar Pay repo plus the documents in the table above.

### What was read (this subagent)

1. Direction narrative: `docs/001-gaps/00-what-we-need-to-do-next.md` in full.  
2. Product law: ADR 021 (Compliance CaaS), ADR 023 (UI lobotomy), ADR 019 (CaaS pivot), ADR 022 (Community/Vault kill), ADR 020 (integration wishlist, watermarked as non-shipping).  
3. Intent-vs-code: `docs/001-gaps/20-architecture-intent-vs-implementation.md`, plus the openings of gap reports 01 (dunning), 05 (billing), 06 (payments), 07 (commerce), 09 (LHDN).  
4. Solidification checklist: Phases 0–D, including residuals that are honesty notes rather than blockers.  
5. Live surfaces: `lazuar-ops` routes + sidebar (Commerce / Developer / Workspace; invoicing **unrouted**), `lazuar-portal` checkout / success / portal / update-payment / pay-session `notFound()`, `lazuar-developers` hub cards, Payments adapters (Stripe, Billplz, CHIP, Razorpay) + `IntegrationEndpoints` M2M cashier, Messaging `ConsoleMessagingService` with `Messaging:WhatsAppEnabled` default false, Billing README (ledger live; deferred-revenue job parked; Xero absent).

### What a row is allowed to be

A row is allowed only if **at least two** of these are true:

1. A Malaysian / SEA founder, agency, or SaaS integrator can describe the job in one breath (“when the card fails, WhatsApp them an FPX link”; “submit one consolidated e-invoice on the 28th”).  
2. At least one named competitor (including the Informal stack) has a shippable version of the job.  
3. Lazuar already has a slice, a `[MVP-HIDE]` island, a stub, or an explicit reject of that job.

Rejected as rows (put them in notes, not IDs):

- Implementation tasks (“add unique index on `ChargeAttemptLogs`”). Those belong in `plans/001-backend` or a later impl program.  
- Pure visual craft (“softer checkout radius”). Track craft in a UI program.  
- Architecture wishes (“split Commerce into a microservice”). Frozen.  
- Marketing slogans (“AI-powered billing OS”). If there is no merchant, buyer, or integrator job, there is no row.  
- Duplicate names for the same job. “WhatsApp” is not a row. `LP-MSG-003` real Meta Cloud send, `LP-MSG-004` interactive pay buttons, `LP-DUN-002` campaign step type `WHATSAPP`, and `LP-XX-004` “rebuild a WhatsApp inbox / CRM” are four different rows.

One job = one row. If a later report wants to split a row (for example inbound webhook verify vs business-key idempotency), **keep the parent ID** and mint a child only when the jobs can ship on different waves or have different rubric classes. Prefer adding `LP-PAY-013` over turning `LP-PAY-005` into a folder.

### Relationship to other files

```
01 Lazuar inventory (when it lands) ──┐
02 Local/SEA landscape                │
03 Global landscape                   │
04–09 per-competitor                  ├─► promote evidence
10–19 per-domain                      │         │
                                      │         ▼
20 THIS FILE (schema) ────────────────┴─► 00-checklist-tracker.md
                                                  │
                                                  ▼
                                           00-evaluation.md
```

Expected sibling filenames in this folder (from `plans/007-feats/README.md`; do not invent a second ID space when they land):

| # | File | Role vs this constitution |
|---|------|---------------------------|
| 01 | `01-lazuar-feature-inventory.md` | **Ours authority** once it exists |
| 02 | `02-local-sea-competitor-landscape.md` | Loc / Inf column membership |
| 03 | `03-global-competitor-landscape.md` | Str / MoR / Sub column membership |
| 04 | `04-stripe.md` | Cells under **Str** |
| 05 | `05-malaysia-gateways.md` | Cells under **Loc** (Billplz, CHIP, Fiuu, SenangPay, …) |
| 06 | `06-sea-fintech-platforms.md` | Notes / later rails; do not add a seventh column without a constitution edit |
| 07 | `07-merchant-of-record.md` | Cells under **MoR** |
| 08 | `08-subscription-billing-engines.md` | Cells under **Sub** |
| 09 | `09-checkout-and-payment-link-ux.md` / checkout report | **UX** / **COM** depth |
| 10 | `10-lhdn-einvoice-competitors.md` | Cells under **Acc** + **TAX** rows |
| 11–18 | lifecycle, dunning, rails, DX, invoicing, messaging, dashboard, pricing | Domain sequence *inside* a wave |
| 19 | `19-refuse-list-and-adjacents.md` | Must agree with `LP-XX-*`; cannot delete a trap row |
| 20 | this file | Schema authority |

- This file is the **schema authority**.  
- `00-checklist-tracker.md` is the **only** place a row becomes official for scoring.  
- `00-evaluation.md` is written **after** the 20 analyses, using the tracker — not the other way around.  
- `plans/001-backend/001-backend-solidification-checklist.md` is the **engineering residual authority** for work already sequenced as Phase 0–D. Tracker waves **align with** that meaning (A ≈ Wave 0, B ≈ Wave 1, D.3 ≈ Wave 2, billing completeness ≈ Wave 3, D.1/D.4 extras ≈ Wave 4) but use **product-competitor language**, not phase-letter checkboxes.  
- Do **not** copy Phase 0–C completed checkboxes into the tracker as “to-do.” Seed `Ours = shipped` and leave the row in its thematic wave so we do not regress and so competitor cells still have a home.

### Failure modes this schema is designed to prevent

| Failure | What it looks like | How the schema blocks it |
|---------|--------------------|--------------------------|
| **Screenshot backlog** | 200 “Stripe has it” rows, no sequence | Rubric class + wave required before a row can be `later` |
| **MoR envy** | “Take 5% and file tax as MoR to match Paddle” | Trap family `LP-XX` + `refuse` + ADR 019 BYOK lock |
| **Vitamin relapse** | Link-in-bio, giveaways, Telegram bouncer, course LMS “because Kajabi has it” | ADR 021 kill list + `LP-XX` Never |
| **Claim inflation** | README says WhatsApp dunning / Xero / Fiuu because the roadmap does | `Ours` cannot be `shipped` if the path is console-log, unrouted, or absent. WhatsApp today is `absent` (flag off + `ConsoleMessagingService`). LHDN UI is `backend-only`. Xero is `absent`. |
| **UI-lobotomy amnesia** | Rebuilding invoicing from scratch in Wave 2 | Wave 2 is **un-hide + trust**, not greenfield. `[MVP-HIDE]` islands stay. |
| **Builder theater** | Redesigning the dunning campaign UX while vaulted failures never enter `PAST_DUE` | Wave 0 owns the closed loop. Dunning intelligence is Wave 4 (`LP-DUN-006/007/010`). |
| **Swagger as developer platform** | More Scalar tabs without keys + signed webhooks + guides | Wave 1 `DEV` rows. Ops OpenAPI is not a competitor feature. |
| **Score inflation** | Chargebee “wins” 40 rows we Never wanted | Gap score **excludes** `refuse` and `n/a` |
| **Money-plane collapse** | Mixing merchant GMV, utility credits, and (if anyone proposes it) Lazuar’s own SaaS fee | Every money row tags a plane (see Principles). |
| **Impl leaking into tracker** | PRs against this folder that add code, SQL, or “just a small fix” | Standing rule: **tracker only**. Implementation happens in a later, separately kicked-off program. |
| **Aura gravity** | Treating salon OS / Fresha / guest-booking as Lazuar Pay competitors | Aura is a **customer** of Hub. No salon column. No `GB-` / `PY-` IDs. |

### Honesty snapshot used for seed `Ours` values (16 August 2026)

This is the seed, not a scorecard. Patch it when `01` lands.

| Claim people still make | Seed truth |
|-------------------------|------------|
| Multi-gateway BYOK | **partial / shipped slice:** Stripe, Billplz, CHIP, Razorpay adapters exist. Fiuu, SenangPay, Xendit, Midtrans, Cashfree **absent**. Billplz fee extraction is **0**. |
| Automated dunning | **partial:** campaign builder + hourly engine + catch-up + past-due-on-failure + recovery-on-success + email path exist in-repo (Phase A). WhatsApp path is **absent** as a product channel. Campaign snapshot / decline-code intelligence **absent**. |
| Inbound payment webhooks | **partial → near shipped:** verify, persist, business-key idempotency, failed-event publish, structured success logs (Phase A/C). Two-phase raw-intake and a single support “payment timeline” UI still **absent**. |
| Outbound customer webhooks | **partial:** multi-endpoint, event filters, HMAC, fan-out without URL-match bug (Phase B). Residuals: redrive API, secret rotate, test ping, payload richness, SSRF lock, LHDN still on a separate fire-and-forget sender. |
| Platform API keys | **partial / shipped slice:** One-owned credentials, live/test, one-time reveal, scopes, Ops UI, Integration policies (Phase B). Rotate/expiry/last-used/IP allowlists **absent**. |
| Commerce M2M | **partial:** Payments cashier `POST /integrations/payments/checkouts` exists. Commerce **product/subscription admin M2M** deferred (D5). |
| LHDN | **backend-only** for the merchant: submit/poll/SDKs/keys/taxpayer validate/consolidation job exist; ops invoicing + billing profile + portal TIN + tax-invoice download are `[MVP-HIDE]`. |
| Double-entry ledger | **partial / shipped slice** for sold happy paths (Phase C). Deferred revenue **parked**. MRR as a first-class metric **absent**. Xero **absent**. Commerce GMV chargeback ledger still out of scope. |
| WhatsApp dunning | **absent** as a channel (`IMessagingService` → `ConsoleMessagingService`; `Messaging:WhatsAppEnabled` defaults false; Decision 00.4 freezes product work). Orchestration step type exists — that is **not** a shipped channel. |
| Customer portal | **partial:** magic-link list + hard cancel. No payment-method update, no invoice history, no cancel-at-period-end, tax invoice hidden. Stripe customer-portal generation exists for Stripe-only tenants. |
| Checkout conversion | **partial:** hosted product checkout, coupons, quantity, phone/address flags, success page **polls server status** (does not blindly trust redirect). No BM i18n, no bank-logo FPX theater, no custom domain, TIN hidden. |

---

## Principles

These are **locks**. A tracker row that contradicts them is malformed.

### P1 — This folder is tracker only (implement later)

- `plans/007-feats` (this program) **does not implement product code**.  
- No SQL, no C#, no TypeSpec, no frontend, no “tiny honesty fix” PRs against this folder except edits to markdown tracker files.  
- A row moving from `absent` → `partial` in the tracker is a **claim that code elsewhere changed**, evidenced by a path + date in `Notes` / `Src`. It is not permission to start coding from the tracker.  
- Implementation, if ever, is a **new program** (for example a future `plans/008-…`) kicked off explicitly, after `00-evaluation.md` names the wave.  
- Completing this file, or filling `00-checklist-tracker.md`, is **not** a kickoff.

### P2 — ADR 021 is the company; ADR 023 is the GTM clock

- If a feature does not **facilitate a transaction** or **keep the merchant legally compliant**, default verdict is `refuse` unless it is strictly required to *sell or operate* the CaaS slice (developer keys, outbound webhooks, ops console, email delivery).  
- The three tax pillars are real product, not a slide. They are **Wave 2** to *show*, because ADR 023 hid them on purpose. They are not Wave 0 rebuilds.  
- Keep list is short: **WhatsApp dunning** and **Xero**. Both are Wave 4 unless a later evaluation pulls WhatsApp earlier for a specific ICP. Wave 0’s job for WhatsApp is **honesty** (do not market a console logger).  
- Kill list is enforced as `LP-XX-*` refuse rows: viral giveaways, community DRM / Telegram-Discord bouncers, website / link-in-bio builders, first-party Vault/LMS, marketplace take-rate.

### P3 — Three audiences, three auth models, three surfaces

From `00-what-we-need-to-do-next.md`. Mixing them is how the backend stayed mushy.

| Audience | Auth | Surface | Tracker implication |
|----------|------|---------|---------------------|
| **Humans** (ops, portal, superadmin) | Cookies / JWT | `lazuar-ops`, `lazuar-portal`, `lazuar-admin` | UX / OPS / COM rows. JWT is not an integrator story. |
| **Integrators** (ERP, SaaS backend, Zapier, custom Next) | Long-lived **keys created in our UI** | Documented integration APIs + **outbound webhooks** | DEV rows. “Paste a user JWT into Zapier” is `refuse`. |
| **Gateways and vendors** (Stripe, Billplz, Resend, MyInvois) | Signed **inbound** webhooks + BYOK secrets | Payments / Messaging / Lhdn adapters | PAY / MSG / TAX inbound quality is the bar outbound must meet. |

### P4 — Closed loops before flexibility

The direction doc is explicit: a flexible campaign builder on a broken entry path is theater.

Wave 0 asks: *does the money loop finish?* Failed renewal → past due → message and/or auto-charge → pay or suspend/cancel → ledger, subscription, and metrics agree.

Wave 1 asks: *can a stranger integrate and a stranger convert on FPX without us in the call?*

Wave 2 asks: *can we un-hide the moat without lying about invoices?*

Wave 3 asks: *does billing match what global buyers think “Stripe Billing” means?*

Wave 4 asks: *do we earn the keep-list and extra rails, or keep refusing them?*

### P5 — Honesty is a feature

`Ours` values are allowed to be embarrassing. They are not allowed to be aspirational.

- `shipped` requires a **demoable** path on a current deploy, for the audience the row names.  
- Code behind `[MVP-HIDE]` is **`backend-only`**, never `shipped`.  
- A step type in the dunning builder that logs to console is **`absent`** as a messaging channel (`LP-MSG-003`), even if `LP-DUN-002` (builder) is `partial`.  
- README Phase 1 lists (Fiuu, Xendit, GSTN, Coretax, Xero, native WhatsApp) do **not** mint `shipped` rows.  
- Manual e2e residuals in the solidification checklist do **not** block `shipped` if in-repo tests + operators can run the path; they **do** block marketing sentences that say “production-proven at scale.”

### P6 — BYOK, not Merchant of Record; credits, not GMV tax

Every money-adjacent row **must** carry an implicit plane (write it in `Notes` when ambiguous):

| Plane | Who pays whom | Processor | Tracker rule |
|-------|---------------|-----------|--------------|
| **G. Merchant GMV** | Buyer → merchant | Tenant BYOK (Stripe / Billplz / CHIP / Razorpay / later rails) | Never score “Lazuar takes 2% like Lemon Squeezy” as a gap. MoR is `refuse`. |
| **U. Utility credits** | Merchant → Lazuar | Prepaid `TenantCreditBalance` (LHDN submit, WhatsApp send, top-up) | Do not dual-post top-ups as GMV. Do not invent WA credit SKUs before WA exists. |
| **S. Lazuar SaaS fee** | Merchant → Lazuar (if/when billed) | Not Hub GMV; not guest money | Out of scope for this CaaS tracker unless a later commercial ADR exists. Do not put “Pro on Billplz” here. Aura System A (Paddle) is **Aura’s** SaaS fee, not a Lazuar Pay competitor row. |

Mixing planes is Trap T4.

### P7 — Inbound webhook quality is the outbound bar

ADR 004/009 already taught us not to ACK-and-drop inbound money. Outbound to customer apps is the weak twin. Wave 1 treats outbound webhooks as **product infrastructure**, not a fulfillment URL textbox. Silent “URL must match exactly or nothing happens” is a product bug (fixed in Phase B; do not regress; still score residuals: redrive, rotate, rich payloads).

### P8 — TypeSpec describes what ships

Phantom UI against imaginary APIs is either implemented or removed. Product-scoped docs show **integration surfaces**, not the modular monolith and not Ops chat. A “developers hub” row is not satisfied by another Scalar tab of internal routes.

### P9 — Do not expand Phase 2/3 while A–C meaning is unfinished

Explicit non-goals (direction doc + ADR 020 watermark + solidification “out of scope”):

- Escrow, e-sign, community bouncers, multi-country tax beyond LHDN, marketplace.  
- Rebuilding a 15-app super-app.  
- Optimizing dunning UX flexibility until the recovery loop is honest (Wave 0 before `LP-DUN-006`).  
- Treating more Scalar tabs as a developer platform without keys + webhooks + guides.

These are `refuse` or Wave-none until an ADR explicitly reverses 021/023.

### P10 — Seed vs authority after siblings land

- `01` (inventory) becomes the **Ours authority**. This file’s seed `Ours` is patched, not defended.  
- `02` / `03` become the **column authority**. If they rename or drop a competitor column, the parent updates `00-checklist-tracker.md` header and this file’s column section in the same change.  
- `04`–`09` fill **cells**. They do not add columns without an edit here.  
- `10`–`19` may recommend a wave or split a job. They do not mint IDs. Parent mints IDs using §ID scheme.

---

## ID scheme

### Format

```
LP-<CAT>-<NNN>
```

- `LP` = **Lazuar Pay**. Stable prefix so this catalog never collides with Aura (`GB-001`, `PY-001`) or with backend phase tags (`phase-a/…`).  
- `<CAT>` = one of the nine categories below.  
- `<NNN>` = zero-padded integer, unique **inside the category**, starting at `001`. Never reuse. Never renumber. Gaps are allowed.

Spoken short form in conversation may drop the prefix (`PAY-001`), but the tracker cell and any future impl program **must** use the full `LP-PAY-001`.

Do **not** use:

- `LP-001` without a category (unsearchable; waves will scramble domains).  
- Per-competitor IDs (`STRIPE-014`). Competitors are columns, not rows.  
- Phase letters (`LP-A-001`). Phases are engineering history; waves are product sequence.

### Categories

| Cat | Meaning | Home modules / apps | Typical Wave home |
|-----|---------|---------------------|-------------------|
| **PAY** | Money movement: BYOK rails, inbound webhooks, off-session, refunds, disputes, fees, sandbox | `Modules/Payments`, ops Payment Gateways | 0 (loops), 1 (FPX polish), 4 (more rails) |
| **COM** | Sellable catalog and entitlements: products, checkout sessions, subscriptions, coupons, plan changes, portal billing jobs | `Modules/Commerce`, portal checkout | 1 (sellable), 3 (completeness) |
| **DUN** | Recovery engine: entry, campaigns, retries, pause, metrics, intelligence | Commerce dunning + Payments off-session + Communications | 0 (closed loop), 4 (intelligence) |
| **TAX** | Government e-invoice and legal documents: LHDN, TIN, quotes, consolidation, signing | `Modules/Lhdn`, Billing consolidation, hidden invoicing UI | 2 (un-hide), refuse multi-country until then |
| **DEV** | Integrator product: keys, scopes, outbound webhooks, docs, SDKs, M2M | One credentials, One dispatcher, `lazuar-developers`, TypeSpec | 1 (sellable CaaS), 4 (OAuth / full M2M) |
| **UX** | Buyer-facing conversion and honesty: checkout craft, success, branding, i18n, domain | `lazuar-portal` | 0 (honest success), 1 (conversion), 4 (custom domain) |
| **OPS** | Merchant console jobs: lists, KPIs, settings, support timeline, DLQ visibility | `lazuar-ops` | 0 (support truth), 1 (sellable console) |
| **MSG** | Channels and templates: email, WhatsApp, suppressions, delivery log | Communications + Messaging | 0 (email honesty), 4 (real WA) |
| **TRU** | Financial and platform truth: ledger, credits, isolation, outbox, MRR, Xero | Billing, BuildingBlocks, tests | 0 (don’t lie), 3 (MRR), 4 (Xero) |
| **XX** | Traps / refuse. Not in the first 80. Must still appear in the tracker so they cannot be “rediscovered.” | — | no wave |

### Numbering rules

1. First 80 rows below occupy `001`–`N` per category as listed.  
2. Next ID in a category is `max(existing)+1`. Never fill a hole left by a killed row — mark the hole `refuse` or leave a one-line tombstone.  
3. When `01`–`19` need a new job, parent appends; subagents request, they do not mint.  
4. Reserved overflow (do not use until needed): `LP-PAY-013+`, `LP-COM-013+`, `LP-DUN-011+`, `LP-TAX-011+`, `LP-DEV-011+`, `LP-UX-009+`, `LP-OPS-007+`, `LP-MSG-007+`, `LP-TRU-007+`, `LP-XX-001+`.

### Status vocabulary

The six words the program asked for live on **the Lazuar depth column** (`Ours`). They are **not** used in competitor cells.

| `Ours` | Meaning | Demo test | Typical next move |
|--------|---------|-----------|-------------------|
| **shipped** | The job exists end-to-end for the named audience on a current deploy. No asterisk in sales. | “Show me in ops/portal/docs without opening Git.” | Keep; regression-watch |
| **partial** | A real slice exists, but the loop, honesty, UX, or operability hole is large enough that we must not sell it as done. | “I can click it, then I have to explain.” | Same wave until the hole closes |
| **backend-only** | API, job, or ledger path works (or is substantially built); merchant/buyer UI is hidden, unrouted, or never built. ADR 023 islands live here. | “I can curl it / watch the job; I cannot click it in production nav.” | Wave 2 if TAX; else the wave that un-hides it |
| **absent** | Not in the repo, or a stub that cannot be demoed (console logger, parked job, empty adapter). | “There is nothing to click and nothing safe to curl.” | Build only if wave + rubric allow |
| **refuse** | We will not build this. Competitor having it is not a gap. | — | `W = —`, `Class = trap` |
| **later** | Valid job, explicitly parked past the current open wave (usually Wave 3–4 or beyond). Not a trap. | — | Do not pull forward without re-scoring |

**Disambiguation (mandatory):**

- `backend-only` vs `partial`: hidden invoicing is `backend-only`. Visible dunning builder whose WhatsApp step does not send is: builder `partial`, WhatsApp channel `absent`. Do not mark the builder `backend-only`.  
- `absent` vs `later`: `absent` is a fact about the repo. `later` is a **decision** about sequence. A row can be `Ours=absent` and `V=Later` and `W=4` at once.  
- `refuse` vs `later`: `refuse` is company-shape. `later` is time. Escrow is `refuse` until an ADR reverses 021’s “not vitamins / not Phase 2 while solidifying.” Custom domain is `later` (Wave 4), not `refuse`.  
- Do **not** put `later` or `refuse` in a competitor cell. Those are Lazuar decisions.

### Layer A — competitor fact (per cell)

| Cell | Meaning | Fill rule |
|------|---------|-----------|
| **Y** | Competitor has a production-grade, marketed version of this job | Help center, pricing page, or operator interview |
| **P** | Competitor has a slice, add-on, plugin, or awkward path | Note the slice in `Notes` the first time it matters |
| **N** | Competitor does not have it in-product | Absence from help + category mismatch |
| **—** | Not applicable to this competitor’s category | e.g. Informal × OAuth2; Billplz × Xero journals |

If a later parent prefers the compact marks already sketched in `00-checklist-tracker.md` (`B` for backend-only, `R` for refuse, `W` for wrap-as-rail), those marks may appear **only** as aliases of the vocabulary above:

| Compact alias (optional) | Canonical `Ours` / cell |
|--------------------------|-------------------------|
| `B` | `Ours=backend-only` |
| `R` | `Ours=refuse` and/or `V=Never` |
| `W` | Not a status. Means “this capability is a **rail we wrap**, not a product we rebuild.” Put `W` in `Notes` or a dedicated rail flag — do not replace `Y/P/N`. |

The constitution’s canonical `Ours` words remain: shipped / partial / backend-only / absent / refuse / later.

### Layer C — row verdict (`V`)

After cells + rubric, the parent sets one verdict for Lazuar:

| `V` | Meaning |
|-----|---------|
| **Ours** | We have it at `shipped` (or `backend-only` only when the row’s audience is integrators and the API is the product). |
| **Theirs** | They have it; we do not; we intend to. |
| **Both** | Comparable shipped job on both sides. |
| **Partial** | We have a hole; they may or may not. |
| **Later** | Valid, not in an open wave. |
| **Never** | `Ours=refuse`. |
| **N/A** | Row should not have been compared (wrong category). Rare; prefer deleting or moving to `LP-XX`. |

`V` is **not** a vote of who is “better.” Stripe will have **Y** on almost every global table-stakes row. That does not make every row Wave 0.

### Other required fields

| Field | Values | Rule |
|-------|--------|------|
| **W** | `0` `1` `2` `3` `4` `—` | Thematic home. `—` only for `Never` / `N/A`. |
| **P** | integer, `0` = now inside that wave | Only meaningful when that wave is **open**. Do not micro-rank Wave 4 while Wave 0 residuals remain. |
| **Class** | `must-my` · `table-stakes` · `diff` · `trap` | Rubric, next section. |
| **Src** | sibling file id (`01`, `06`, `10`…) or ADR / gap path | Required before changing `Ours` or `V`. |
| **Plane** | `G` `U` `S` `—` | Required on PAY/COM/DUN/TAX/TRU money rows. |

### Exact markdown columns for `00-checklist-tracker.md`

The parent must use **this header** on every domain table (and on the implement-later queue, minus competitor cells if compactness requires — but the master tables must match):

```markdown
| ID | Feature | Ours | Inf | Loc | Str | MoR | Sub | Acc | V | W | P | Class | Src |
|----|---------|------|:---:|:---:|:---:|:---:|:---:|:---:|---|--:|--:|-------|-----|
| LP-PAY-001 | … | partial | P | Y | Y | P | Y | — | Partial | 1 | 1 | must-my | 06 |
```

Optional last column **Notes** is allowed on the implement-later queue and on any row whose `Ours` is `partial` or `backend-only`. Do not add competitor columns without editing this constitution.

**How to read a cell (copy this block to the top of `00-checklist-tracker.md`):**

| Mark | Where | Meaning |
|------|-------|---------|
| **shipped / partial / backend-only / absent / refuse / later** | `Ours` only | Lazuar depth + intent (see vocabulary) |
| **Y / P / N / —** | Inf Loc Str MoR Sub Acc | Competitor fact |
| **Ours / Theirs / Both / Partial / Later / Never / N/A** | `V` | Row verdict |
| **0–4 / —** | `W` | Wave |
| **0,1,2,…** | `P` | Priority inside an **open** wave (`0` = now) |
| **must-my / table-stakes / diff / trap** | `Class` | Rubric |

Do not use Aura’s `Y/P/N` marks inside `Ours`. Do not use `doc_off` / `stub` / `killed` — map them: `doc_off` → `later` or `refuse`; `stub` → `absent`; `killed` → `refuse`.

If the living `00-checklist-tracker.md` currently names vendor columns (Billplz, CHIP, HitPay, Xendit, Stripe, Paddle, Chargebee, Polar) instead of the six stack keys, **the constitution still wins for new tables**. Vendor names map as:

| Constitution key | Vendor names already appearing in drafts |
|------------------|------------------------------------------|
| **Inf** | (no single vendor — informal bundle; do not invent a “WhatsApp” column) |
| **Loc** | Billplz, CHIP, plus notes for ToyyibPay, SenangPay, Fiuu, iPay88, GHL, Curlec |
| **Str** | Stripe |
| **MoR** | Paddle, Polar (Lemon Squeezy / FastSpring in notes) |
| **Sub** | Chargebee (Recurly / Lago / Maxio in notes) |
| **Acc** | not a vendor column in the draft — fill from report 10 (MyInvois, Xero, AutoCount, StoreHub) |

HitPay and Xendit are **Loc-adjacent / SEA fintech**. They may stay as extra named columns **only** if `02`/`06` prove they are sales alternatives and this constitution is edited to add **Sea** as a seventh key. Until then, put HitPay/Xendit evidence in **Loc** notes or in `Notes` on `LP-PAY-014+`.

---

## Waves

Waves are **phases of meaning**, not sprints and not the Phase 0–D engineering checklist. A wave opens when the previous wave’s **exit criteria** can be said without asterisks. Residuals may remain as honesty notes; **marketing claims may not**.

Engineering Phase letters map as:

| Solidification phase | Tracker wave | Status as of 2026-08-16 |
|----------------------|--------------|-------------------------|
| 0 Foundations (outbox, authz, tests) | Wave 0 (platform honesty) | In-repo complete; residuals noted |
| A Recovery loop | Wave 0 | In-repo complete; manual gateway e2e residual |
| B Machines without JWT | Wave 1 | In-repo complete; redrive/rotate/ping/M2M-admin residuals |
| C Operate and trust | Wave 0 + Wave 1 trust bar | In-repo complete; support UI still SQL |
| D.1 WhatsApp productization | Wave 4 (or earlier only by new decision) | **Not started** (00.4 freeze) |
| D.2 Dunning flexibility | Wave 4 | Not started |
| D.3 Compliance UI re-surface | Wave 2 | Not started (`[MVP-HIDE]` still on) |
| D.4 Commerce polish + extra rails + Xero | Wave 3 (proration/portal/MRR) and Wave 4 (rails/Xero) | Not started |

### Wave 0 — Honesty + closed money loops

**Intent:** “Failed payment → customer gets nudged → we retry sensibly → they pay or we suspend/cancel → access and metrics update.” Inbound money events are receivable, durable, auditable. Refunds are not dead code. We do not advertise WhatsApp, Xero, or Fiuu. Support can explain what a webhook did from logs/tables.

**Why first:** `00-what-we-need-to-do-next.md` Theme 1 and Phase A. A CaaS that drops money events is not a competitor; it is a liability. ADR 023 says we compete *right now* on recovery + FPX — so recovery must be real.

**In scope (row families):**

- `LP-PAY-005` … `LP-PAY-010`, `LP-PAY-012` — inbound webhooks, failure events, off-session, refunds, disputes, secrets/disable.  
- `LP-DUN-001` … `LP-DUN-005`, `LP-DUN-008` — enter past-due, builder that actually fires, catch-up, recovery, update-payment, pause.  
- `LP-MSG-001`, `LP-MSG-002`, `LP-MSG-006` — email channel, variable resolution, suppressions. WhatsApp **honesty** (flag off / label UI), not Meta Cloud.  
- `LP-TRU-001` … `LP-TRU-004` — ledger happy path, refund symmetry, no double credit, summary under reversals.  
- `LP-UX-003` — success page waits for server status.  
- `LP-OPS-005`, `LP-OPS-006` — support timeline + DLQ visibility (may stay SQL-runbook and still be Wave 0 if documented; UI is the remaining hole).

**Out of scope:** campaign versioning, decline-code ML, WhatsApp product, LHDN UI, proration, Xero, extra rails, Commerce M2M admin.

**Exit criteria (must say without asterisks):**

1. A declined vaulted renewal **enters recovery**, email (the channel we claim) goes out, payment **ends recovery** with metrics.  
2. Gateway payment success is **idempotent** on business identity; failures are visible to domain logic.  
3. Refunds carry real amount/currency and reverse ledger/tax on sold paths.  
4. Credits and ledger posts for LHDN/top-ups/payments **do not double-count** on the happy path.  
5. README / ops UI do **not** claim WhatsApp, Xero, Fiuu, or “tax invoices in the portal.”  
6. Cross-module events that matter **leave the outbox** and can be retried or dead-lettered.

**Open as of this file:** most domain work is in-repo from Phase A/C. Remaining Wave 0 *product* holes to keep `partial` until closed: commerce GMV dispute ledger (`LP-PAY-010`), two-phase raw webhook intake (`LP-PAY-016` reserved), single support timeline UI (`LP-OPS-005`), Billplz fee=0 is **Wave 1** not Wave 0 (does not break the loop). Do not reopen D1 (run snapshot) inside Wave 0.

**Wave 0 lock:** do not open Wave 2 (un-hide LHDN) if Wave 0 exit #4 (ledger/credits) is still a lie. Do not open Wave 4 WhatsApp if Wave 0 still claims a channel we do not have.

### Wave 1 — Sellable CaaS

**Intent:** Integrator flow = create workspace → generate credentials in the frontend → call documented APIs → receive signed webhooks. Buyer flow = high-converting localized checkout, especially **FPX**. Docs teach integration, not the modular monolith.

**Why second:** ADR 023’s temporary competitive position is “Billplz (FPX) + automated dunning” against Western checkouts. Theme 2–3 of the direction doc. Without keys + outbound webhooks, we are a hosted form, not CaaS. Without checkout conversion + FPX polish, we lose to Billplz’s own payment link on price and to Stripe on craft.

**In scope:**

- `LP-DEV-001` … `LP-DEV-007`, `LP-DEV-010` — keys, scopes, outbound multi-endpoint, signatures, logs+redrive, docs hub, Payments M2M cashier, rotate/ping/unify residuals.  
- `LP-PAY-001` … `LP-PAY-003`, `LP-PAY-011` — Stripe/Billplz/CHIP as sellable rails; **fee fidelity** and FPX operability.  
- `LP-COM-001` … `LP-COM-004`, `LP-COM-006` — links, subs, one-time, coupons, offline mark-paid.  
- `LP-UX-001`, `LP-UX-002`, `LP-UX-004`, `LP-UX-005` — MY localization, mobile conversion, receipt PDF, branding.  
- `LP-OPS-001` … `LP-OPS-004` — console a merchant can live in.  
- `LP-MSG-005` — delivery log visible to support in ops.  
- `LP-DUN-009` — payment-method-aware campaigns (online vs manual) if it unblocks FPX vs card ICPs.

**Out of scope:** un-hiding LHDN (Wave 2), proration/portal/MRR (Wave 3), Fiuu/Xendit/Xero/WhatsApp/OAuth/full Commerce M2M (Wave 4), `LP-DEV-008`.

**Exit criteria:**

1. A stranger can mint a **test key in Ops**, call a documented Payments or LHDN path, and receive a **signed, logged** webhook without a user JWT.  
2. Stolen/mis-scoped keys cannot mint more keys or change payment config.  
3. Workspace webhooks fan out **without** product URL equality. Redrive exists or is explicitly waived in `Notes` with a date.  
4. Developers hub leads with auth + event catalog + one happy path; Ops internal API is labeled internal.  
5. Billplz/FPX checkout is something we would put in front of a paying MY merchant without apologizing for fees=0, wrong gateway default, or English-only bank copy **or** we document the apology as a known `partial` and stop saying “Apple-Pay-style FPX.”  
6. Product checkout + coupons + subscribers + transactions are the obvious ops loop.

**Open as of this file:** Phase B shipped the spine. Wave 1 **remaining** work is mostly residuals: webhook redrive/rotate/test ping, payload richness, SSRF, LHDN delivery unify (`LP-DEV-010`), Billplz fee fidelity (`LP-PAY-011`), checkout conversion craft (`LP-UX-001/002/005`), delivery-log UI (`LP-MSG-005`). Do not invent a second credential system.

### Wave 2 — Compliance UI (un-hide LHDN, TIN, invoices)

**Intent:** Reverse ADR 023 when — and only when — the ledger and consolidation paths we already sell are trustworthy. The moat becomes visible: B2C 28th consolidation, B2B TIN + instant tax invoice, legal profile.

**Why third, not first:** ADR 023 is explicit. Launching Pure CaaS first was the point. Un-hiding a tax invoice button before consolidation selection and receipt numbering are honest creates **legal** false expectations. Direction doc Theme 4: “no silent double charge / no wrong consolidated tax batch” is the near bar.

**In scope:** all `LP-TAX-001` … `LP-TAX-010`, plus `LP-COM-005` if quotes are the B2B checkout, plus any `LP-UX` tax-invoice download un-hide.

**Method:** remove `[MVP-HIDE]` on routes and sidebar; bind already-built backend (TIN fields, quotes page, tax invoices, credit notes, billing profile, portal download). Do **not** rewrite MyInvois. Do **not** start GSTN/Coretax.

**Exit criteria:**

1. Ops shows Invoicing + Legal/Billing Profile. Portal can collect TIN when the product requires it and can download a tax invoice only when one exists.  
2. B2C consolidation has a sandbox month-end dry run against real (sandbox) MyInvois, with receipts not overwritten by consolidation IDs.  
3. B2B path: TIN validate → pay → UBL submit → QR / long-id visible.  
4. Credit note / 72h cancel path is operable from UI or documented as API-only with a support runbook.  
5. V1.1 signing is either real or the UI **does not** claim signed documents.  
6. Marketing may say “LHDN at the point of sale” without a backend-only asterisk.

**Wave 2 lock:** do not un-hide if Wave 0 ledger/credit lies remain. Do not build India/Indonesia tax in this wave (`LP-TAX-013` reserved = `later` or `refuse` until LHDN loop is production-trusted).

### Wave 3 — Billing completeness (proration, portal self-serve, MRR)

**Intent:** Match what a global SaaS buyer means by “billing”: plan changes that prorate, a portal that updates payment methods and cancels at period end, MRR that a founder can take to a board deck **from the ledger**.

**Why fourth:** These are **table-stakes global**, not must-have-to-open-in-MY. Informal and Billplz do not have them. Stripe Billing and Chargebee do. Building them before money loops and FPX conversion is how Asian CaaS products die in abstraction.

**In scope:** `LP-COM-007` … `LP-COM-012`, `LP-UX-007`, `LP-UX-008`, `LP-TRU-005`.

**Exit criteria:**

1. Upgrade/downgrade produces a prorated invoice or an explicit documented non-prorate policy shown to the buyer.  
2. Buyer portal: list entitlements, **update payment method** (not Stripe-only or, if Stripe-only, labeled), cancel **at period end**, see receipts. Hard-cancel-only is no longer the only story.  
3. Ops dashboard MRR/ARR comes from `billing.LedgerLines` (or a materialized projection thereof), not from summing `commerce.TransactionLogs`.  
4. Quantity/seats either work through renewals or are removed from checkout.  
5. Trials either vault a card and convert or are removed from the product model.

**Wave 3 lock:** do not implement usage-based / metered billing in this wave unless `01` proves an ICP that cannot buy without it. Metered is a common Chargebee trap.

### Wave 4 — Expansion (more rails, Xero, WhatsApp if not earlier)

**Intent:** Differentiation on top of a trusted core. ADR 021 keep-list (WhatsApp, Xero). Extra Asian rails only after the core four are stable (fees, refunds, webhooks). Commerce M2M admin and OAuth only if Wave 1 integrator motion is hitting the D5 ceiling.

**In scope:** `LP-PAY-004` and later rails, `LP-MSG-003`, `LP-MSG-004`, `LP-TRU-006`, `LP-DEV-008`, `LP-UX-006`, `LP-DUN-006`, `LP-DUN-007`, `LP-DUN-010`.

**WhatsApp sequencing rule:** Wave 0 = honesty (off or labeled). Wave 4 = Meta Cloud + templates + credits + failure UX + optional interactive FPX buttons. Pull into Wave 1 **only** with an explicit D2 reversal and a named ICP that will not buy email-only recovery. Do not build interactive in-chat checkout before plain utility templates send.

**Xero sequencing rule:** ledger must already be trusted (Wave 0/3). Xero is a **sync of truth**, not a second ledger. QuickBooks is not in the first 80.

**Extra rails rule:** FPX depth (Billplz/CHIP) before India/Indonesia. Razorpay exists; do not market India until we have an ICP. Fiuu/SenangPay/Xendit are Wave 4 **pain-gated**.

**Exit criteria:** marketing claims (WhatsApp dunning, Xero, extra rails, API-first) match **demoable** production paths. Integrator onboarding measured in minutes.

### What has no wave

`W = —` for every `LP-XX-*` trap and every `Ours=refuse` row. Also `W = —` for N/A rows that should be deleted.

### Suggested open-wave policy for a solo/small team

Only **one** build wave is open at a time, plus **honesty patches** on any shipped Wave 0/1 row that regresses. Research (filling competitor cells) may run in parallel. Un-hiding Wave 2 is a product go-live, not a side quest during Wave 1 conversion work.

---

## Competitor column set

Columns are **stacks a Malaysian founder actually chooses between**, plus the two global pattern libraries that distort our backlog if left unnamed. They are not “every company in billing.”

### The six columns

| Col | Stack | Who is inside | Why this column exists | What we must not do with it |
|-----|--------|----------------|------------------------|------------------------------|
| **Inf** | Informal MY | WhatsApp + IG DM + Excel/Google Sheet + a Billplz/CHIP/SenangPay **link** + month-end typing into MyInvois / a bookkeeper | The incumbent. Most SEA digital businesses do not “switch from Stripe.” They switch from this. | Do not score Inf **N** on dunning and call it a win. Informal *does* dunning: the founder nagging in WA. That is **P**, not **N**. |
| **Loc** | Local rails as products | **Billplz**, **CHIP / ChipCollect**, **Fiuu** (RMS), **SenangPay**, to a lesser degree **Payex**, **Toyyibpay** | They own FPX distribution and “just send a bill.” ADR 023 says we compete with them on checkout + recovery. | Do not treat Loc as “dumb” and therefore skip fee/refund/webhook holes. Loc **Y** on hosted FPX bill is their whole product. |
| **Str** | Stripe Checkout + Billing + Customer Portal + webhooks | Stripe (optionally Stripe Tax, Sigma — mention in notes, do not add columns) | Global table-stakes and the UX founders screenshot. Terrible native FPX; no LHDN. | Do not adopt Stripe’s company shape (Connect marketplace, Tax-as-MoR, Capital). Pattern-library only. |
| **MoR** | Merchant of Record | **Paddle**, **Lemon Squeezy**, **Polar**, FastSpring | The “we will do tax and refunds, you take 5–9%” alternative. Attractive to Western indie hackers; **wrong economics** for our ICP and for LHDN-at-POS. | Never score “become MoR” as `must-my`. `LP-XX-001`. |
| **Sub** | Subscription billing OS | **Chargebee**, **Recurly**, **Lago**, (notes: Stripe Billing depth, Chargebee retention) | Where proration, entitlements, quote-to-cash, and dunning intelligence live. The Wave 3/4 gravity well. | Do not import their entire quote-to-cash suite. Metered, add-on matrices, and CPQ are usual traps. |
| **Acc** | Compliance / books | **LHDN MyInvois portal**, **Xero**, **AutoCount**, **SQL Account**, StoreHub/SQL e-invoice add-ons, bookkeeper + spreadsheet | ADR 021’s other side: accountants veto software that does not land in the books. Also the “just use the government portal” incumbent for tax. | Do not become an accounting suite. Xero **sync** is keep; GL UI is `refuse`. |

### Who is not a column (and where they go)

| Who | Why not a column | Where their evidence lives |
|-----|------------------|----------------------------|
| **Aura / Fresha / salon OS** | Different company. Guest-pay via Lazuar is a *customer* of this product, not a competitor column. | Out of this tracker. Do not add `Aura` as Inf. README standing constraint. |
| **Shopify / EasyStore / WooCommerce** | Storefront-first. They win on catalog/theme, not on CaaS. Woo+Billplz is Informal/Loc hybrid. | Fill checkout/catalog rows as **P** under Inf or Loc; mention Shopify Payments in `Notes` on `LP-COM-001`. If `02` later proves we lose deals to EasyStore as a named alternative, promote a **Stf** column by editing this file. |
| **Wati / Respond.io / Twilio** | Messaging clouds. Relevant to `MSG` only. | Cells on `LP-MSG-*` as pattern sources in `Notes`, or **P** under Inf (Wati) / Str (Twilio). |
| **Razorpay / Xendit / Midtrans** | Real, but not the MY sales conversation until Wave 4. | Loc notes, or `Y` on `LP-PAY-004` when that row is compared. |
| **HitPay** | SG/MY no-code links. Sales-adjacent. | Loc notes until `02`/`06` promote a **Sea** column. |
| **QuickBooks** | ADR 020 twin of Xero; no code; do not double the Acc column. | Acc **P** if needed; Xero is the keep. |
| **Avalara / Sovos / ClearTax** | Global/IN tax engines. Multi-country is refuse-until-LHDN. | `Notes` on `LP-TAX-011+` only. |
| **Kajabi / Gumroad / Linktree** | Vitamin gravity. | `LP-XX` traps, not columns. |
| **Booksy / Mindbody / Zenoti** | Wrong vertical. | Never. |

### How to fill a column when the stack is a *bundle*

- **Inf** is a bundle by definition. `Y` means “a competent informal operator already does this job with tools they have.” Manual MyInvois key-in is **Y** for “file a B2C invoice,” **P** for “consolidate 3,000 receipts without crying.”  
- **Loc** = what the **gateway product** does, not what a developer could bolt on. Billplz does not get **Y** for subscription dunning because a merchant *could* write a cron.  
- **Str / MoR / Sub / Acc** = the named vendor’s *native* product, not Zapier.

### Column authority after siblings land

`02-local-sea-competitor-landscape.md` may rename **Loc** members (e.g. drop SenangPay, add Payex) without changing the column key. `03-global-competitor-landscape.md` may split **MoR** vs **Sub** if Polar starts looking like Stripe Billing — still do not add a seventh column without a constitution edit. If both `02` and `03` agree EasyStore is a sales competitor, add:

```text
Stf | Shopify / EasyStore / Woo + plugin
```

as column 7, placed after Loc, and update every table. Until then, six columns.

If `06-sea-fintech-platforms.md` proves HitPay + Xendit are in the shortlist as often as Billplz, add:

```text
Sea | HitPay / Xendit / Midtrans (SEA fintech platforms)
```

as column 7 (or 8 if Stf also exists). Do not silently widen `00-checklist-tracker.md` without this file changing in the same commit.

---

## Rubric

Four classes. Every row gets exactly one. Class is **why the row exists**, not how good we are at it.

### `must-my` — Must-have to compete in Malaysia

The deal dies in KL/Selangor/JB/Penang without this, or the merchant immediately commits a **legal / money** failure.

Tests (need one):

1. FPX / MYR / local-gateway path for the named ICP.  
2. LHDN / SST / PDPA obligation the ICP already feels.  
3. Recovery on the channel or rail Malaysians actually use (email minimum; WhatsApp is `diff` until we ship it).  
4. Honesty about money (idempotent pay, refunds that reverse, no double credit).

Examples: `LP-PAY-002` Billplz/FPX, `LP-PAY-005` inbound webhooks, `LP-DUN-001` past-due entry, `LP-TAX-002` B2C consolidation, `LP-TRU-001` ledger.

**Scoring:** a `must-my` row that is `absent` or `refuse` is an emergency (refuse should never be must-my — that is a malformed row). `backend-only` on a must-my **buyer/merchant** job is a Wave 2 emergency if we are already selling compliance; it is acceptable during ADR 023 if we are selling Pure CaaS **and** we do not market the hidden job.

### `table-stakes` — Table-stakes global

A founder who has used Stripe Billing, Chargebee, or Paddle will ask on the first call. Informal/Loc may lack it. Losing the deal to Stripe/Chargebee, not to Billplz.

Examples: hosted checkout link, subscriptions, coupons, customer portal, webhook event catalog, API keys, proration, cancel-at-period-end, MRR.

**Scoring:** do not pull every `table-stakes` row into Wave 0. That is how Wave 0 becomes Chargebee. Wave 0 only gets the subset that is also money-loop honesty. The rest wait for Wave 1 (CaaS surface) or Wave 3 (billing completeness).

### `diff` — Differentiator

We win **because** of this, or we intend to. Hard to clone (UBL + PKI + ledger), or locally unfair (WhatsApp recovery, FPX+dunning together, prepaid credits instead of GMV tax).

Examples: LHDN-at-POS, B2B TIN+QR, WhatsApp utility dunning, Xero from *our* ledger, signed outbound webhooks as the unlock primitive, BYOK zero-take-rate.

**Scoring:** differentiators that are `absent` are **not** Wave 0 by default. ADR 023 hid the biggest one on purpose. Wave 2/4 is the home. A differentiator we **claim in README** while `absent` is a Wave 0 **honesty** patch (delete the claim) plus a Wave 4 build row.

### `trap` — Trap

Building it makes us a different company, a vitamin, or a liar.

Tests (need one):

1. On the ADR 021 kill list.  
2. Requires MoR, marketplace take-rate, or holding customer funds.  
3. CMS / site builder / social / inbox.  
4. Vertical tourism (salon OS, gym, EMR, LMS).  
5. Enterprise CPQ / metered spaghetti required to “match Chargebee.”  
6. Multi-country tax before MY loop is trusted.  
7. Rebuilding Community/Vault.

Examples live in `LP-XX-*`. Class `trap` ⇒ `Ours=refuse`, `V=Never`, `W=—`.

### Worked examples (so later fillers do not argue class)

| Row | Class | Why |
|-----|-------|-----|
| Billplz/FPX hosted checkout | **must-my** | Without this we are Stripe in a country that pays by bank. |
| Inbound webhook idempotency | **must-my** | Money safety. Informal does this by “I only click once.” We cannot. |
| Customer portal self-serve | **table-stakes** | Stripe/Chargebee buyers expect it; Billplz users do not. Wave 3. |
| Proration | **table-stakes** | Same. Wave 3. Do not block Wave 1. |
| LHDN consolidation | **diff** (also legally must-my for the *compliance* ICP) | Class **diff** because ADR 023 chose to hide it for the Pure CaaS ICP. When Wave 2 opens, treat as must-my **for that go-live**. Put `Class=diff` in the tracker and `Notes=must-my at Wave 2 go-live`. |
| WhatsApp Meta Cloud send | **diff** | ADR 021 keep. Informal already nags in WA (Inf=Y). Our job is automation + credits + audit. Wave 4. |
| Become MoR / file tax for the merchant | **trap** | ADR 019. Acc already does books; MoR takes GMV. |
| Telegram bouncer | **trap** | ADR 021/022 kill. |
| Usage-based billing v1 | **trap** until an ICP is named | Chargebee gravity. Re-class to table-stakes only with evidence in `03`/`08`. |

### Scoring the matrix (for `00-evaluation.md`, later)

When the parent scores “how far behind,” use:

```
gap_points = count(rows where Class in {must-my, table-stakes, diff}
                   and V in {Theirs, Partial}
                   and Ours not in {refuse, later}
                   and W <= open_wave + 1)
```

Exclude `trap`, `Never`, `N/A`, and rows with `W` more than one wave ahead of the open wave. Otherwise Stripe and Chargebee will “win” 80–0 and the score is useless.

Weight suggestion (do not bake into the table; use in the evaluation narrative):

- `must-my` + `Theirs` = 3  
- `must-my` + `Partial` = 2  
- `table-stakes` + `Theirs` = 2  
- `table-stakes` + `Partial` = 1  
- `diff` + `Theirs` = 1 (or 2 if we currently **market** it)  
- `diff` we market + `absent` = 3 **honesty penalty** (fix the claim in Wave 0)

### Traps written as company-shape mistakes (`LP-XX` reserved)

Mint these in `00-checklist-tracker.md` even though they are outside the first 80. They exist so a later sibling cannot “discover” them as Wave 1.

| ID | Feature | Why refuse |
|----|---------|------------|
| `LP-XX-001` | Merchant of Record / GMV take-rate | ADR 019 BYOK. Plane collapse. |
| `LP-XX-002` | Website / link-in-bio / funnel builder | ADR 021 vitamin. CMS trap (ADR 015). |
| `LP-XX-003` | Community DRM (Telegram/Discord bouncer) | ADR 021/022 kill. |
| `LP-XX-004` | First-party WhatsApp inbox / marketing suite | We are not Wati. Channel for **recovery + transactional** only. |
| `LP-XX-005` | Viral giveaways / lead-gen forms | ADR 021 kill. |
| `LP-XX-006` | Vault / LMS / course hosting as a product | ADR 022. Fulfillment is webhook + receipt, not our DRM. |
| `LP-XX-007` | Marketplace / discover / take-rate network | ADR 018 later-never under 021. |
| `LP-XX-008` | Escrow.com / e-sign at checkout | ADR 020 Phase 2; direction doc “not next.” |
| `LP-XX-009` | Multi-country tax (GSTN / Coretax / InvoiceNow) before LHDN trusted | ADR 021 pillars 2–3 are sequenced; 023 + direction say not yet. |
| `LP-XX-010` | Affiliate mass-payouts / BNPL / Web3 settlement | ADR 020 Phase 3. |
| `LP-XX-011` | Super-app 15 modules / ops AI as the product | ADR 014 stale; ops chat stays unrouted. |
| `LP-XX-012` | “Pro plan billed through tenant Billplz” mixing SaaS fee into GMV plane | Plane S vs G. |

---

## Suggested first 80 rows (IDs + names + wave)

Seed `Ours` is from the repo on 2026-08-16 (Phases 0–C in-repo, D not started, ADR 023 still hiding TAX UI). **`01` overwrites `Ours`.** `P` is a suggestion for when that wave **opens**, not a now-list.

`Class` abbreviations in this table: `must-my` · `table-stakes` · `diff` · `trap`.

### PAY — Payments (12)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-PAY-001 | BYOK Stripe hosted checkout (cards, Apple/Google Pay via Stripe) | 1 | shipped | table-stakes |
| LP-PAY-002 | BYOK Billplz hosted bill — FPX / MYR path a merchant can sell | 1 | partial | must-my |
| LP-PAY-003 | BYOK CHIP Collect hosted checkout + recurring token | 1 | partial | must-my |
| LP-PAY-004 | BYOK Razorpay (keep working; do not market IN until ICP) | 4 | partial | table-stakes |
| LP-PAY-005 | Inbound webhook verify, persist, structured process log | 0 | shipped | must-my |
| LP-PAY-006 | Business-key idempotency (not only provider event id) | 0 | shipped | must-my |
| LP-PAY-007 | Payment-failed published into Commerce (past-due entry) | 0 | shipped | must-my |
| LP-PAY-008 | Off-session / vaulted renewal charge with metadata | 0 | partial | must-my |
| LP-PAY-009 | Full/partial refunds with real amount + ledger + tax reverse | 0 | partial | must-my |
| LP-PAY-010 | Disputes / chargebacks first-class on **commerce GMV** (not only utility clawback) | 0 | absent | must-my |
| LP-PAY-011 | Gateway fee fidelity (Billplz today always 0) | 1 | partial | must-my |
| LP-PAY-012 | Encrypted BYOK secrets + soft-disable gateway without delete | 0 | shipped | table-stakes |

`LP-PAY-004` class: treat as `table-stakes` only when an India/SEA-India ICP is open; until then score as `later` in `V`, keep the row so the adapter is not deleted by accident.

Reserved next: `LP-PAY-013` DuitNow QR as first-class rail, `LP-PAY-014` Fiuu adapter, `LP-PAY-015` SenangPay, `LP-PAY-016` two-phase raw intake vs fulfill, `LP-PAY-017` webhook replay UI, `LP-PAY-018` capability matrix (portal/off-session/refund flags) instead of try/catch.

### COM — Commerce (12)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-COM-001 | Product checkout links (`/{tenant}/{product}`) | 1 | shipped | table-stakes |
| LP-COM-002 | Recurring subscriptions (month / year) | 1 | shipped | table-stakes |
| LP-COM-003 | One-time products / orders | 1 | shipped | table-stakes |
| LP-COM-004 | Coupons (reserve / confirm / expire) | 1 | shipped | table-stakes |
| LP-COM-005 | Custom payment links / B2B quotes checkout | 2 | partial | diff |
| LP-COM-006 | Manual / offline mark-paid + record-payment | 1 | shipped | table-stakes |
| LP-COM-007 | Quantity / seats that survive renewal | 3 | partial | table-stakes |
| LP-COM-008 | Proration on plan change | 3 | absent | table-stakes |
| LP-COM-009 | Cancel at period end (not only hard cancel) | 3 | absent | table-stakes |
| LP-COM-010 | Customer portal self-serve: update PM, invoices, cancel | 3 | partial | table-stakes |
| LP-COM-011 | Trials that vault a card and convert | 3 | absent | table-stakes |
| LP-COM-012 | Plan upgrade / downgrade (same customer) | 3 | absent | table-stakes |

`LP-COM-005` is Wave 2 because the B2B quote route is `notFound()` under ADR 023; ad-hoc admin payment links may already exist — split later if `07`/`11` say they are two jobs.

Reserved: `LP-COM-013` PWYW / `PricingModel` enforce-or-delete, `LP-COM-014` usage-based (default trap), `LP-COM-015` multi-currency checkout, `LP-COM-016` checkout session expiry (likely already shipped — confirm in `01`).

### DUN — Dunning (10)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-DUN-001 | Failed renewal enters `PAST_DUE` and assigns a campaign | 0 | shipped | must-my |
| LP-DUN-002 | Campaign builder: day offsets, EMAIL / AUTO_CHARGE, grace, CANCEL/SUSPEND | 0 | partial | must-my |
| LP-DUN-003 | Catch-up steps + idempotent dispatch after worker lag | 0 | shipped | must-my |
| LP-DUN-004 | Recovery payment clears dunning, advances period, records metrics | 0 | shipped | must-my |
| LP-DUN-005 | Update-payment / arrears checkout (correct gateway, real links) | 0 | shipped | must-my |
| LP-DUN-006 | Campaign run snapshot / versioning (in-flight edits do not rewrite journeys) | 4 | absent | table-stakes |
| LP-DUN-007 | Decline-code-aware retry rules (static first, not ML) | 4 | absent | diff |
| LP-DUN-008 | Pause / resume dunning per subscriber | 0 | shipped | table-stakes |
| LP-DUN-009 | Payment-method-aware campaigns (online vs manual / FPX vs card) | 1 | partial | must-my |
| LP-DUN-010 | Funnel analytics by step (sent → paid → churned) | 4 | absent | table-stakes |

`LP-DUN-002` is `partial` because WHATSAPP is a step type that does not send, and D1 snapshot is not done. Do not mark it `absent`.

Reserved: `LP-DUN-011` multi-action same day, `LP-DUN-012` merchant timezone / time-of-day, `LP-DUN-013` force-retry / skip-step ops tools.

### TAX — Compliance (10)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-TAX-001 | LHDN MyInvois submit / poll pipeline (UBL, XSD, credits) | 2 | backend-only | diff |
| LP-TAX-002 | B2C monthly consolidation job (28th MYT, catch-up) | 2 | backend-only | diff |
| LP-TAX-003 | B2B TIN + company capture at checkout | 2 | backend-only | diff |
| LP-TAX-004 | Instant tax invoice + MyInvois QR / long-id for buyer | 2 | backend-only | diff |
| LP-TAX-005 | TIN validate against LHDN | 2 | backend-only | diff |
| LP-TAX-006 | Credit notes / 72-hour cancel window | 2 | backend-only | diff |
| LP-TAX-007 | Quotes UI + `/pay/{session}` B2B checkout (today `notFound()`) | 2 | backend-only | diff |
| LP-TAX-008 | Legal & billing profile (supplier TIN, BRN, MSIC, address) | 2 | backend-only | must-my |
| LP-TAX-009 | Un-hide invoicing nav (quotes, tax invoices, credit notes) | 2 | absent | diff |
| LP-TAX-010 | V1.1 XML signing **or** UI that does not claim signed docs | 2 | partial | diff |

`LP-TAX-001`–`008` are `backend-only` even when APIs are strong: the **merchant job** is hidden. `LP-TAX-009` is the routing act itself (`Ours=absent` until `[MVP-HIDE]` comes off). `LP-TAX-010` is `partial` because unsigned V1.0 exists and signing is the honesty hole.

Reserved: `LP-TAX-011` zero-rated export classification (pillar 3), `LP-TAX-012` SST line honesty vs e-invoice, `LP-TAX-013` GSTN/Coretax (`refuse` until LHDN trusted → then `later`).

### DEV — Developer platform (10)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-DEV-001 | Platform API keys: name, live/test, reveal once, list prefix, revoke | 1 | shipped | table-stakes |
| LP-DEV-002 | Scoped keys (not OrgAdmin); cannot mint keys or edit payment config | 1 | shipped | table-stakes |
| LP-DEV-003 | Multi-endpoint outbound webhooks + versioned event catalog | 1 | partial | table-stakes |
| LP-DEV-004 | Signed delivery (timestamp + HMAC) + retries | 1 | shipped | table-stakes |
| LP-DEV-005 | Delivery logs + **redrive** | 1 | partial | table-stakes |
| LP-DEV-006 | Developers hub: auth guide, event catalog, product APIs (not Ops dump) | 1 | partial | table-stakes |
| LP-DEV-007 | Payments M2M cashier (`/integrations/payments/checkouts`) | 1 | shipped | diff |
| LP-DEV-008 | Commerce M2M admin (products, subs, cancel) — D5 deferred | 4 | absent | table-stakes |
| LP-DEV-009 | LHDN SDKs (TS/.NET) + taxpayer validate as the gold-standard slice | 2 | shipped | diff |
| LP-DEV-010 | Secret rotate, test ping, SSRF lock, unify LHDN onto shared dispatcher | 1 | absent | table-stakes |

`LP-DEV-008` class in tracker = `table-stakes` with `V=Later` (D5). Do not let `03`/`14` promote it to Wave 1.

Reserved: `LP-DEV-011` OAuth2 client_credentials, `LP-DEV-012` last_used / expiry / IP allowlists, `LP-DEV-013` public vs internal OpenAPI split permanence.

### UX — Buyer experience (8)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-UX-001 | MY-localized checkout (MYR, FPX copy, bank expectation, optional BM) | 1 | partial | must-my |
| LP-UX-002 | Mobile-first checkout conversion (complete in one thumb) | 1 | partial | must-my |
| LP-UX-003 | Honest success: poll server status; timeout ≠ paid | 0 | shipped | must-my |
| LP-UX-004 | Receipt PDF (HMAC public document) | 1 | shipped | table-stakes |
| LP-UX-005 | Checkout branding (logo / colors / merchant name) | 1 | partial | table-stakes |
| LP-UX-006 | Custom domain for checkout | 4 | absent | table-stakes |
| LP-UX-007 | Buyer portal list + cancel (current magic-link slice) | 3 | partial | table-stakes |
| LP-UX-008 | Abandoned-checkout email (wire or delete orphan template) | 3 | absent | table-stakes |

`LP-UX-003` seed is `shipped` because `CheckoutSuccessView` polls `/checkout/{id}/status` and times out to “still processing,” not “paid.” Confirm in `09`/`01` whether `sub_id` query is always present and whether custom-link success uses the same path.

Reserved: `LP-UX-009` i18n BM/EN as a dedicated row if `01` splits it from `LP-UX-001`, `LP-UX-010` Apple/Google Pay visibility on Stripe path.

### OPS — Merchant console (6)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-OPS-001 | Commerce console: products, subscribers, transactions, coupons | 1 | shipped | table-stakes |
| LP-OPS-002 | Dashboard KPIs that are not stub zeros | 1 | partial | table-stakes |
| LP-OPS-003 | Payment gateway + Resend BYOK settings (mask, has_*) | 1 | shipped | must-my |
| LP-OPS-004 | Utility credit wallet + top-up (plane U, not GMV) | 1 | shipped | diff |
| LP-OPS-005 | Support “what did this payment do?” timeline (today: SQL/logs join) | 0 | absent | must-my |
| LP-OPS-006 | Outbox / dead-letter visibility in ops (today: runbook / metrics) | 0 | partial | table-stakes |

Reserved: `LP-OPS-007` members/roles, `LP-OPS-008` workspace switch polish, `LP-OPS-009` re-mount ops AI chat (`refuse` unless a new JTBD).

### MSG — Messaging (6)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-MSG-001 | Email lifecycle + dunning via tenant Resend BYOK | 0 | shipped | must-my |
| LP-MSG-002 | Template variables actually resolve (`plan_name`, amounts, links) | 0 | shipped | must-my |
| LP-MSG-003 | WhatsApp Meta Cloud send (templates, E.164, credits, failure UX) | 4 | absent | diff |
| LP-MSG-004 | Interactive WhatsApp pay / update-payment buttons | 4 | absent | diff |
| LP-MSG-005 | Message delivery log visible in ops (API exists) | 1 | backend-only | table-stakes |
| LP-MSG-006 | Suppressions + List-Unsubscribe | 0 | shipped | table-stakes |

Do **not** mark `LP-MSG-003` `partial` because a console logger and a campaign checkbox exist. That is how README lies start.

Reserved: `LP-MSG-007` broadcasts (default `later` or `LP-XX` if it becomes a marketing suite), `LP-MSG-008` credit-metered WA (depends on 003).

### TRU — Trust (6)

| ID | Feature | W | Ours (seed) | Class |
|----|---------|--:|-------------|-------|
| LP-TRU-001 | Double-entry ledger on gateway payment happy path | 0 | shipped | must-my |
| LP-TRU-002 | Refund / tax ledger symmetry (full and partial) | 0 | shipped | must-my |
| LP-TRU-003 | Credit wallet: no double LHDN charge; concurrent deduct idempotent | 0 | shipped | must-my |
| LP-TRU-004 | Financial summary believable under reversals | 0 | partial | must-my |
| LP-TRU-005 | MRR / ARR from the ledger (not transaction-log sums) | 3 | absent | table-stakes |
| LP-TRU-006 | Xero journal sync from Billing (ADR 021 keep) | 4 | absent | diff |

Reserved: `LP-TRU-007` deferred-revenue recognition (parked job), `LP-TRU-008` tenant isolation fail-closed (likely shipped — confirm `01`; if shipped, still a regression row), `LP-TRU-009` money-path tests as a CI gate (hygiene; only mint if a later testing sibling insists it is a competitor-visible trust job).

### Count

12+12+10+10+10+8+6+6+6 = **80**.

### Wave census of the first 80 (for the parent’s open-wave view)

| W | Count | IDs (short) |
|--:|------:|-------------|
| 0 | 23 | PAY-005–010, PAY-012; DUN-001–005, DUN-008; MSG-001, MSG-002, MSG-006; TRU-001–004; UX-003; OPS-005, OPS-006 |
| 1 | 27 | PAY-001–003, PAY-011; COM-001–004, COM-006; DUN-009; DEV-001–007, DEV-010; UX-001, UX-002, UX-004, UX-005; OPS-001–004; MSG-005 |
| 2 | 12 | COM-005; TAX-001–010; DEV-009 |
| 3 | 9 | COM-007–012; UX-007, UX-008; TRU-005 |
| 4 | 9 | PAY-004; DUN-006, DUN-007, DUN-010; DEV-008; UX-006; MSG-003, MSG-004; TRU-006 |

Plus `LP-XX-001`–`012` with `W=—` in the trap appendix of the tracker.

### Implement-later queue seed (rows that are not already `shipped` / `Ours`, and not refuse)

Parent should copy this shape into `00-checklist-tracker.md` **Implement-later queue**, sorted by `W` then `P`. Suggested first cut (not a build commit):

| ID | Feature | Ours | V | W | P | Why |
|----|---------|------|---|--:|--:|-----|
| LP-PAY-010 | Commerce GMV disputes | absent | Theirs | 0 | 0 | Money loop incomplete vs Stripe |
| LP-OPS-005 | Support payment timeline | absent | Partial | 0 | 0 | Success criterion #3 still SQL |
| LP-OPS-006 | DLQ visibility in ops | partial | Partial | 0 | 1 | Runbook ≠ product |
| LP-DUN-002 | Campaign builder honesty (WA step) | partial | Partial | 0 | 1 | Label or hide WHATSAPP until MSG-003 |
| LP-PAY-009 | Refund path operability | partial | Partial | 0 | 1 | Offline / missing ExternalReference residual |
| LP-TRU-004 | Summary under reversals | partial | Partial | 0 | 2 | Ops dashboard trust |
| LP-PAY-011 | Billplz fee fidelity | partial | Partial | 1 | 0 | Cannot claim net cash |
| LP-PAY-002 | FPX sellable polish | partial | Partial | 1 | 0 | ADR 023 competitive claim |
| LP-UX-001 | MY checkout localization | partial | Partial | 1 | 0 | Lose to Billplz on familiarity |
| LP-UX-002 | Mobile conversion | partial | Partial | 1 | 1 | SEA traffic is phone |
| LP-DEV-005 | Webhook redrive | partial | Partial | 1 | 0 | Integrators will ask day one |
| LP-DEV-010 | Rotate / ping / unify LHDN delivery | absent | Theirs | 1 | 1 | Twin of inbound quality |
| LP-DEV-003 | Event catalog completeness (`payment.succeeded/failed`) | partial | Partial | 1 | 1 | D5 webhooks-first |
| LP-MSG-005 | Delivery log in ops | backend-only | Partial | 1 | 2 | Support |
| LP-TAX-009 | Un-hide invoicing | absent | Later | 2 | 0 | ADR 023 reverse |
| LP-TAX-002 | Consolidation dry run | backend-only | Later | 2 | 0 | Legal lie risk |
| LP-TAX-003 | TIN at checkout | backend-only | Later | 2 | 0 | Pillar 2 |
| LP-COM-008 | Proration | absent | Later | 3 | 0 | Stripe Billing expectation |
| LP-COM-010 | Portal self-serve | partial | Later | 3 | 0 | Hard-cancel only today |
| LP-TRU-005 | Ledger MRR | absent | Later | 3 | 1 | Board metric |
| LP-MSG-003 | Real WhatsApp | absent | Later | 4 | 0 | ADR 021 keep; honesty first |
| LP-TRU-006 | Xero sync | absent | Later | 4 | 1 | ADR 021 keep |
| LP-DEV-008 | Commerce M2M admin | absent | Later | 4 | 2 | After D5 ceiling |

---

## How to update the tracker later

### Standing rule

**This folder is tracker only. No implementation.** Filling a cell is research. Changing `Ours` from `absent` to `partial` requires evidence that **code in `lazuar-pay` already changed** in some other program. If the code has not changed, the cell does not change.

### File jobs

| File | Job | Who writes |
|------|-----|------------|
| [`plans/007-feats/20-sequencing-and-tracker-schema.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/20-sequencing-and-tracker-schema.md) (this file) | Constitution. ID scheme, columns, waves, rubric. | Parent, rarely. Edit when adding a column, a category, or a wave lock. |
| [`plans/007-feats/00-checklist-tracker.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md) | Living matrix + implement-later queue. | Parent after each sibling lands; small patches anytime. |
| [`plans/007-feats/00-evaluation.md`](/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-evaluation.md) | Narrative verdict after all 20 analyses. | Parent, once; amend, do not rewrite weekly. |
| `01`–`19` under `plans/007-feats/` | Uncondensed evidence. | Subagents. They **request** IDs; they do not mint. |
| `impl/` under this folder | Forbidden as a place to hide product work. If a future impl program exists, it lives under a **new** `plans/00x-…`, not here. | — |

### Promotion rule (sibling → tracker)

1. Sibling uses an existing `LP-CAT-NNN` or asks the parent for a new one.  
2. Parent adds/adjusts **one row** in `00-checklist-tracker.md`.  
3. Parent fills competitor cells only where the sibling cited a source (help center URL, pricing page, or operator interview). Empty cells stay blank, not `N`. Blank means “not researched.” `N` means “researched absent.”  
4. Parent sets `Ours` from `01` (inventory), not from marketing.  
5. Parent sets `V`, `W`, `P`, `Class` using this constitution. A sibling may *recommend* a wave; the constitution wins on conflict (e.g. sibling wants WhatsApp in Wave 1 → denied unless D2 is reversed in an ADR).  
6. Parent adds a one-line pointer in `Src`.  
7. Parent updates the implement-later queue if the row is not `shipped`/`Ours`/`Never`.

### When `01`–`19` disagree with this seed

| Conflict | Winner |
|----------|--------|
| `Ours` depth | `01` inventory |
| Who belongs in **Loc** / whether to add **Stf** or **Sea** | `02` then this file (column add is a constitution edit) |
| Stripe vs Chargebee vs Paddle column membership | `03` then this file |
| Cell Y/P/N for a named vendor | The per-competitor sibling (`04`–`10`) |
| Wave inside a domain | This file’s wave locks, then the domain sibling’s *priority inside the wave* |
| New job | New ID, this taxonomy |
| “We should become MoR / marketplace / LMS” | `LP-XX` + this file. Sibling may argue; it may not delete the trap row. |

### Changing a wave or opening the next wave

1. List Wave *n* exit criteria from this file.  
2. Every row with `W=n` and `Class` in `{must-my, table-stakes}` is `shipped` **or** explicitly waived in `00-evaluation.md` with a date and owner.  
3. Honesty claims that Wave *n* forbids (WhatsApp, tax invoices, Fiuu, …) are gone from README and ops copy.  
4. Parent writes a dated note at the top of `00-checklist-tracker.md`: `Open wave: n+1 as of YYYY-MM-DD`.  
5. Only then may `P` values for wave `n+1` be treated as a now-queue.

Do not open Wave 2 if Wave 0 ledger/credit lies remain. Do not open Wave 4 WhatsApp if README still needs an honesty patch.

### Changing `Ours`

Allowed values only. Evidence required:

- `shipped` ← path to UI **and** API **and** a test or operator note.  
- `partial` ← what works + what does not, in `Notes`.  
- `backend-only` ← API/job path + `[MVP-HIDE]` or missing route.  
- `absent` ← grep negative or stub citation (`ConsoleMessagingService`).  
- `refuse` ← ADR or `LP-XX` rationale.  
- `later` ← wave ≥ 3 or explicit park.

It is forbidden to set `shipped` because “Phase A checkbox is ticked” if the audience cannot demo it. Phase checkboxes are engineering; `Ours` is product.

### Adding rows after the first 80

1. Pick the category. Take `max+1`.  
2. One job.  
3. Assign `Class` with the rubric tests. If you cannot, it is not a row.  
4. Assign `W` from the wave intents, not from enthusiasm.  
5. Add to the implement-later queue if not `shipped`/`Never`.  
6. Do not insert into the middle of a category’s numbering.

### Killing or refusing a row that was in the first 80

Do not delete the ID. Set `Ours=refuse`, `V=Never`, `W=—`, `Class=trap` (or `later` if it is merely parked). Write one sentence in `Notes`. Deleting IDs is how zombie features return under a new number.

### Header flags (honest dashboard)

`00-checklist-tracker.md` should start with a small flag table, updated when claims change:

| Flag | Seed value (2026-08-16) | May become shipped when |
|------|-------------------------|-------------------------|
| Closed recovery loop (vaulted fail → email → pay → clear) | **in-repo / operator residual** | Wave 0 exit #1 said without asterisks |
| WhatsApp dunning | **not a product** (`WhatsAppEnabled=false`, console transport) | `LP-MSG-003` shipped |
| LHDN merchant UI | **lobotomized** (ADR 023) | Wave 2 `LP-TAX-009` |
| Xero | **absent** | `LP-TRU-006` |
| Extra rails (Fiuu/Xendit/…) | **absent** | Wave 4 pain-gated |
| Commerce M2M admin | **deferred (D5)** | `LP-DEV-008` |
| MoR / take-rate | **Never** | ADR 019 reversed |
| Community / Vault | **killed** | ADR 022 reversed |

### What the parent should paste first into `00-checklist-tracker.md`

1. Title, date, link to this file.  
2. “How to read a cell” + competitor column legend (copy from §ID scheme and §Competitor column set).  
3. Header flags table.  
4. Implement-later queue (seed above).  
5. Nine domain tables with the **exact header**:

```markdown
| ID | Feature | Ours | Inf | Loc | Str | MoR | Sub | Acc | V | W | P | Class | Src |
```

6. Trap appendix `LP-XX-001`–`012` with all competitor cells `—` or `Y` (they have it) and `V=Never`.  
7. A one-line reminder: **Do not implement from this file.**

### What “done” means for *this* program

This competitor-feature program is done when:

- `plans/007-feats/00-checklist-tracker.md` contains the 80 rows + XX traps + filled cells from `01`–`19`.  
- `plans/007-feats/00-evaluation.md` names who we compete with, what we will implement **later**, and what we refuse.  
- No PR in `plans/007-feats/` has touched `apps/` or `packages/`.

It is **not** done when Wave 0 code is written. That is a different program, and much of Wave 0 **code already exists**. The tracker’s job is to stop us from pretending the leftovers are strategy, and to stop us from rebuilding vitamins because Chargebee’s marketing site is long.

---

**Absolute paths for this constitution**

| Concern | Path |
|---------|------|
| This constitution | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/20-sequencing-and-tracker-schema.md` |
| Living matrix | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-checklist-tracker.md` |
| Parent eval | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/00-evaluation.md` |
| Folder index | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/README.md` |
| Product repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Direction | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/00-what-we-need-to-do-next.md` |
| ADR 021 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` |
| ADR 023 | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` |
| Backend checklist | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/001-backend/001-backend-solidification-checklist.md` |

Do not write tracker files under `/Users/akmalfirdaus/Code/saas/aura/`. Aura’s `plans/002-feats` is a different product’s program.

---

*End of constitution. Subagent 20 of 20. Full text on purpose. No implementation.*
