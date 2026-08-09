# 004 — Maintenance questions roadmap (backend + TypeSpec)

**Status:** Living analysis for maintenance planning  
**Date:** 2026-08-09  
**Scope:** `apps/lazuar-api` (BuildingBlocks, SharedKernel, Modules, host, tests) + `packages/api-spec` (+ gen consumers: `api-types-dotnet`, `api-types-ts`, LHDN SDKs as contract surfaces)  
**Out of scope for this document’s implementation work:** application code changes; frontend apps (except where frontend residual debt forces backend/TypeSpec honesty decisions)

**Primary evidence sources (surveyed):**

| Source | Role |
|--------|------|
| `plans/001-backend/001-backend-solidification-checklist.md` | Phase 0–C largely complete with explicit residuals; Phase D open |
| `docs/001-gaps/00`–`21` | Full uncondensed gap analyses + Phase C acceptance notes |
| `docs/architecture-decision-log/` especially 001–007, 014, 019–023 | Intent, pivots, hide/remove Community/Vault, UI lobotomy |
| `apps/lazuar-api/docs/001`–`008` | Cross-module, SharedKernel, outbox runbook, webhook playbooks (some stale Community examples) |
| `docs/api-versioning.md` | Integrator versioning policy |
| Live tree under `Modules/*`, `BuildingBlocks/*`, `packages/api-spec/*`, `Directory.Packages.props`, `global.json`, `.github/workflows/ci.yml`, `Taskfile.yml` | Current implementation posture |

**How to use this document:** Treat it as a **maintenance roadmap and question bank**, not a replacement for `plans/001-backend` product solidification. Phase 0–C closed money/auth/isolation loops; this plan answers “what still smells, is fat, mis-owned, or will hurt us in six months?”

---

## Executive posture (after Phase 0–C)

Lazuar is a **credible modular monolith** with real money paths, platform API credentials, outbound workspace webhooks, LHDN compliance machinery, TypeSpec-first contracts, outbox retry/DLQ, fail-closed tenant filters on many HTTP paths, and a non-trivial module test suite.

What remains is **maintenance debt**, not empty scaffold:

1. **Product honesty debt** still open from Phase D / residuals (WhatsApp console-only, LHDN outbound fire-and-forget, deferred revenue dead path, dual API-key stores, marketing claims vs channels).
2. **Structural debt** (fat files, inconsistent layer placement, SharedKernel shell, hybrid outbox+inline handlers, CRM 3-project exception).
3. **Platform ops debt** (.NET 10 + preview package matrix, central package version skew, IMemoryCache multi-instance, migrate-on-boot, Prometheus/OpenTelemetry thinness, secret re-encrypt migrations).
4. **Documentation / ADR currency debt** (Community-era examples in api docs, ADR 014 super-app catalog, gap reports still name `lazuar-hub` / `*-page` paths).
5. **Deletion debt** (ADR 022 Phase 2 Community/Vault full removal not finished on FE/types/docs; backend modules appear already gone from `Modules/`).

**Guiding principle for maintenance sequencing:**  
Prefer **delete / freeze / document truth** over new modules. Prefer **chunk + reorganize inside existing modules** over extracting microservices. Prefer **closing dual paths and dual stores** over adding feature flags that never die.

---

# 1. What to remove / delete / improve

## 1.1 Delete or finish deleting (high value)

### ADR 022 Phase 2 — Community / Vault residual cleanup (backend + contracts + docs)

**Status (backend modules):** `Modules/Community` and `Modules/Vault` are **not present** under `apps/lazuar-api/Modules/`. That is further than ADR 022’s “hide” phase.

**Still remove / clean (backend-adjacent + TypeSpec + docs in scope):**

| Item | Why | Risk if left |
|------|-----|--------------|
| Stale Community examples in `apps/lazuar-api/docs/001-cross-module-communication.md`, `004-transactional-import-protocol.md`, `005-tenant-isolation-mapping-backfilling.md`, `006-payment-webhook-idempotency-backfilling.md` | Docs teach Community plan/subscription/lifecycle jobs | Wrong mental model for new engineers; dangerous import playbooks |
| `packages/api-spec/README.md` Community-first examples | Onboarding still oriented around removed product | TypeSpec contributors recreate dead patterns |
| `packages/api-spec/modules/messaging/models.tsp` comment “Templates migrated to Community” | Misleading | Template ownership confusion |
| AppOptions / comments referencing “Community Enrollment page” | Naming lie | Config confusion |
| Communications default template names “Community Welcome”, “Community Payment Success” | Product vocabulary leftover | Ops UI noise; seed clutter |
| ADR 014 super-app catalog still implying Community retention core | Strategy contradiction with 019–023 | Product/eng misalignment |
| Any remaining FE orphans / TypeSpec community/vault dirs (if still on disk outside backend) | ADR 022 Phase 2 | `task gen` / types bloat — **frontend out of code-edit scope here, but contract cleanup is in TypeSpec scope** |
| DB schemas `community` / `vault` if still present in shared DBs | Dead tables | Migration noise, security surface (“what still writes here?”) |

**Improvement action (ordered):**

1. Grep/doc pass: replace Community → Commerce / Messaging / Communications correctly; drop Vault import protocols or rewrite for R2/One storage.
2. Confirm no dormant EF migrations or Taskfile references to Community/Vault (Taskfile `api:migrations:*` already lists active 9 modules only — good).
3. If schemas still exist in production/dev Neon: deferred drop migration with backup note (ADR 022 step 8).
4. Close ADR 022 open decisions: data plan for any historical community/vault purchasers; whether Communications FE folder rename is required.

### Dead deferred-revenue surface (Billing)

**Evidence (Phase C.1 residual):** `RevenueRecognitionJob` unregistered; schedules not created from product periods; entity/table kept.

| Choice | When |
|--------|------|
| **Delete** job/entity/table if product will not sell deferred revenue near-term | Reduces lie surface and empty metrics |
| **Implement** schedule creation from product periods | Only if finance UI / Xero (ADR 021) is next revenue work |

**Maintenance recommendation:** Prefer **delete or mark obsolete with ADR** until Phase D accounting work starts. A dormant table with no writer is worse than no table.

### LHDN dual credential path — retire legacy after migration window

| Dual path | Location | Maintenance ask |
|-----------|----------|-----------------|
| `one.ApiCredentials` (platform) + `lhdn.DeveloperApiKeys` (legacy) | `ApiKeyAuthenticationMiddleware` dual-read SQL | Define cutover date; migrate remaining keys; remove Lhdn lookup + dual-subscribe revoke handlers |
| Dual `ApiKeyRevokedIntegrationEvent` (One + Lhdn) | `Program.cs` dual subscribe | Collapse to One event only after dual-read ends |
| Lhdn list/generate façade still talking about DeveloperApiKeys | Lhdn Application ports / domain | Ensure all mint/list/revoke is One-owned; Lhdn only scopes |

Leaving dual-read forever is a **security + cache-invalidation** tax (two stores, two revoke events, IMemoryCache keys).

### LHDN outbound webhook stack duplication

| Path | Quality |
|------|---------|
| One `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob` + signing | Durable, retries, multi-endpoint (Phase B) |
| Lhdn `WebhookSenderService` fire-and-forget | Weaker; residual B.4.4 |

**Remove or converge:** fire-and-forget as sole LHDN customer webhook path. Either route LHDN lifecycle through One dispatcher or give Lhdn the same outbox/signing primitives (shared building block). Do not “improve” both forever.

### Orphan / weak event types (cleanup catalog)

From historical gap analysis and residual checklist items (verify with grep before delete):

- Duplicate / unused twin types (e.g. Commerce copy of `ExecuteOffSessionChargeIntegrationEvent` if still present).
- Published-with-zero-consumers events (`GatewayRefundFailed` style) — either subscribe or stop publishing.
- Fictional TypeSpec fields (LHDN `events[]` on register if still phantom — Phase B residual).
- `ApiCreditPurchasedIntegrationEvent` style orphans if still unsubscribed.

**Improve:** maintain a living **event catalog** (markdown or TypeSpec docs) with Publisher / Subscriber / Outbox module. Gap report `15` is a template; refresh after Phase A–C rather than leave 2026-08-03 snapshot as truth.

### ConsoleMessagingService as production default

`Program.cs` registers `IMessagingService` → `ConsoleMessagingService`. WhatsApp steps are honesty-gated (email fallback / feature disable) but the **port still looks real**.

| Option | Maintenance meaning |
|--------|---------------------|
| Keep console + explicit ops “Email only” | Acceptable until D.1; document in README |
| Fail closed if WHATSAPP channel selected and no provider | Safer |
| Implement Meta Cloud (Phase D.1) | Only if product commits D2 |

**Remove** WhatsApp steps from default dunning campaigns if product will not ship Meta near-term — reduces support lies more than keeping disabled steps forever.

### Dead / misaligned docs claims

- README / marketing “automated WhatsApp dunning” while provider is console.
- Gap analyses still naming `lazuar-hub`, `ops-page`, `developers-page` (plan 002 renamed apps).
- ADR 014 vs 019–023 strategy stack without a single “current truth” watermark (partially done in C.8).

**Improve:** one **architecture truth page** (or root README section) that lists: active modules, auth models, channels that work, replica policy, secret model. Link ADRs as history, not as shipping claims.

---

## 1.2 Improve (do not delete — harden)

### Bulk secret re-encrypt migrations (C.7 residual)

Current pattern: encrypt on save; **legacy plaintext decrypt fallback**. Affects:

- Payment gateway ApiKey / WebhookSecret
- LHDN MyInvois client secret + PFX bytes
- Resend tenant API keys

**Improve:** one-off background migrator or admin SQL runbook that re-saves rows through vault; then **remove plaintext fallback** in a subsequent release. Leaving fallback forever means any DB dump is still a breach for old rows.

### Multi-instance API key cache (C residual)

Revoke uses `IMemoryCache` eviction. Single replica OK (deploy rule). Multi-instance → stolen key may live until TTL on other nodes.

**Improve options (pick one):**

1. Short TTL only (e.g. 60s) — simplest.
2. Redis / distributed cache for key hash → principal.
3. DB hit on every request for keys (with careful pooling) until Redis exists.

Document choice in deploy README next to replica=1 rule.

### Inbound payment webhook two-phase intake (A.5 residual)

Today: process in request path with strong idempotency.  
**Improve later:** persist raw webhook → async process for long gateway work / poison isolation. Not urgent if SLAs are fine; schedule when webhook latency or poison volume hurts.

### Outbound webhook product gaps (B residuals)

Still improve (not delete):

- Payload enrichment (customer email/name policy, amount, product slug).
- Delivery log response status + **redeliver** API.
- Secret rotate endpoint.
- SSRF baseline (HTTPS-only prod, private IP block).
- `payment.succeeded` / `payment.failed` catalog if sold as product.
- LHDN wire name alignment (`validated` vs `valid`).

### Observability (C.6 residual → production grade)

Present: `LazuarMetrics`, gauges job, `/health`, `/health/ready`, `/health/metrics`, correlation id.

**Improve:**

- Prometheus/OpenTelemetry scrape (System.Diagnostics.Metrics alone is not enough for many hosts).
- Separate counters for outbox vs inbox dead letters (shared name residual).
- Trace propagation into outbox publish / gateway calls.
- Alerting runbook thresholds (outbox lag, LHDN stuck, dunning cancel spike).

### Test honesty

| Gap | Evidence |
|-----|----------|
| CI `dotnet` job may omit `Modules.Ops.Tests` | `.github/workflows/ci.yml` ends after Billing project; Taskfile `api:test` includes Ops |
| Concurrent credit tests skip without Docker | Testcontainers residual |
| True `SKIP LOCKED` concurrency not under InMemory | Documented residual |
| Full host HTTP IDOR matrix thin | Command + EF filter gate |

**Improve:** align CI with Taskfile; mark flaky/opt-in tests explicitly; add one Postgres-backed multi-worker smoke if scaling beyond replica=1.

### Architecture tests expansion

Present: module boundaries, tenant filters, middleware allowlists.  
**Improve:** Minimal API reflection vs OpenAPI path diff (C.8 residual); forbid dual `HasQueryFilter` without tenant; forbid referencing Community namespaces if any stub remains.

---

## 1.3 Explicit “do not remove yet” (keep as dark matter)

Per ADR 023 **UI lobotomy** philosophy (backend kept):

- Billing ledger, PDF generation, B2C consolidation job, LHDN full pipeline, Ops chat backend, tax invoice APIs.

**Maintenance rule:** If a backend surface is intentionally dark:

1. Keep TypeSpec only if an internal client or future unhide needs it; otherwise mark `@doc("Internal / MVP-hidden")` and gate Scalar product docs.
2. Do not let dark APIs become accidental public integrator surface.
3. When unhiding (Phase D.3), treat as product launch with tests — not uncomment-only.

---

# 2. What to chunk into smaller files

Targets are **maintainability**, not ceremony. Prefer splitting when a file owns multiple bounded contexts or exceeds ~300–400 lines of mixed concerns.

## 2.1 Host composition root

| File | Approx size / smell | Split into |
|------|---------------------|------------|
| `src/Lazuar.Api/Program.cs` (~485 lines) | Env loader, Key Vault, Serilog, metrics DI, R2, JWT auth policies (many), CORS, MediatR scan, all modules, migrate-on-boot, middleware order, health, endpoint map | `Hosting/EnvBootstrap.cs`, `Hosting/AuthenticationExtensions.cs` (policies), `Hosting/ModuleRegistration.cs`, `Hosting/DatabaseMigrator.cs`, `Hosting/HealthEndpoints.cs`, keep thin `Program.cs` |

**Why:** Policy matrix alone (OrgAdmin + Integration* scopes) will grow with Commerce M2M / webhook manage scopes. Migrate-on-boot deserves its own testable unit.

## 2.2 Endpoint maps (fat Minimal API files)

| File | Status | Chunk plan |
|------|--------|------------|
| `Modules/One/Infrastructure/Endpoints.cs` (~767 lines) | Auth cookies, workspaces, members, invites, API keys, webhooks, storage | Mirror Commerce: `Endpoints/AuthEndpoints.cs`, `WorkspaceEndpoints.cs`, `ApiCredentialEndpoints.cs`, `WebhookEndpoints.cs`, `StorageEndpoints.cs` + thin `MapOneEndpoints` |
| `Modules/Lhdn/Infrastructure/Endpoints.cs` (~248 lines) | Documents write/read, keys, config, taxpayer | `DocumentEndpoints`, `ConfigEndpoints`, `ApiKeyEndpoints` (or delete keys once fully One-owned) |
| `Modules/Billing/Infrastructure/Endpoints.cs` (~239 lines) | Ledger, summary, documents, credits, profile | Split by public vs admin; document download vs ledger |
| `Modules/Payments/Infrastructure/Endpoints.cs` + `IntegrationEndpoints.cs` + `PlatformEndpoints.cs` | Already partially split | Keep; ensure webhook endpoints stay isolated from admin config |
| `Modules/Commerce/Infrastructure/Endpoints.cs` | **Already good** (~82 lines facade + `Endpoints/*`) | Model for other modules |
| `Modules/Ops/Infrastructure/Endpoints.cs` | Chat/stream likely dense | Split stream vs system-message vs tools |

## 2.3 Query services and workers

| File / partial | Smell | Chunk plan |
|----------------|-------|------------|
| `CommerceQueryService.*.cs` | Already partials (Products, Subscribers, …) | **Good pattern** — keep; ensure no god methods re-accumulate in one partial |
| `DunningEngineJob.cs` (500+ lines) | Past-due scan, pre-due, autocharge, communication, catch-up, WhatsApp effective-action | Extract pure functions: `DunningStepSelector`, `DunningChargeDispatcher`, `DunningCommunicationDispatcher`, keep job as orchestration loop |
| `BillingEngineJob.cs` | Sibling complexity | Same pattern: claim → process one sub → save |
| `LhdnGatewayAdapter.cs` (~385 lines) | Rate limiters, token, submit, poll, cancel, TIN | Split `LhdnAuthClient`, `LhdnDocumentClient`, `LhdnRateLimiterRegistry` |
| `StripeGatewayAdapter.cs` / other gateways | Checkout + portal + off-session + webhook parse | Split webhook verification/mapping from checkout generation if growing |
| `ProcessGatewayWebhookCommandHandler.cs` (~307 lines) | Idempotency + map + publish many events | Extract `WebhookIdempotencyService`, `GatewayEventPublisher` |
| `OutboundWebhookDispatcherJob` + handlers | Delivery + signing | Signing already separate (`OutboundWebhookSignature`); keep claim/deliver pure |

## 2.4 TypeSpec chunking

| Artifact | Status | Chunk plan |
|----------|--------|------------|
| `modules/commerce/models.tsp` (~385 lines) | Dense DTO surface | Split `product-models.tsp`, `subscription-models.tsp`, `checkout-models.tsp`, `dunning-models.tsp` |
| `modules/one/routes.tsp` / `models.tsp` | Auth + workspace + credentials + webhooks | Split by surface (public auth vs admin workspace vs developer) |
| `modules/lhdn/models.tsp` + `routes.tsp` | Documents + config + keys | Align with endpoint chunking |
| `docs-*.tsp` | Product docs entrypoints | Good; ensure each stays **product-pure** (no Billing leak into One/Ops — historical gap) |
| `main.tsp` | Orchestrator | Keep thin imports only |

**Gen pipeline note:** chunking TypeSpec is low risk if `main.tsp` imports stay correct; CI `task gen --force` + dirty-client gate protects consumers.

## 2.5 BuildingBlocks

| File | Note |
|------|------|
| `PlatformDbContext.cs` | Tenant filter + domain event dispatch — high criticality; extract filter configuration helpers if growing |
| `OutboxPublisherJob` / `InboxConsumerJob` / `MessageProcessingResultApplier` | Already factored after Phase 0 — keep small |
| `AesSecretVault` + extensions | OK; document key derivation (Jwt vs Kms:MasterKey) in one place |

## 2.6 Tests

| Smell | Chunk |
|-------|-------|
| Large multi-scenario ModuleTests files | One file per behavior cluster (already mostly true under `Lazuar.ModuleTests/{Module}/`) |
| Golden masters for LHDN | Keep under `TestData/`; avoid embedding huge XML in test methods |

---

# 3. What to reorganize

## 3.1 Module layer placement consistency

ADR 001 / module guide: **Contracts → Domain → Application → Infrastructure**.

| Module | Deviation | Reorg recommendation |
|--------|-----------|----------------------|
| **CRM** | No `Application` project; handlers live in Infrastructure | Either add thin Application (commands only) for consistency, or document CRM as “Infrastructure-hosted handlers” exception in ADR 001 amendment |
| **Billing** | Command handlers largely under `Infrastructure/Commands`; Application is thin (queries/LLM) | Prefer handlers in Application for testability without EF; Infrastructure = EF adapters only |
| **One** | Commands in Application (good); large Endpoints still embed cookie/JWT issuance | Move cookie issue helpers to Application/Infrastructure service (`IAuthCookieService`) |
| **Payments** | Application owns webhook handler (good) | Keep money path in Application |
| **Commerce** | Commands in Application; some event handlers in Infrastructure | Prefer all integration event handlers that mutate domain in Application; Infrastructure only EF handlers if needed |
| **Communications** | Application thin | OK if volume stays low |

**Goal:** “Where do I put a new command?” has one answer per module.

## 3.2 SharedKernel vs BuildingBlocks (docs vs empty shell)

| Reality | Doc intent (`002-shared-kernel-vs-building-blocks.md`) |
|---------|--------------------------------------------------------|
| `SharedKernel` is only `SharedKernelMarker.cs` + csproj | IDs, audit markers, pure global value types |

**Reorg options:**

1. **Populate** SharedKernel with true cross-module primitives that must not drag MediatR (e.g. money `Money` VO, `OrganizationId` strong type — *only if* modules will adopt).
2. **Delete SharedKernel project** and stop requiring Domain → SharedKernel if it stays empty (update ADR 001 + boundary tests).
3. **Keep placeholder** but stop pretending it holds real types in docs.

Also: `BuildingBlocks.Domain` references MediatR via `IDomainEvent : INotification` — docs claim “pure C#”. Either fix docs or introduce domain event abstraction without MediatR on Domain.

## 3.3 Host middleware ownership

| Middleware | Location | Note |
|------------|----------|------|
| `ApiKeyAuthenticationMiddleware` | Host | Dual SQL into One + Lhdn — host correctly owns cross-schema auth |
| `TenantSecurityMiddleware` | Host | Exempt path matrix must stay centralized |
| `CorrelationIdMiddleware` | Host | OK |

**Reorg:** when dual-read ends, middleware should only query One (or a small `IApiCredentialLookup` in BuildingBlocks.Application implemented by One Infrastructure via host DI). Avoid permanent host SQL strings.

## 3.4 Messaging vs Communications ownership

| Concern | Owner today | Clarity needed |
|---------|-------------|----------------|
| Template catalog / seed | Communications | Yes |
| Dispatch email/WhatsApp | Messaging (dumb pipe) + Communications hydrate | Docs still mix Community examples |
| Delivery logs | Messaging | API exists; ops UI residual |
| Suppressions | Communications | OK |
| Broadcasts | Communications | OK |

**Reorg docs** to: Communications = content + policy; Messaging = channel execution + credits + delivery log. Remove Community from the story.

## 3.5 TypeSpec / OpenAPI product purity (ADR 006 / 007)

| Intent | Residual risk |
|--------|---------------|
| External DTOs ≠ MediatR contracts | Endpoints as ACL — hold |
| Product-scoped docs for integrators | Ensure `docs-one` / `docs-ops` do not re-import Billing contamination |
| Internal vs public OpenAPI permanent split (Phase D.5) | Plan separate `main-internal.tsp` vs `main-public.tsp` generation |

**Reorg gen outputs:**

- Committed clients: full monolith for internal FE (`api-types-ts` / `api-types-dotnet`).
- Scalar / developers hub: only product docs (`docs-lhdn`, `docs-commerce`, `docs-payments`, selective one).
- Superadmin `platform/*`: thin TypeSpec residual (C.8) — either flesh out or explicitly “internal only, untyped admin.”

## 3.6 Solution / project graph hygiene

- Central package versions: `Directory.Packages.props` — **version skew** (see §5 / package section).
- `NoWarn` NU1603/NU1605 and NuGet audit not as errors — intentional debt; schedule audit.
- `SharedKernel` references BuildingBlocks.Domain — unusual for a “kernel”; invert or empty.

## 3.7 Migrations folder growth

Commerce already has many migrations (initial + dunning + outbox + charge attempts + …).  

**Reorg practice (ops, not code now):**

- Never `api:migrations:purge` in shared envs casually.
- Prefer additive migrations; squash only with coordinated greenfield reset.
- Document “one migration per module per PR” when touching multiple schemas.

## 3.8 ADR currency reorg

Proposed living index (not more ADRs for every cleanup):

| Decision area | Canonical living doc | Historical ADRs |
|---------------|----------------------|-----------------|
| Module layout | ADR 001 + amendment for CRM | 001 |
| Events vs BB | ADR 003 + `docs/001-cross-module-communication.md` (rewrite Community out) | 003 |
| Payments webhooks | ADR 004/009 + `docs/006` | 004, 009 |
| TypeSpec | ADR 005/006/007 + `docs/api-versioning.md` | 005–007 |
| Product scope | ADR 019–023 **stack** with watermark “shipping truth 2026-08” | 014 obsolete |
| Community/Vault | ADR 022 Phase 2 checklist | 022 |
| MVP hide compliance UI | ADR 023 | 023 |

---

# 4. Fat modular monolith — further modules?

## 4.1 Current module inventory (backend)

| Module | Schema role (intent) | Fatness | Split candidate? |
|--------|----------------------|---------|------------------|
| **One** | Identity, workspace, platform API keys, outbound webhooks, storage | Endpoints fat; credentials + webhooks growing | **Maybe subdivide later** — not new deployable service |
| **Payments** | BYOK gateways, inbound webhooks, integration checkout | Multiple adapters; manageable | **No** — gateway adapters stay inside Payments |
| **Commerce** | Products, checkout, subs, dunning, coupons, stats | Largest product surface; good internal folder split | **No new module** — optionally internal folders “Dunning” vs “Catalog” without new csproj |
| **Billing** | Ledger, credits, PDFs, consolidation | Financial truth critical | **No** — do not extract “Ledger service” yet |
| **Lhdn** | MyInvois, certs, UBL, poll/submit | Heavy but cohesive compliance | **No** — keep as compliance product module |
| **Communications** | Templates, Resend BYOK, broadcasts | Medium | **No** |
| **Messaging** | Channel send, credits, delivery logs | Thin | **No** — do not merge into Communications yet (ADR 003 separation is valuable) |
| **CRM** | Client profiles, consent, anonymize | Small, incomplete layering | **No** — flesh, don’t split |
| **Ops** | Agent chat, LLM tools | Optional product | **No** — keep lobotomizable |

**BuildingBlocks** is not a module; resist dumping domain logic there.

## 4.2 When *not* to add modules

Do **not** add modules for:

- “Dunning” as separate deployable (keep in Commerce until multi-product dunning for non-commerce exists).
- “Webhooks” as separate module (stay in One as platform capability; optionally BuildingBlocks primitives for signing/outbox row shape).
- “Analytics” / “Marketplace” / “Escrow” / “Community rebuild” (explicitly out of Phase D exit and ADR 021 vitamins).
- Per-gateway modules (StripeModule, BillplzModule) — adapters in Payments are correct.

## 4.3 When a *logical* sub-area is OK without new csproj

Inside existing modules, folder boundaries (not new .sln projects) help:

```
Commerce/
  Application/Dunning/
  Application/Checkout/
  Application/Catalog/
  Infrastructure/Workers/Dunning/
```

Same for One:

```
One/
  Application/Credentials/
  Application/Webhooks/
  Application/Workspaces/
```

This reduces fatness without modular-monolith tax (extra DbContext, outbox tables, DI, boundary tests).

## 4.4 When a new module *would* be justified later

Only if **all** of the following hold:

1. Independent schema lifecycle and scale characteristics.
2. Different team or compliance boundary (e.g. multi-country tax engine beyond LHDN).
3. Clear integration-event contract; no sync DB joins.
4. Product commitment (not speculative).

**Possible future modules (not now):**

| Future module | Trigger |
|---------------|---------|
| **Tax** (multi-country) | After LHDN production-trusted (ADR 021 sequence) |
| **Accounting export** (Xero) | After ledger trusted; keep as adapter module or Billing port |
| **Notifications platform** | If WhatsApp + SMS + email + push become multi-tenant products with own billing — currently Messaging+Communications enough |
| **Developer platform** (keys, logs, usage) | If One becomes too fat with workspaces *and* full developer portal backend |

## 4.5 Fatness diagnosis: composition root vs modules

The monolith feels “fat” more from:

1. **Host Program.cs + policy matrix + migrate-all-schemas**
2. **Cross-cutting dual paths** (keys, webhooks LHDN vs One)
3. **Product surface area** (CaaS + compliance + ops agent) coexisting under ADR 023 hide

…than from “too many modules.” **Nine modules is appropriate.** Maintenance should thin dual paths and files, not invent module #10 for organization.

## 4.6 Extract microservice? (explicit recommendation)

**No** for maintenance phase. Event bus is in-process `InMemoryEventBus`; outbox is per-schema but dispatch is single process. Microservice extraction before:

- distributed cache for auth
- true inbox-first handlers for critical paths
- multi-instance workers validated
- separate deploy units for workers vs API

…would multiply failure modes without product benefit.

---

# 5. Other maintenance questions the team should ask

This section is the **question bank** for planning sessions. Answers should become ADRs, runbooks, or checklist items — not silent assumptions.

## 5.1 Observability & operations

1. What is the **single pane** for “was this payment fulfilled?” today (SQL + logs per C.9 notes) — when do we build a support timeline UI?
2. Who pages when `/health/ready` fails on outbox lag? What is the threshold env var in prod?
3. Do we export metrics to Prometheus/Grafana/Azure Monitor, or is `/health/metrics` scrape-by-curl enough for 2026?
4. Are dead-letter outbox rows reviewed on a cadence? Who owns `docs/007-outbox-inbox-dead-letter-runbook.md` currency?
5. Correlation id: is it propagated to gateway HTTP clients and customer webhook deliveries?
6. Structured log field names: frozen schema for support queries?
7. Do we need **worker-only** process (no HTTP) for billing/dunning/LHDN poll before multi-replica API?

## 5.2 Security & secrets

1. Production **must** set `Kms:MasterKey` / `Jwt:Secret` — is deploy env.example complete and CI-checked for forbidden defaults?
2. When do we **remove plaintext secret fallbacks** after re-encrypt migration?
3. Azure Key Vault is optional at boot — is silent fallback to env acceptable for prod?
4. PFX + gateway secrets: AES app-level vault vs HSM/KMS long-term (B.8 residual)?
5. API key last_used, IP allowlist, expiry (Phase D.5) — priority vs SSRF on outbound webhooks?
6. Resend webhook secret fail-closed outside Development — same for all inbound vendor webhooks?
7. Platform admin password/emails via `PLATFORM_ADMIN_*` — rotation process?
8. Cookie domain `.lazuar.com` — local/dev vs multi-env consistency after rename?
9. Rate limiting: LHDN gateway has limiters; are public checkout and register protected against abuse?
10. NuGet audit warnings not errors (`NU190x`) — when is vulnerability budget reviewed?

## 5.3 Multi-tenancy & isolation

1. Fail-closed EF filters: are **all** worker/event handlers audited for `IgnoreQueryFilters` + explicit `OrganizationId`?
2. One routes still partially exempt from tenant middleware (`/one/workspaces`, auth/me) — is that still correct?
3. Public commerce slug binding: any remaining session-id-only paths?
4. Cross-tenant IDOR tests: expand to LHDN document get, API keys list, webhook endpoints, storage presign?
5. Superadmin platform routes: TypeSpec thin — intentional internal?
6. Data residency / backup: per-tenant export for GDPR-style delete (CRM anonymize covers profiles — what about ledger immutability)?

## 5.4 API versioning & contracts

1. Is `/api/v1` + `info.version 1.0.0` enough until first external partner? (`docs/api-versioning.md`)
2. Who approves breaking changes to webhook signatures?
3. Internal vs public OpenAPI permanent split — schedule?
4. Minimal API vs OpenAPI auto-diff in architecture tests — yes/no?
5. Snake_case JSON policy frozen forever?
6. Idempotency-Key required for LHDN submit — same for payment M2M checkout?
7. Generated clients committed: is NSwag `Generated/Models.cs` vs `Lazuar.ApiContracts.cs` dual output still needed?
8. `packages/api-spec/dist` gitignored — developers hub how loads OpenAPI in CI/CD deploy?

## 5.5 ADR / documentation currency

1. Which ADR is **shipping truth** for product scope (023 MVP hide vs 021 compliance moat vs README)?
2. Rewrite `apps/lazuar-api/docs/001`–`006` Community examples — owner + deadline?
3. Gap reports under `docs/001-gaps` are snapshots from 2026-08-03 — archive vs annotate “fixed in Phase X”?
4. ADR 022 open decisions still open — close or redate?
5. Password hashing upgrade doc (`008`) still accurate vs `PasswordService`?
6. Cross-module doc hybrid inbox model — updated after Phase 0 decision?

## 5.6 CI / CD / environments

1. Why does CI omit `Modules.Ops.Tests` while Taskfile includes it?
2. Is Docker available in CI for Testcontainers concurrency tests?
3. `task gen --force` on every PR — duration budget?
4. Migrate-on-boot in production: safe with multiple replicas starting simultaneously? (race on EF migrations)
5. GHCR bake + deploy scripts: API image includes all module migrations?
6. Prod replica count documented as 1 — enforced in compose/k8s?
7. Separate staging environment with LHDN sandbox credentials?

## 5.7 Package versions & .NET 10

**Current posture:**

- `global.json`: SDK `10.0.100`, `allowPrerelease: true`
- TFM: `net10.0` via `Directory.Build.props`
- EF Core / Npgsql EF / JwtBearer: **`10.0.0-preview.*`**
- Several Microsoft.Extensions.* packages: **9.0.0** while Configuration **10.0.x** — central version skew
- Serilog.AspNetCore 9, test SDK 17.x, Stripe.net 48, QuestPDF 2026.6.1, etc.

**Questions:**

1. When do we move from **preview EF/Npgsql** to stable .NET 10 GA package line?
2. Who owns `Directory.Packages.props` upgrades (monthly?)?
3. Are `NU1603/NU1605` suppressed because of known preview conflicts — what is the exit criteria to re-enable?
4. MediatR 12 vs future v12/v13 — pin policy?
5. Stripe.net / Razorpay / Billplz API version pins — tested against live sandbox on upgrade?
6. TypeSpec compiler `~1.13` — upgrade cadence with OpenAPI emit breaks?
7. Node 22 in CI vs local — aligned for gen?

## 5.8 Generation pipeline

1. `task gen` graph: TypeSpec → OpenAPI → openapi-typescript + NSwag + Kiota LHDN — any step still manual?
2. Failure mode if someone edits `Lazuar.ApiContracts.cs` by hand?
3. Should product docs build fail if Billing types leak into `docs-one`?
4. Kiota clean-output: do we lose hand-written SDK wrappers? (structure of `lhdn-sdk-*`)
5. Portal/ops consuming `@repo/api-types-ts` — monorepo workspace version protocol?
6. Postman collection under `docs/postman` — generated or hand-maintained? Drift process?

## 5.9 Performance & data

1. Dapper query indexes for admin list endpoints (subscribers, ledger, delivery logs)?
2. Export 10k cap — streaming plan?
3. Outbox poll interval vs `DatabaseJobTrigger` — load under burst webhooks?
4. LHDN rate limiters process-local (`ConcurrentDictionary`) — multi-instance fairness?
5. B2C consolidation catch-up cost on large tenants?
6. EF InMemory in unit tests hiding N+1 / filter bugs — enough Postgres integration coverage?
7. Connection string: one DB many schemas — connection pool sizing per keyed `ISqlConnectionFactory`?

## 5.10 Background workers & multi-instance

1. Deploy rule still replica=1 — when is multi-replica GA?
2. Subscription rows without lease columns — acceptable long-term?
3. B2cConsolidationJob calendar + catch-up without SKIP LOCKED — freeze replica=1 forever for that job?
4. Configurable `Workers` options — are prod values documented?
5. Poison policy max attempts/backoff — same for all modules?
6. Should domain engines (dunning/billing) move to dedicated worker host project?

## 5.11 Dead feature flags & product half-features

1. WhatsApp: channel strings, template fields, credits, ConsoleMessaging — **flag or product**?
2. PricingModel / PWYW fields — enforced or remove (Phase D.4)?
3. Ops chat / invoicing / billing profile floating islands — unhide criteria metrics?
4. Integration scopes declared but unused on routes — prune catalog?
5. `IsReminderOnly` subscription flag — still used?
6. Community welcome templates still seeded?
7. Dual API key tables — migration end date?
8. Feature “soft-disable gateway” (`IsActive`) — UX complete?

## 5.12 Event-driven architecture honesty

1. Docs say inbox-first; code is mostly **outbox + inline handlers**. Do we update docs to hybrid model officially (Phase 0 residual mentioned store-and-ack decision)?
2. Which handlers **must** become inbox-backed (money, credits, LHDN state)?
3. InMemoryEventBus multi-handler sequential failure semantics after retry policy — still correct?
4. Customer webhook outbox vs module outbox — two concepts documented for support?

## 5.13 Money & compliance trust (post solidification)

1. Offline refund without gateway ref — product need?
2. Commerce dispute ledger beyond utility clawback — scope?
3. Deferred revenue: kill or build?
4. Fee fidelity per gateway (Stripe fee extraction residual) — ops reporting honesty?
5. B2C consolidation month-end dry run in sandbox (D.3) — scheduled?
6. V1.1 XML signing vs JSON-only path documentation currency?

## 5.14 Developer platform

1. Commerce M2M admin API still deferred (D5) — still correct?
2. OAuth2 client_credentials timeline?
3. SDK publish on tag (ADR 011) — automated?
4. Authenticated Try-it with test keys?
5. Usage metering vs credit wallet for API calls?

## 5.15 Naming after plan 002 (lazuar-pay)

1. Remaining string identifiers: `lazuar-api`, JWT issuer `lazuar-api`, cookie names, package ids — intentional brand?
2. External partner docs still saying hub.lazuar.com?
3. Historical paths in gap reports — add banner “map names” only, or rewrite?

---

# 6. Prioritized backlog themes

Themes are ordered for **maintenance ROI** after Phase 0–C product solidification. Each theme lists sample work items (not full tickets).

## P0 — Stop dual truths and dual stores (next 2–4 weeks)

| Theme | Work | Why now |
|-------|------|---------|
| **T0.1 Legacy API key cutover** | Inventory `lhdn.DeveloperApiKeys`; migrate to One; remove dual-read + dual revoke | Security, cache, cognitive load |
| **T0.2 Docs truth pass (backend)** | Rewrite Community out of `apps/lazuar-api/docs/*`; watermark ADR stack; README channel honesty (WhatsApp) | Prevent reintroduction of dead modules |
| **T0.3 CI = Taskfile tests** | Add `Modules.Ops.Tests` to CI; fail if projects diverge | Silent untested Ops LLM path |
| **T0.4 Secret fallback exit plan** | Re-encrypt job + date to remove plaintext fallback | Compliance/security |
| **T0.5 LHDN webhook converge decision** | ADR: One dispatcher vs shared BB; kill fire-and-forget as primary | Product reliability claim |

## P1 — Operability & scale readiness (same quarter)

| Theme | Work |
|-------|------|
| **T1.1 Observability production** | OTel/Prometheus; alert rules; split dead-letter metrics; support payment timeline SQL views or admin read API |
| **T1.2 Multi-instance auth cache** | TTL or Redis for API keys |
| **T1.3 Migrate-on-boot safety** | Leader election / init container migrations; document dual-start race |
| **T1.4 Worker host option** | Spike separate worker process for dunning/billing/LHDN poll |
| **T1.5 Outbound webhook SSRF + redeliver + rotate** | Close B residuals that are security-adjacent |
| **T1.6 Package matrix** | Align Microsoft.Extensions 9 vs 10; track EF preview → stable |

## P2 — Structural maintainability (ongoing, opportunistic)

| Theme | Work |
|-------|------|
| **T2.1 Chunk fat files** | One Endpoints; Program.cs; DunningEngineJob; LhdnGatewayAdapter; Commerce models.tsp |
| **T2.2 Layer consistency** | Billing handlers → Application; CRM Application decision |
| **T2.3 SharedKernel decide** | Populate or delete |
| **T2.4 Event catalog living doc** | Publishers/subscribers/outbox module; kill orphans |
| **T2.5 Architecture tests** | OpenAPI vs Minimal API; forbid dead namespaces |
| **T2.6 TypeSpec product purity** | Permanent internal/public split; platform routes honesty |

## P3 — Product Phase D (only after P0 honesty)

| Theme | Work |
|-------|------|
| **T3.1 WhatsApp Meta Cloud or permanent demote** | D.1 or remove WA from defaults/marketing |
| **T3.2 Dunning flexibility** | D1 decision + run snapshots (D.2) |
| **T3.3 Compliance UI re-surface** | D.3 when metrics trusted |
| **T3.4 Commerce M2M / OAuth** | D.5 when integrator demand proven |
| **T3.5 Deferred revenue / Xero** | Kill path or real schedules |

## P4 — Deletion & archive (calendar-driven)

| Theme | Work |
|-------|------|
| **T4.1 ADR 022 schema drop** | After FE/types cleanup and purchaser plan |
| **T4.2 Gap report archive** | Move `docs/001-gaps` to historical with “superseded by Phase X” banners |
| **T4.3 Stale postman / import playbooks** | Delete or rewrite for Commerce |

---

# 7. Detailed answers to the five planning questions (expanded)

## Q1 — What to remove / delete / improve?

**Remove / finish removing**

- Community/Vault **conceptual and documentation residue** (modules already gone from backend tree); DB schemas when safe; ADR 022 Phase 2 close-out.
- Deferred revenue **dead path** (job unregistered) — delete or implement, not leave liminal.
- Legacy `lhdn.DeveloperApiKeys` dual-read after migration.
- LHDN fire-and-forget webhooks as parallel product mechanism.
- Orphan integration events / twin DTOs / phantom TypeSpec fields.
- WhatsApp as marketed channel while `ConsoleMessagingService` is wired — either implement or demote defaults + copy.
- Stale Community-centric examples in api docs and TypeSpec README.

**Improve**

- Secret re-encrypt + remove fallbacks.
- Distributed or short-TTL API key cache.
- Observability export and alerting.
- Outbound webhook security/ops residuals (SSRF, redeliver, rotate, payload richness).
- CI completeness vs Taskfile.
- Hybrid event model **documented as intentional** or moved toward inbox-first for money paths.
- Package version central alignment and .NET 10 stable track.
- Test depth for SKIP LOCKED and HTTP IDOR.

**Do not remove**

- Dark-matter compliance/ledger/LHDN backend (ADR 023) until product unhide.
- Multi-gateway adapter set (core CaaS).
- Platform credentials + workspace webhooks (integrator spine).

## Q2 — What to chunk into smaller files?

Highest ROI chunk list:

1. `Modules/One/Infrastructure/Endpoints.cs` (~767 lines) → endpoint partials by domain.
2. `src/Lazuar.Api/Program.cs` → hosting extension methods (auth policies, module registration, migrator, health).
3. `DunningEngineJob` / `BillingEngineJob` → orchestration + pure step services.
4. `LhdnGatewayAdapter` → auth/document/rate-limit collaborators.
5. `ProcessGatewayWebhookCommandHandler` → idempotency + publish helpers.
6. TypeSpec `commerce/models.tsp` and large `one/*` routes/models.
7. Keep Commerce’s `Endpoints/` + `CommerceQueryService` partials as the **house style**.

## Q3 — What to reorganize?

1. **Handler layer placement** (Billing/CRM consistency with ADR 001).
2. **SharedKernel empty shell** decision.
3. **Messaging vs Communications** narrative (docs + template naming).
4. **Host SQL dual-read** collapse into One-owned lookup.
5. **TypeSpec internal vs public** generation topology.
6. **ADR/doc watermark** so 014/020 wishlist does not override 021/023 shipping truth.
7. **Event docs** hybrid model honesty.
8. Optional **folder subdomains** inside One/Commerce without new modules.

## Q4 — Further modules?

**No new modules for maintenance.** Nine modules match the product. Prefer:

- folder/subdomain organization inside fat modules;
- BuildingBlocks primitives only for truly shared tech (secret vault, outbox row processing, metrics);
- future Tax / Accounting / Developer-platform modules only on product triggers.

Do not microservice-split until multi-instance, worker host, and distributed auth are real problems.

## Q5 — Other maintenance questions?

See **§5** question bank covering observability, secrets, multi-tenancy, versioning, ADR currency, CI, packages, gen pipeline, .NET 10, Npgsql/EF preview, performance, workers, dead flags, event honesty, money/compliance, developer platform, and post-rename naming.

Use that bank as agenda items for a recurring “platform maintenance” ritual (e.g. monthly), separate from feature Phase D.

---

# 8. Suggested maintenance cadence

| Cadence | Activity |
|---------|----------|
| **Weekly** | Outbox dead-letter count; failed webhook deliveries; LHDN stuck; CI red trends |
| **Per PR** | `task gen` honesty (CI); architecture tests; no new dual paths without ADR |
| **Monthly** | `Directory.Packages.props` review; secret audit; ADR watermark check; event catalog drift |
| **Quarterly** | .NET/EF channel upgrade plan; replica/scale review; dual-store cutover review; Phase D honesty vs marketing |
| **Per release** | Re-encrypt residual progress; versioning notes; runbook dry-run (payment support query, dead-letter replay) |

---

# 9. Relationship to existing plans

| Plan / doc | Relationship |
|------------|--------------|
| `plans/001-backend` Phase 0–C | **Done bar for money/auth/isolation** — maintenance starts *after* this, cleaning residuals |
| `plans/001-backend` Phase D | **Product differentiation** — do not use maintenance to sneak D features without honesty gates |
| `docs/001-gaps/*` | Historical evidence; many items fixed — use residuals lists + this roadmap, not re-fix all gaps |
| `docs/api-versioning.md` | Keep as integrator contract policy; link from maintenance versioning questions |
| ADR 022 / 023 | Deletion and hide strategy — maintenance owns Phase 2 finish and dark-matter discipline |
| Plan 002 rename | Naming residue in docs only for backend scope |

---

# 10. Definition of “maintenance healthy”

The backend+TypeSpec surface is maintenance-healthy when:

1. **One auth store** for machine credentials; revoke works on all instances by design.
2. **One outbound webhook delivery stack** quality bar for all products that claim webhooks.
3. **Docs and ADRs** describe shipping modules and real channels (no Community lifecycle, no fake WhatsApp).
4. **CI** runs every test project Taskfile knows; gen drift fails PRs.
5. **Secrets** at rest are encrypted without plaintext fallback for production data ages.
6. **Fat files** in One/host/dunning/LHDN gateway are chunked enough that PR review is local.
7. **No liminal features** (deferred revenue, dual keys, dual webhook stacks) without an owner and end date.
8. **.NET/package channel** is intentional (preview accepted with date, or stable).
9. **Support** can answer payment fulfillment from documented logs/tables (or a real timeline tool).
10. **Scale story** is either replica=1 enforced or multi-instance proven for workers + auth cache.

---

# 11. Immediate next sessions (agenda templates)

## Session A — Deletion & honesty (90 min)

- ADR 022 Phase 2 remaining backend/docs items  
- WhatsApp: implement vs demote defaults  
- Deferred revenue: delete vs backlog  
- Dual API keys cutover date  

## Session B — Operability (90 min)

- Metrics/alerts destination  
- Migrate-on-boot vs init job  
- API key cache strategy  
- CI Ops tests + Testcontainers  

## Session C — Structure (90 min)

- Program.cs / One Endpoints chunk plan  
- SharedKernel populate vs delete  
- Billing/CRM layering  
- TypeSpec internal/public split  

## Session D — Phase D gate (60 min)

- Only open D items that pass honesty: marketing claim match, support path, tests  
- Explicit non-goals for next quarter  

---

# 12. Appendix — residual checklist extracted from Phase 0–C (maintenance-relevant)

These are **not re-opened product features**; they are honesty residuals that feed maintenance themes.

| Residual | Source phase | Maps to theme |
|----------|--------------|---------------|
| Multi-instance API key cache | 0 / B | T1.2 |
| LHDN customer webhooks not on One dispatcher | B.4.4 | T0.5 |
| Webhook redeliver / rotate / SSRF / rich payloads | B | T1.5 |
| Secret plaintext fallbacks until re-save | C.7 | T0.4 |
| Deferred revenue unregistered job | C.1 | T3.5 / delete |
| Prometheus scrape absent | C.6 | T1.1 |
| Inbox/outbox hybrid vs docs | 0 / 15 | T2.4 |
| OpenAPI vs Minimal API auto-diff absent | C.8 | T2.5 |
| Platform TypeSpec thin | C.8 | T2.6 |
| SKIP LOCKED not proven under InMemory | C.5 | T1.4 |
| Ops tests in Taskfile vs CI | C / CI file | T0.3 |
| WhatsApp console provider | A / D | T3.1 |
| Community doc examples | 022 / docs | T0.2 |

---

# 13. Appendix — module “fatness” map (qualitative)

| Area | Assessment | Primary maintenance lever |
|------|------------|---------------------------|
| One Endpoints | High | Chunk files |
| Commerce domain+workers | High | Chunk workers; keep module |
| Payments gateways+webhooks | Medium-high | Adapter discipline; webhook handler helpers |
| Lhdn gateway+templates+XSD | High but cohesive | Collaborator classes; not new module |
| Billing ledger handlers | Medium | Layer move Application; delete dead revenue |
| Communications/Messaging | Medium-low | Naming/docs; WhatsApp decision |
| CRM | Low completeness | Complete layering, don’t split |
| Ops | Medium optional | Keep hideable |
| BuildingBlocks | Medium growth risk | Guard against domain leakage |
| TypeSpec commerce/one | Medium | Split models; purity |
| Host Program | High ceremony | Extension methods |

---

# 14. Appendix — explicit non-goals for this maintenance plan

- Implementing Meta WhatsApp product (that is Phase D product work once decided).
- Building Xero / multi-country tax / marketplace / community rebuild.
- Extracting microservices.
- Rewriting frontend (except contract honesty constraints).
- Big-bang migration squash across all module schemas.
- Replacing MediatR/EF wholesale.

---

*End of uncondensed maintenance roadmap. Update in place as dual paths are retired and package channels stabilize; link new ADRs from §3.8 index rather than forking another parallel narrative.*
