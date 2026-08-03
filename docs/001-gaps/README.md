# 001 — Backend Gap Analyses (Full Subagent Reports)

**Date:** 2026-08-03  
**Repo:** `lazuar-hub`  
**Method:** 20 parallel explore subagents; each report is written **in full without condensation**.

This directory captures an exhaustive evaluation of the Lazuar modular monolith to identify gaps and solidify the backend. Product-owner starter concerns (dunning, webhooks/API, developers-page, integration credentials vs JWT) are covered in depth in the reports listed below.

---

## How to use

- Read individual reports as the **source of truth** for each area.
- Do **not** treat this README as a substitute for those reports.
- Prioritized fix themes that recur across reports are summarized only to orient navigation.

---

## Direction (start here)

| File | Focus |
|------|--------|
| [00-what-we-need-to-do-next.md](./00-what-we-need-to-do-next.md) | Shared narrative: themes, phases, success criteria, open decisions — no checklist/code |
| [../../plans/001-backend-solidification-checklist.md](../../plans/001-backend-solidification-checklist.md) | Phase-by-phase implementation checklist (0 → A → B → C → D) |

---

## Full reports (uncondensed)

| # | File | Focus |
|---|------|--------|
| 01 | [01-dunning-engine.md](./01-dunning-engine.md) | Dunning domain, job, flexibility, failed-charge → PAST_DUE loop |
| 02 | [02-payment-webhooks.md](./02-payment-webhooks.md) | Inbound gateway webhooks, idempotency, signatures, outbox |
| 03 | [03-api-auth-credentials.md](./03-api-auth-credentials.md) | JWT vs API keys, `API_CLIENT` over-privilege, credential lifecycle |
| 04 | [04-developers-page-dx.md](./04-developers-page-dx.md) | Developers hub = docs not integration console; credential UX missing |
| 05 | [05-billing-module.md](./05-billing-module.md) | Ledger, credits, B2C consolidation, double-charge, ABS summary bugs |
| 06 | [06-payments-module.md](./06-payments-module.md) | BYOK multi-gateway cashier, off-session, refunds, secrets |
| 07 | [07-commerce-module.md](./07-commerce-module.md) | Products, checkout, subscriptions, coupons, portal gaps |
| 08 | [08-communications-module.md](./08-communications-module.md) | Templates, Resend BYOK, WhatsApp console stub, suppressions |
| 09 | [09-lhdn-module.md](./09-lhdn-module.md) | MyInvois pipeline, keys as pattern, event/credit bugs |
| 10 | [10-one-identity-module.md](./10-one-identity-module.md) | Identity, workspace, authz holes, credential ownership |
| 11 | [11-ops-crm-messaging.md](./11-ops-crm-messaging.md) | Ops agent, CRM anonymize, Messaging dumb-pipe vs reality |
| 12 | [12-buildingblocks-host.md](./12-buildingblocks-host.md) | Shared infra, outbox/inbox, middleware, Lhdn outbox missing |
| 13 | [13-typespec-api-contracts.md](./13-typespec-api-contracts.md) | Spec vs impl drift, product docs purity, LHDN path bug |
| 14 | [14-tenant-isolation.md](./14-tenant-isolation.md) | Fail-open filters, IDOR, webhook ownership, public surfaces |
| 15 | [15-event-driven-architecture.md](./15-event-driven-architecture.md) | Event catalog, orphans, Lhdn/CRM undrained outbox |
| 16 | [16-testing-coverage.md](./16-testing-coverage.md) | Thin test net; money/dunning/webhooks/auth untested |
| 17 | [17-background-workers.md](./17-background-workers.md) | Job inventory, poison policy, multi-instance unsafety |
| 18 | [18-outbound-customer-webhooks.md](./18-outbound-customer-webhooks.md) | Lazuar → customer webhooks; URL-match silent drop |
| 19 | [19-frontend-backend-integration.md](./19-frontend-backend-integration.md) | Ops/portal/superadmin vs backend; phantom routes |
| 20 | [20-architecture-intent-vs-implementation.md](./20-architecture-intent-vs-implementation.md) | ADR/README vision vs shipping truth |

---

## Product-owner starter themes (map into reports)

### 1. Dunning is problematic and less flexible
→ Primary: **01**, also **06** (off-session), **08** (WhatsApp stub), **17** (engine job)

Headline findings (see full report for evidence):

- Vaulted charge failures often never enter `PAST_DUE` → dunning never starts for the main SaaS path.
- `ChargeAttemptLogs` unique index conflicts with multi-retry intent.
- Off-session success metadata incomplete → renewals/recovery may not clear dunning.
- Step matching is exact calendar-day only (no catch-up); campaign edit regenerates step IDs.
- Default messaging ships broken `{{plan_name}}`; WhatsApp is console-only.

### 2. Webhooks and API are worse
→ Primary: **02**, **18**, also **15**, **13**, **03**

Headline findings:

- Inbound: event-id idempotency (not payment-id), Razorpay unsafe EventId fallback, outbox poison messages marked processed, failures never published.
- Outbound: product URL must **exactly match** workspace webhook URL or events silently drop; thin payloads; LHDN fire-and-forget path separate and weaker.
- No platform event catalog, multi-endpoint filters, or Svix-grade delivery UX.

### 3. Developers-page focuses on backend API, not integration APIs
→ Primary: **04**, also **13**, **03**, **19**

Headline findings:

- Hub is Scalar OpenAPI only (One/Ops/Billing/LHDN) — no credentials console, guides, or SDK install.
- Ops “Developer” nav is outbound webhooks only, not API keys.
- Commerce public CaaS surface is missing from the hub; Ops internal APIs are published publicly.

### 4. Integrations should use generated credentials, not JWT
→ Primary: **03**, also **04**, **09**, **10**, **19**

Headline findings:

- Human sessions = HttpOnly JWT cookies (correct for browsers).
- Machine auth exists only as LHDN-scoped `sk_live_` / `sk_test_` keys, but `API_CLIENT` is in **OrgAdmin** → over-privileged across admin modules.
- No scopes, list keys endpoint, last-used, rotation, inbound rate limits, or Ops UI for key lifecycle.
- Keys live in `lhdn.DeveloperApiKeys` while middleware is platform-global — wrong ownership for CaaS M2M.

---

## Cross-cutting P0 themes (navigation only)

These appear repeatedly; details and file evidence live in the numbered reports:

1. **Close money loops:** payment failed → PAST_DUE → dunning; off-session metadata; refunds; ledger/credit double-charge.
2. **Outbox correctness:** Lhdn + CRM missing publisher jobs; poison messages always marked processed; no DLQ/retry.
3. **Integration credentials:** platform keys + scopes; deny `API_CLIENT` on key/admin management; frontend generate/reveal/revoke.
4. **Outbound webhooks:** fix silent URL match; multi-endpoint; event catalog; unify LHDN delivery.
5. **Contract truth:** TypeSpec vs endpoints drift (portal cancel, LHDN path double-prefix, list keys, net-profit); phantom Ops UI routes.
6. **Tenant isolation fail-open** when `TenantId` empty; Ops conversation filter overwrite; unauthenticated `/messaging/notify`.
7. **Tests:** expand CI to all projects; webhook/dunning/ledger/auth first.

---

## Suggested reading order for solidifying backend

0. `00-what-we-need-to-do-next.md` (shared direction)  
1. `01-dunning-engine.md` + `02-payment-webhooks.md` + `06-payments-module.md`  
2. `03-api-auth-credentials.md` + `04-developers-page-dx.md` + `18-outbound-customer-webhooks.md`  
3. `05-billing-module.md` + `15-event-driven-architecture.md` + `17-background-workers.md`  
4. `14-tenant-isolation.md` + `13-typespec-api-contracts.md` + `16-testing-coverage.md`  
5. Module deep-dives as needed (`07`–`12`, `19`–`20`)

---

*Analyses generated by Grok explore subagents. Content intentionally uncondensed per request.*
