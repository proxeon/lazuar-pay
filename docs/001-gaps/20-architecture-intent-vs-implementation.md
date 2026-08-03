<!-- Source subagent: 019fc650-3515-7c20-8652-516d6df72466 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Architecture Intent vs Implementation Gap Analysis

**Scope:** Lazuar Hub at `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Focus:** Solidifying backend as CaaS / compliance platform with proper integration APIs  
**Method:** Full read of ADRs (esp. 004, 006, 007, 009, 019–023), root README, `apps/lazuar-api/docs/*`, TypeSpec/developer hub; code spot-checks only where claims need validation.

---

## Stated Product Vision

Three successive strategy layers are still coexisting in docs:

| Layer | Source | Claim |
|--------|--------|--------|
| **CaaS / headless** | README, ADR 019 | Sovereign checkout + billing + compliance engine; BYOK gateways; no CMS; creators bring their own frontends |
| **Compliance CaaS (3 pillars)** | ADR 021 | Transaction + government tax only; B2C consolidation, B2B TIN+instant LHDN, cross-border zero-rated; kill vitamins |
| **Phased Pure CaaS MVP** | ADR 023 | Ship checkout + dunning first; **UI-lobotomize** LHDN/B2B surfaces; backend “dark matter” stays |

**Root README positioning (current marketing truth):**

> “A Sovereign Checkout, Billing, and Compliance Engine for Asian Creators and B2B SaaS Founders.”  
> … multi-gateway orchestration, double-entry ledgers, automated WhatsApp dunning, and LHDN tax e-Invoicing …

**ADR 019 (CaaS pivot) — identity:**

> “Lazuar is no longer a collection of 15 website builders; it is a Sovereign Checkout & Fulfillment Engine (Checkout-as-a-Service).”

**ADR 021 (compliance pivot) — tighter constraint:**

> “Lazuar is exclusively a Compliance-First Checkout Engine (Compliance CaaS).”  
> “If a feature does not directly facilitate a transaction or keep a business legally compliant, we will not build it.”

**ADR 023 (GTM reality check):**

> “Time-to-Market and early cash flow validation supersede launching with the ultimate moat.”  
> “Hyper-Focused MVP: … High-converting checkout links with automated dunning.”

**Vision tension (documented, not accidental):**  
ADR 021 says compliance is the moat; ADR 023 deliberately **hides** that moat in UI while README/ADR 019 still market LHDN + WhatsApp + ledger. Backend has substantial compliance machinery; product surface is Pure CaaS.

---

## ADR Summary Relevant to Backend Solidification

| ADR | Decision relevant to CaaS / integration APIs | Implementation posture (summary) |
|-----|-----------------------------------------------|----------------------------------|
| **001** | 4-project module layout, schema isolation, inbox/outbox, boundary tests | **Largely held** for active modules; CRM has no `Application` project (exception) |
| **003** | Prefer integration events over sync building blocks | **Held** for payments → commerce → messaging path |
| **004 / 009** | Stateless Payments; Billplz metadata via callback query string; runtime event dispatch | **Implemented** for Billplz + webhook endpoints |
| **005** | TypeSpec → OpenAPI → TS + C# DTOs | **Working**; generated types committed |
| **006** | External TypeSpec DTOs ≠ internal MediatR contracts; endpoints as ACL | **Structurally held** |
| **007** | Product-scoped developer API references | **Partial** — One/Ops/Billing/LHDN only; **Commerce missing from hub** |
| **010** | XML templating (Scriban) + XSD for LHDN | **Present** in Lhdn module |
| **011** | Publish LHDN SDKs (npm/NuGet) | **Packages exist** (`@lazuar/lhdn-sdk`, .NET SDK); ops runbook exists |
| **014** | Super-app module catalog (15 apps) | **Stale** vs 019–023; still documents Community as retention core |
| **015** | No CMS; headless payment links | **Held** for MVP checkout |
| **016** | Domain strategy `api` / `ops` / `portal` | **Held** in deploy layout |
| **018** | Marketplace later; structured metadata | **Not in code** |
| **019** | CaaS, BYOK, utility wallet, outbound HMAC webhooks | **Partial** — see sections below |
| **020** | Integration roadmap Phases 1–3 | **Phase 1 incomplete**; 2–3 aspirational only |
| **021** | Compliance CaaS; keep dunning + Xero; kill community DRM/vault | **Backend partial; Xero absent; dunning incomplete channel** |
| **022** | Hide then remove Community/Vault | **Further than ADR:** modules **gone** from `Modules/`; frontend hide remnants remain |
| **023** | UI lobotomy of B2B/LHDN | **Done** (`[MVP-HIDE]`); backend kept |

---

## Intent vs Reality: Dunning

### Stated intent

**README / ADR 019 / 020 / 021 / 023** all treat dunning as core MVP:

- ADR 020: Meta WhatsApp Cloud API, interactive “tap to pay” buttons, 95% open-rate recovery.  
- ADR 021: “**Keep: WhatsApp Dunning (Auto-retries).**”  
- ADR 019: WhatsApp dunning deducts from `TenantCreditBalance`.  
- ADR 023: compete on “Billplz (FPX) + Automated WhatsApp Dunning.”

### What exists (real)

| Capability | Status | Evidence |
|------------|--------|----------|
| Dunning campaigns CRUD + defaults | **Yes** | `DunningCampaign` aggregate, admin endpoints, ops-page builder |
| Background engine | **Yes** | `DunningEngineJob` (hourly): pre-due, PAST_DUE steps, AUTOCHARGE, grace → CANCEL/SUSPEND |
| Pause/resume per subscriber | **Yes** | Commands + Subscribers UI |
| Email dunning path | **Partial** | Dispatches `FulfillmentRequested` → Communications → `DispatchMessage` → Resend/BYOK email |
| WhatsApp dunning path | **Stub only** | `IMessagingService` → **`ConsoleMessagingService`** logs `[MESSAGING/SMS]`; **no Meta/Wati/Twilio** |
| Credit wallet for WhatsApp | **Wired but ineffective** | Cost check + `DeductTenantCreditCommand` only if send runs |
| Interactive WA pay buttons | **No** | Plain text bodies only; no Meta interactive message API |
| Template variable completeness in dunning | **Gap** | Handler fills `customer_*`, `business_name`, payment links; default campaign uses `{{plan_name}}` which is **not replaced** in dunning path |
| Off-session auto-retry | **Partial** | Emits `ExecuteOffSessionChargeIntegrationEvent` when vaulted tokens exist; gateway coverage varies |

### Conflicting / exceeding quotes

ADR 020:

> “Integrate native WhatsApp Interactive Buttons. When the Dunning Engine detects a failed payment, it sends: *‘Your payment failed. [Tap here to pay RM50 via FPX]’*. The checkout happens seamlessly inside the chat.”

**Reality:** Console logger; no interactive components; no in-chat checkout.

Default campaign WhatsApp step (code) is free-text with `{{update_payment_link}}` → portal URL, not FPX-in-chat.

### Bottom line

**Campaign orchestration is real; Asian conversion channel is not.** Docs and UI sell “WhatsApp dunning”; production dispatch is email-capable + console WhatsApp.

---

## Intent vs Reality: Webhooks

### A. Inbound payment webhooks (gateway → Lazuar)

**Intent (ADR 004 / 009):** Stateless Payments; self-describing callback URLs for dumb gateways; query → `Query-*` headers; reconstruct metadata.

**Reality: Strong alignment**

- `Payments/Infrastructure/Endpoints.cs` injects `Query-*` headers.  
- `BillplzGatewayAdapter` appends `?type=&reference_1=` and reconstructs metadata.  
- Stripe uses native `metadata`.  
- Commerce listens for `type == commerce_subscription | custom_payment_link` + `subscription_id`.  
- Idempotency playbook + `PaymentWebhookLog` documented (`docs/006-payment-webhook-idempotency-backfilling.md`).

**Gaps:**

- Docs still use **`community_subscription`** metadata examples; code uses **`commerce_subscription`**. Stale migration docs risk silent drops if anyone follows old docs.  
- ADR 014 still: “Emits `GatewayPaymentFailedIntegrationEvent` → Community (dunning)” — Community module removed; dunning is Commerce-status driven.

### B. Outbound developer webhooks (Lazuar → merchant SaaS)

**Intent (ADR 019):**

> “By listening to internal `GatewayPaymentCompletedIntegrationEvent` messages, our `OutboundWebhookDispatcherJob` will fire HMAC-SHA256 signed payloads to external URLs…”

**Reality: Architecture present; product semantics incomplete**

| Piece | Status |
|-------|--------|
| `TenantWebhookEndpoint` + `WebhookDeliveryOutbox` | Yes (`one` schema) |
| `OutboundWebhookDispatcherJob` | Yes — POST + `X-Lazuar-Signature` HMAC-SHA256, retries (5 attempts, exponential backoff) |
| Lifecycle → webhook | Yes for **https** fulfillment targets on activate/suspend/cancel/resume/order.completed/dunning final |
| Registration UI/API | Single endpoint per workspace (`GET/PUT .../webhooks`) — **singular**, not multi-endpoint fan-out |

**Critical behavioral gap:**

`OutboundWebhookEventHandlers` only enqueues if an **active** `TenantWebhookEndpoint` exists with **exact URL match** to the product fulfillment target:

```21:27:apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs
// FirstOrDefault: OrganizationId + Url == TargetUrl + IsActive
// if (endpoint == null) return;  // silent drop
```

So:

1. Product can list many `https://…` targets, but workspace model is **one** registered URL.  
2. Mismatch → **silent no-op** (same class of bug ADR 009 fixed for inbound).  
3. Dispatcher listens to **product fulfillment URLs**, not a stable event catalog subscribed per endpoint (Stripe-style).

### C. LHDN outbound webhooks

Separate subsystem: LHDN `WebhookSubscription` for tax document status callbacks (TypeSpec `/lhdn/webhooks`). Domain: compliance integrator, not CaaS fulfillment.

### Bottom line

Inbound payment webhooks: **production-grade pattern**.  
Outbound CaaS webhooks: **MVP plumbing**, not a documented multi-event developer platform.

---

## Intent vs Reality: Integration Credentials / External API

### Stated intent

- **Developer-first CaaS** (ADR 019): machine clients unlock SaaS via webhooks/API.  
- **LHDN as productized gateway** (ADR 007, 011, 014): “Submit clean JSON; we handle UBL + PKI.”  
- **BYOK** for gateways and Resend; utility wallet for LHDN/WhatsApp.

### What exists

| Surface | Auth | Scope |
|---------|------|--------|
| **LHDN Developer API keys** | `Bearer sk_live_*` / `sk_test_*` | Stored in **`lhdn.DeveloperApiKeys`**; middleware hashes + caches; role `API_CLIENT` allowed under `OrgAdmin` policy |
| **LHDN HTTP API** | JWT admin **or** API key | `/api/v1/lhdn/*` documents, TIN validate, cancel, webhooks, cert, key mgmt |
| **LHDN SDKs** | Published package layout | `packages/lhdn-sdk-ts`, `packages/lhdn-sdk-dotnet` + ADR 011 runbook |
| **Commerce public API** | Unauthenticated / magic-link tokens | Checkout, portal, coupons — **buyer** surface, not M2M tenant API |
| **Commerce admin API** | Cookie/JWT OrgAdmin | Products, dunning, subscribers — **ops console**, not external integrator keys |
| **Platform-wide API keys** | — | **Do not exist** outside LHDN table |
| **Gateway BYOK** | Per-tenant payment config | Stripe, Billplz, CHIP, Razorpay adapters present |
| **Email BYOK** | Tenant Resend config | Yes |
| **WhatsApp BYOK / platform keys** | — | **None** |

### Gaps for “proper integration APIs” (CaaS solidification)

1. **No Commerce Integration API** under API keys: create product, create checkout session, list subscriptions, refund trigger, retrieve customer — all missing as first-class M2M contract.  
2. **API key ownership is LHDN-scoped** (`lhdn.DeveloperApiKeys` queried via `LhdnSqlConnectionFactory`). Using LHDN keys as universal Lazuar credentials is a **domain leak** and wrong boundary for CaaS.  
3. **Developer hub omits Commerce** despite `docs-commerce.tsp` existing; `package.json` build does **not** compile it; `dist/commerce/` absent; `developers-page` only lists One, Ops, Billing, LHDN.  
4. **No signed webhook event schema versioning**, public event catalog, or test-delivery endpoint in TypeSpec docs.  
5. **No OAuth / restricted scopes** (e.g. `lhdn:write` vs `commerce:read`); API_CLIENT is all-or-nothing role on OrgAdmin routes it can hit.

### Conflicting quote (ADR 019)

> “… saving developers weeks of billing backend engineering.”

**Reality:** External developers can (with work) integrate **LHDN JSON→XML**, and receive **sparse fulfillment webhooks** if URLs are pre-registered. They cannot fully replace a billing backend via public Commerce APIs.

---

## Intent vs Reality: External vs Internal Contracts

### Stated intent (ADR 006)

> “TypeSpec-generated DTOs will solely represent the HTTP Edge Boundary, while internal `Contracts` projects will exclusively handle internal business operations and CQRS messaging.”  
> “Endpoints.cs [act] as an **Anti-Corruption Layer**…”

### Reality: Structure holds; hygiene drifts

**Aligned:**

- Modules keep `Contracts` with MediatR commands/events (`ICommand`, `IIntegrationEvent`).  
- TypeSpec → NSwag models used at Minimal API edges.  
- Pipeline `task gen` documented (ADR 005).

**Gaps / risks:**

| Issue | Detail |
|-------|--------|
| **ADR 005 vs ADR 007 split** | `main.tsp` for monorepo types; `docs-*.tsp` for Scalar — correct pattern, but Commerce docs entry not wired into build/hub |
| **Stale generated residue** | `api-types-dotnet` still contains `Telegram_invite_link` etc. after Community/Vault removal narrative |
| **ADR 022 Phase 1 said TypeSpec imports commented; gen unsafe** | Current `main.tsp` has **no** community/vault imports (Phase 2-ish on contracts); Community/Vault **module dirs absent** — ADR 022 text is partly obsolete |
| **Billing README still internal-contracts-era wording** | “From Community: ZeroAmount…” / “Community/Vault manage Access” — documents old ownership |
| **Cross-module event naming** | Commerce-owned fulfillment events; One owns outbound delivery — good boundary; payload is ad-hoc anonymous JSON, not versioned external schema |

**External contract quality for integrators:** LHDN TypeSpec is the only polished external product API. Commerce public TypeSpec is **portal-oriented**, not SDK-oriented.

---

## Intent vs Reality: Module Boundaries

### Stated pattern (README / ADR 001)

```
Modules/{Name}/ Application | Contracts | Domain | Infrastructure
```

### Live modules (code)

| Module | Role today | Notes |
|--------|------------|-------|
| **One** | CIAM, workspaces, outbound webhooks, presigned URL | Presigned-url relocated here (ADR 022 Phase 2 item partially done) |
| **Commerce** | Products, checkout, subscriptions, dunning | CaaS core |
| **Payments** | Gateway adapters, webhook ingest | Stateless intent held |
| **Billing** | Ledger, credits, B2C consolidation job, PDFs | Compliance + wallet |
| **Lhdn** | UBL, submit, keys, tax webhooks | Compliance product |
| **Communications** | Templates, broadcasts remnants, fulfillment comms | Still seeded with community/telegram copy |
| **Messaging** | Dispatch email/WhatsApp façade | WhatsApp = console |
| **CRM** | Client profiles | **No Application project** — handlers in Infrastructure |
| **Ops** | AI agent (hibernating) | Present |
| **Community / Vault** | — | **Not under `Modules/`** |

### Boundary health

- `ModuleBoundaryTests` lists active modules only (Community/Vault removed from test list) — **good**.  
- Physical schema isolation + inbox/outbox pattern still the norm.  
- **Doc debt:** ADR 014, Billing README, payment docs, Communications default templates still assume Community/Telegram universe.  
- **ADR 022** claimed Phase 1 hide with modules still present; **reality is deletion of backend modules** without finishing ADR’s full checklist (orphan frontend community files, generated telegram fields, DB schema drop TBD).

---

## Roadmap Items Not Reflected in Code

From **ADR 020** + root README, compared to adapters/modules:

### Phase 1 — “Un-Fireable Core” (claimed current)

| Item | Doc claim | Code reality |
|------|-----------|--------------|
| Local gateways BYOK | Billplz, Fiuu, SenangPay, CHIP, Xendit, Midtrans, Razorpay, Cashfree | **Stripe, Billplz, CHIP, Razorpay** only — **no** Fiuu, SenangPay, Xendit, Midtrans, Cashfree |
| Gov tax | LHDN, GSTN, Coretax, InvoiceNow | **LHDN only** (deep); others absent |
| Accounting sync | Xero, QuickBooks | **Zero** integration code |
| WhatsApp commerce | Meta Cloud API / Twilio / Wati | **ConsoleMessagingService only** |

### Phase 2 — High-ticket

Escrow.com, DocuSign/PandaDoc, Telegram/Discord bouncer, Keygen — **not implemented**.  
ADR 021 **kills** community DRM; ADR 020 Phase 2 still lists bouncer — **roadmap contradiction**.  
Escrow narrative still references **Vault** holding assets (ADR 020 §5) after Vault kill.

### Phase 3 — Borderless

Wise/PayPal MassPay, Capchase BNPL, BTCPay/Web3, Singpass/MyDigital ID — **not implemented**.

### Other ADR aspirations absent

| Source | Missing |
|--------|---------|
| ADR 019 | Open-source Astro/Next storefront templates repo |
| ADR 018 | Marketplace / `storefront-page` / catalog projection |
| ADR 014 | Bio, Form, Event, Academy, Giveaway, etc. as full apps |
| ADR 023 reverse | Re-expose LHDN UI when ready — backend ready-ish, UX intentionally off |

### ADR 021 keep list vs code

- **WhatsApp dunning:** orchestration yes, channel no.  
- **Xero:** not started — largest explicit “keep” gap for CFO loop.

---

## Strategic Recommendations

Ordered for **backend solidification as CaaS + compliance integration platform**.

### P0 — Make marketing claims true or rewrite them

1. **Either implement Meta Cloud WhatsApp adapter** behind `IMessagingService` (templates + utility credit) **or** demote README/ADR 020 language to “email dunning + WhatsApp-ready payloads.”  
2. **Fix dunning template variables** (`plan_name`, product price) in `FulfillmentRequestedIntegrationEventHandler` so defaults actually work.  
3. **Align metadata docs** (`community_subscription` → `commerce_subscription`) in `apps/lazuar-api/docs/006-…` and any Postman assets.

### P0 — Outbound webhooks as a real integration product

4. Decouple **event subscription** from **product fulfillment URL string**: register endpoints with event types (`subscription.activated`, `order.completed`, …); fan-out to all active endpoints.  
5. Stop silent drop when URL not registered — queue with error or validate on product save.  
6. Document payload schema in TypeSpec (`docs-commerce` or `docs-webhooks`) + Signature verification guide.  
7. Support **multiple** endpoints per tenant (current model is singular).

### P0 — Integration credential model for CaaS (not only LHDN)

8. Introduce **platform API keys** in **One** (or shared Auth building block), not `lhdn.DeveloperApiKeys`.  
9. Scope keys: `lhdn:*`, `commerce:read`, `commerce:write`, `webhooks:manage`.  
10. Keep LHDN keys as alias or migrate; middleware must not hard-query LHDN schema for all API clients.

### P1 — Commerce Integration API (the missing CaaS edge)

11. Wire **`docs-commerce.tsp`** into `api-spec` build + developers-page route `/commerce`.  
12. Expose M2M endpoints (API key):  
    - Products CRUD / list  
    - Create checkout session → URL  
    - Get subscription / cancel / pause dunning  
    - List transactions  
    - Configure fulfillment targets / webhooks  
13. Ensure endpoints map TypeSpec → internal Commands (ADR 006 ACL) — do **not** reuse admin cookie assumptions.

### P1 — Compliance path honesty under ADR 023

14. Keep LHDN backend + SDK as **standalone compliance product** (already strongest integration surface).  
15. When reactivating ADR 023 UI: B2B TIN capture, quotes, tax invoice download already partially backend-backed (`B2cConsolidationJob`, document handlers).  
16. Do **not** claim multi-country tax until GSTN/Coretax are real modules.

### P1 — Finish ADR 022 cleanup

17. Delete frontend orphans (`portal-page` community modules, telegram fields).  
18. Move Communications pages out of `modules/community` naming if still collides.  
19. Regenerate types; drop dead telegram/community DTO fields.  
20. Plan dormant `community`/`vault` schema drop.  
21. Rewrite Billing README golden rule (no Community/Vault).

### P2 — Phase 1 roadmap completion (true “un-fireable”)

22. **Xero** (ADR 021 keep): ledger → journal export/sync from Billing.  
23. Gateway coverage: prioritize **FPX depth** (Billplz/CHIP mature) over India/ID until MY CaaS wins.  
24. Replace Console messaging in production DI with real adapter + feature flag.

### P2 — Contract & doc governance

25. Tag ADRs that supersede others: **021/023 supersede 014/018 feature factory**; **022/021 supersede 020 §7 community DRM**.  
26. Archive or watermark ADR 014 acquisition apps as historical.  
27. Single “Integration API” README linking LHDN SDK + Commerce webhooks + auth.

### P3 — Do not build yet (explicit non-goals while solidifying)

- Escrow, e-sign, Keygen, mass payouts, BNPL, Web3, national KYC, marketplace (018).  
- Rebuilding Community/Vault as first-party apps.  
- Meta interactive commerce until plain WhatsApp delivery is production-true.

---

## ADR-by-ADR Notes

### ADR 001 — Implementing new module  
**Status vs code:** Pattern intact for Commerce/Billing/Payments/Lhdn/One/Communications.  
**Gap:** CRM lacks Application layer; Messaging is thin.  
**Exceeds nothing** — baseline still valid.

### ADR 002 — Building blocks  
**Held.** `ConsoleMessagingService` is the right extension point for WhatsApp — but still the only implementation.

### ADR 003 — Events over building blocks  
**Held** for post-payment and dunning comms.  
**Exception usage correct** for password/JWT/R2.

### ADR 004 — Payment integration pitfalls  
**Fixed in code** for Billplz + runtime event type.  
**Doc debt:** Community examples; metadata type rename.

### ADR 005 — TypeSpec pipeline  
**Operational.**  
**Gap:** product-scoped docs incomplete (Commerce).  
**Note:** ADR says `dist` gitignored; monorepo still has `packages/api-spec/dist/**` present in tree — process inconsistency.

### ADR 006 — External vs internal contracts  
**Architecturally accepted and largely followed.**  
**Risk:** Using NSwag DTOs deep in handlers (e.g. email config property access) blurs ACL slightly but not catastrophic.

### ADR 007 — Product-scoped API references  
**Partial implementation.**  
**Quote exceeding reality:**

> “… generate distinct OpenAPI artifacts for each business domain … e.g. developers.lazuar.com/one, …/community”

**Reality:** One, Ops, Billing, LHDN only. Community path killed; **Commerce never landed on hub**. Checklist in ADR still teaches Vault as example — obsolete.

### ADR 009 — Stateless webhook metadata  
**Implemented for Billplz.**  
**Quote still true for Payments design:**

> “The Payments module is designed to be completely **stateless** regarding checkout sessions.”

**Note:** Commerce **does** hold checkout sessions — correct split.  
**Echoing problem now lives in OutboundWebhook silent drop** (see Webhooks section).

### ADR 010 — XML templating  
**Implemented** (Scriban templates, XSD validators in Lhdn). Core compliance moat engineering is real even under MVP-HIDE.

### ADR 011 — SDK publishing  
**Packages + runbook exist.** Publish maturity / public package status not verified from code alone.  
**Scope:** LHDN only — no Commerce SDK.

### ADR 012 / 013 — Frontend nav / modules  
Not primary backend solidification; ops-page dunning UI is live.

### ADR 014 — Apps catalog  
**Largest stale document.** Still frames 15-app superapp, Community as dunning owner, Event as validated revenue, etc.  
**Conflicts with** 019/021/022/023. Treat as historical inventory, not roadmap.

### ADR 015 — CMS trap  
**Aligned** with current checkout UX.  
**Open:** promised Astro templates still missing.

### ADR 016 — Domain strategy  
**Aligned** (api/ops/portal).  
**Stale examples:** community/vault portal paths.

### ADR 017 — Portal vertical slices  
**Structure exists**; community routes partially notFound/MVP-hidden.

### ADR 018 — Marketplace  
**Not started.** Correctly deferred; still conflicts with pure Compliance CaaS narrative if reactivated early.

### ADR 019 — CaaS pivot  
**Strategic direction of monorepo.**  
**Exceeds implementation:**

- WhatsApp utility moat  
- Polished developer outbound webhooks  
- Storefront templates  

**Implemented:** BYOK gateways (subset), ledger, commerce checkout, fulfillment target model, HMAC dispatcher skeleton, credit wallet domain.

### ADR 020 — Integration roadmap  
**Aspirational catalog.** Phase 1 overstated as “current.” Phase 2 includes items **killed** by 021. Needs rewrite into: (A) done, (B) next, (C) won’t do.

### ADR 021 — Compliance CaaS  
**Strategic north star for backend.**  

| Pillar | Backend | Product surface |
|--------|---------|-----------------|
| B2C consolidation | `B2cConsolidationJob` (28th MYT) + LHDN path | UI lobotomized |
| B2B TIN + instant invoice | LHDN validate + submit APIs | TIN/quote UI hidden (023) |
| Cross-border zero-rate | Not evidenced as first-class export tax engine | — |

**Keep Xero:** **not started** — critical honesty gap.  
**Kill community DRM:** backend modules gone; residual templates/UI copy remain.

### ADR 022 — Remove Community/Vault  
**Phase 1 narrative outdated:** modules not merely commented in `Program.cs` — **absent from Modules/**.  
**Still incomplete:** frontend orphans, type residue, DB drop, Communications folder naming, full gen hygiene.  
**Presigned URL** now under One (`/storage/presigned-url`) — Phase 2 prerequisite **partially satisfied**.

### ADR 023 — UI lobotomy Pure CaaS MVP  
**Implemented as described** (`[MVP-HIDE]` in ops/portal).  

> “Backend Dark Matter: … change absolutely nothing in the .NET backend”

**Mostly true** — LHDN/Billing jobs still register.  
**Trade-off accepted:** temporary loss of compliance differentiator while competing on FPX + dunning — **but dunning WhatsApp is not production-true**, so mitigation in ADR 023 is currently **overstated**.

---

## Executive synthesis (backend as CaaS / compliance integration platform)

```text
                    STATED                              REAL
Checkout + BYOK gateways          ████████░░  (Stripe/Billplz/CHIP/Razorpay)
Double-entry ledger               ██████████
Dunning orchestration             ████████░░
WhatsApp recovery channel         ██░░░░░░░░  (console stub)
Outbound HMAC webhooks            █████░░░░░  (single URL, silent match)
Commerce M2M Integration API      ██░░░░░░░░  (public portal only)
LHDN compliance API + SDK         █████████░  (strongest external product)
API key model (platform-wide)     ███░░░░░░░  (LHDN-table only)
Xero / multi-country tax          ░░░░░░░░░░
Doc/ADR consistency               ███░░░░░░░  (014/020 vs 021/022 drift)
```

**Strongest solid foundation:** modular monolith boundaries, payments webhook correctness, Commerce subscription/dunning engine, Billing ledger + credits, LHDN XML pipeline + developer keys/SDKs.

**Largest solidification gaps for integration APIs:** (1) production messaging channel, (2) reliable multi-endpoint outbound webhooks with public schemas, (3) platform API keys + Commerce M2M API + developer hub, (4) ADR/doc cleanup so intent matches shipping truth under ADR 023.
