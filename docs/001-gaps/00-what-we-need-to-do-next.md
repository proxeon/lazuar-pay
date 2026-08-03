# What we need to do next — same page

**Date:** 2026-08-03  
**Companion:** Full uncondensed subagent reports in this directory (`01`–`20`).  
**Nature:** Direction and sequencing narrative — not a task list, checklist, or implementation plan.

This document assumes the gap reports under `docs/001-gaps` as evidence and focuses on *what “solid” means* for Lazuar as a CaaS / compliance backend.

---

## Where we actually are

We have a **credible modular monolith** with real product bones: multi-tenant workspaces, BYOK payment gateways, commerce checkout/subscriptions, a double-entry + credit-wallet skeleton, LHDN as a serious compliance product, TypeSpec-first contracts, and ops/portal UIs that already feel productized in places (especially payment config, dunning *configuration*, and workspace webhooks).

What we do **not** have yet is a **closed, trustworthy revenue and integration surface**. Several things look finished in the UI or README but are incomplete loops in the backend: dunning that doesn’t always start; webhooks that can silently drop; “native WhatsApp” that doesn’t really send; API keys that exist only for LHDN and are too powerful; a developers hub that documents the modular monolith’s edges rather than “how a third party integrates Lazuar.”

The next phase is less “add modules” and more **make the money and machine-integration paths honest and finishable**.

---

## The product contract we should align on

Three audiences, three auth models, three surfaces. If we keep mixing them, the backend will stay mushy.

1. **Humans (ops, portal, superadmin)**  
   Browser sessions: cookies / JWT. Tenant context via workspace selection. This is fine and should stay.

2. **Integrators (ERP, SaaS backend, Zapier, custom Next apps)**  
   Long-lived **credentials created in our product UI**, used against **documented integration APIs**, plus **outbound webhooks**. Not user JWTs, not scraping admin OpenAPI.

3. **Gateways and vendors (Stripe, Billplz, Resend, MyInvois)**  
   Signed inbound webhooks and BYOK secrets. Already the strongest pattern we have; inbound quality should be the bar for *outbound* quality.

Everything below should reinforce that split.

---

## Theme 1 — Make revenue recovery real (dunning + payments)

**Intent:** “Failed payment → customer gets nudged → we retry sensibly → they pay or we suspend/cancel → access and metrics update.”

**Today’s problem:** The *configuration* of dunning is ahead of the *engine*. The primary online path (vaulted card / off-session charge) often never cleanly enters past-due dunning, retries are constrained by data design, success paths don’t always rehydrate subscription state, and the WhatsApp story is orchestration + console logging rather than a production channel. So we are selling a recovery engine that, for the most important segment, may never run or may never finish.

**Evidence:** `01-dunning-engine.md`, `06-payments-module.md`, `08-communications-module.md`, `02-payment-webhooks.md`, `17-background-workers.md`.

**What “done” looks like (conceptually):**

- A failed renewal is a first-class business event, not a log line. That event is what starts (or continues) a dunning *run*, not a vague hourly scan of statuses that never flipped.
- A dunning run is a versioned journey: when a sub enters recovery, it locks onto a campaign snapshot so mid-flight edits don’t spam or skip customers.
- Retries (auto-charge) and messages (email/WhatsApp) are deliberate steps with catch-up semantics if workers lag—not “only if the clock lands on exactly day N.”
- Successful payment in recovery always clears arrears, advances billing period, clears dunning, and attributes recovery metrics—whether the customer paid via magic update-payment link or a silent off-session success.
- Messaging copy actually resolves variables and uses real deep links; WhatsApp is either a real provider with credits and failure visibility, or we stop claiming it as a differentiator until it is.

**Flexibility we actually need (without boiling the ocean):**  
Not Stripe Smart Retries ML on day one—but: configurable schedules, multi-channel steps, grace/final actions that work, payment-method-aware campaigns (online vs manual), pause/resume for CS, and gateway response awareness at least enough to stop thrashing hard declines. The current “day offset + string action type + hourly job” model can evolve toward that; it should not stay as a half-wired status scanner.

**Sequencing note:** Fix the **closed loop and metadata** before redesigning the campaign builder. A flexible builder on a broken entry path is theater.

---

## Theme 2 — Treat webhooks as product infrastructure (inbound + outbound)

**Intent:** Money and lifecycle events must be **receivable, durable, auditable, and relayable** to customer systems.

### Inbound (gateways → Lazuar)

We already understand the multi-gateway, stateless-metadata story. The next step is **operability and money safety**: process “received” vs “fulfilled” as separate phases; dedupe on business payment identity not only provider event ids; never ACK success then permanently drop domain work; emit failures so commerce/dunning can react; handle refunds/disputes as first-class; keep raw intake for replay/support.

The mental model should be: **webhooks are the cash register bell**, not a best-effort side effect.

**Evidence:** `02-payment-webhooks.md`, `06-payments-module.md`, `15-event-driven-architecture.md`.

### Outbound (Lazuar → customer apps)

This is the CaaS unlock path and is currently the weak twin of inbound.

**Product model we should agree on:**

- Tenant registers one or more **endpoints** in the console (not free-text product URLs that must magically equal a single workspace URL).
- Endpoints subscribe to a **versioned event catalog** (payment succeeded/failed, subscription lifecycle, order completed, LHDN validated/invalid, etc.).
- Every delivery is signed, retried, logged, and redriveable; secrets rotate; payloads are rich enough to act without an immediate second API call for basic unlock/revoke.
- LHDN callbacks and commerce fulfillment should eventually share the same delivery machinery, even if event names differ by product.

**Today’s silent “URL must match exactly or nothing happens” behavior is a product bug**, not a feature. Until that’s fixed, “we do webhooks” is not a trustworthy sales claim.

**Evidence:** `18-outbound-customer-webhooks.md`, `04-developers-page-dx.md`, `10-one-identity-module.md`.

---

## Theme 3 — Integration credentials and a real developer product

**Intent:** Integrator flow = **create workspace → generate credentials in frontend → call integration APIs → receive webhooks**. JWT remains for humans only.

**Today’s problem:** LHDN-shaped keys are a good *prototype* of secret hashing and live/test prefixes, but they live in the wrong module, authorize too broadly, lack lifecycle UX, and the public “developer” experience is mostly Scalar dumps of internal-ish APIs (including Ops). There is no platform-wide “API keys” product.

**Evidence:** `03-api-auth-credentials.md`, `04-developers-page-dx.md`, `09-lhdn-module.md`, `10-one-identity-module.md`, `13-typespec-api-contracts.md`, `19-frontend-backend-integration.md`.

**What we should build toward:**

1. **Platform credentials** owned by identity/workspace (One or a small platform capability), not buried only under LHDN.
2. **Scopes / product grants** so a key can submit e-invoices without also administering products, minting more keys, or touching payment config.
3. **Console UX** under Developer: name, live/test, create once, list prefixes, revoke, last used later.
4. **Docs that teach integration**, not the modular monolith:
   - Authentication (keys, not JWT)
   - Event catalog + signature verify
   - Commerce public/M2M surface (checkout/subscription as product, not only portal HTML)
   - LHDN as the polished compliance product (already closest)
5. **Developers-page** becomes: integration hub (guides + product APIs) with deep links into the console for keys; demote or gate pure internal surfaces (Ops chat, full One admin).

**Commerce M2M API** is the strategic gap for CaaS: today external parties mostly get public portal/checkout routes and admin JWT. For “power the Buy button from your app,” we need a stable, key-authenticated surface and the webhook story above—not more admin OpenAPI.

---

## Theme 4 — Financial truth and prepaid utility wallet

**Intent:** Billing is the ledger of record; credits meter infrastructure (LHDN, WhatsApp, etc.) without taxing GMV.

**Where we are:** Skeleton is good (balanced posts, wallet with concurrency ideas, PDF receipts). Trust is not: summary math under reversals, dual charging on LHDN, B2C consolidation selection that doesn’t match how receipts are stamped, dead deferred-revenue path, top-up accounting dual-posting, thin tests.

**Evidence:** `05-billing-module.md`, `09-lhdn-module.md`, `16-testing-coverage.md`.

**Next step mindset:** Before adding more accounting features, **make the existing posts and meters trustworthy** for the paths we already sell (gateway payment, refunds, credit top-up, LHDN submit, WhatsApp send). “Audit-grade” is a destination; “no silent double charge / no wrong consolidated tax batch” is the near bar.

---

## Theme 5 — Reliability plumbing (so features stop rotting)

Several product bugs are really **platform bugs**:

- Outbox rows that never publish (notably LHDN and CRM paths).
- Outbox/inbox failures marked processed once and dropped.
- Domain handlers running inline on the bus without a consistent retry story.
- Hourly engines that assume a single API instance.
- Fail-open tenant filters when context is empty; a few unauthenticated or under-authorized surfaces.

**Evidence:** `12-buildingblocks-host.md`, `15-event-driven-architecture.md`, `17-background-workers.md`, `14-tenant-isolation.md`.

**Shared agreement:** New product features should not pile onto undrained outboxes or “log and forget.” Closing the integration-event pipeline and isolation edges is part of solidifying the backend, not a separate ops polish project.

---

## Theme 6 — Contract honesty (TypeSpec, UI, SDKs)

Frontends and docs currently invent or omit routes (portal cancel, subscriber actions, LHDN path double-prefix, list keys). That erodes trust between teams and integrators.

**Evidence:** `13-typespec-api-contracts.md`, `19-frontend-backend-integration.md`, `07-commerce-module.md`.

**Agreement:** TypeSpec (and generated clients) should describe **what ships**. Phantom UI against imaginary APIs should either get real endpoints or be removed. Product-scoped docs should show **integration surfaces**, not every internal admin edge.

---

## What we are *not* doing next

To stay on the same page about scope:

- Not expanding Phase 2/3 roadmap (escrow, e-sign, community bouncers, multi-country tax beyond LHDN, marketplace).
- Not rebuilding a 15-app superapp narrative.
- Not optimizing dunning UX flexibility until the recovery loop works for vaulted failures.
- Not treating more Scalar tabs as “developer platform” without keys + webhooks + guides.

**Evidence:** `20-architecture-intent-vs-implementation.md`.

---

## Suggested narrative sequence (phases of *meaning*, not tickets)

### Phase A — “It actually recovers money and records it”

Close payment failure → dunning → message/retry → paid/cancel, with correct subscription and ledger/credit side effects. Make inbound webhooks and off-session paths consistent enough that silent drops are rare and debuggable.

### Phase B — “Machines can integrate without JWT”

Platform credentials + scoped access; Developer console for keys and webhooks; outbound delivery that doesn’t silent-drop; docs that match. LHDN remains the gold-standard product slice; Commerce M2M and webhooks become the CaaS slice.

### Phase C — “We can operate and trust it”

Outbox retry/DLQ, tenant isolation fail-closed for HTTP, financial summary/consolidation correctness for paths we use, tests on money and auth, multi-instance safety before scaling API replicas.

### Phase D — “Differentiation”

Real WhatsApp (or honest demotion), richer dunning intelligence, full compliance UI re-surface when ready, Xero-class accounting later—on top of A–C, not instead of them.

---

## Success criteria (shared definition of “backend solidified”)

We are on the same page if we can say all of the following without asterisks:

1. A declined renewal for a vaulted subscription **enters recovery**, customers are contacted on channels we claim, and payment **ends recovery correctly** with metrics.
2. An integrator can **mint a key in the UI**, call documented APIs, and receive **signed, logged webhooks** for core lifecycle events—without using a user JWT.
3. Gateway payment success is **idempotent and reconcilable**; failures are visible to domain logic; support can explain what a webhook did.
4. Credits and ledger posts for LHDN/top-ups/payments **don’t double-count or lie** on the happy path.
5. Docs and developer hub describe **integration products**, not the internal module map.
6. Cross-module events that matter **leave the outbox** and can be retried when handlers fail.

---

## Open decisions we should still settle explicitly

These affect design, not just implementation order:

1. **Dunning model:** keep status+day engine with hard fixes, or move toward “dunning run + invoice/attempt” (more flexible, more design).
2. **WhatsApp:** commit to Meta Cloud as a near-term channel, or temporarily market email-first recovery.
3. **API key ownership:** platform keys with LHDN scopes vs keep product-local keys but constrain them strictly.
4. **Outbound webhooks:** workspace-level event bus only vs also per-product endpoints (both can exist; exact-match product URL list should not).
5. **Commerce external surface:** how much M2M API in the first integrator release (webhooks-only + public checkout links vs full subscription admin API).

---

## Bottom line

The codebase is past “empty scaffold” and short of “trust the money and trust the integrator path.” The next work is **closing loops and clarifying surfaces**: dunning/payments as a real recovery system; webhooks as durable product infrastructure; credentials and docs for machines, not session tokens and admin Swagger; financial and outbox plumbing so features don’t rot.

If this narrative matches product intent, the natural follow-on is a short design pass on **(A) recovery loop**, **(B) platform credentials + outbound webhooks**, and **(C) which Commerce events/APIs are v1 for integrators**—still design-level, still no implementation until explicitly kicked off.

**Implementation checklist (phase by phase):** [`plans/001-backend-solidification-checklist.md`](../../plans/001-backend-solidification-checklist.md)

---

## Related reports in this directory

| Report | Role |
|--------|------|
| [README.md](./README.md) | Index of all subagent analyses |
| [01-dunning-engine.md](./01-dunning-engine.md) | Dunning deep dive |
| [02-payment-webhooks.md](./02-payment-webhooks.md) | Inbound payment webhooks |
| [03-api-auth-credentials.md](./03-api-auth-credentials.md) | JWT vs integration credentials |
| [04-developers-page-dx.md](./04-developers-page-dx.md) | Developer hub / DX |
| [18-outbound-customer-webhooks.md](./18-outbound-customer-webhooks.md) | Lazuar → customer webhooks |
| [20-architecture-intent-vs-implementation.md](./20-architecture-intent-vs-implementation.md) | ADR/README vs code |
