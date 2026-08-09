# 009 — BuildingBlocks ownership map (stay / move / defer)

**Status:** Active policy (maintenance Phase 15)  
**Date:** 2026-08-09  
**Evidence:** [`plans/004-maintenance/06-building-blocks-shared-kernel.md`](../../../plans/004-maintenance/06-building-blocks-shared-kernel.md), [`plans/004-maintenance/decisions.md`](../../../plans/004-maintenance/decisions.md)  
**Companion:** [`002-shared-kernel-vs-building-blocks.md`](./002-shared-kernel-vs-building-blocks.md)

This document is the **ownership map** for what may live in BuildingBlocks / SharedKernel today, what should move to a product module, and what is explicitly deferred. It does **not** authorize a big-bang relocation of LLM, email, or messaging stacks in a single PR.

---

## 1. Core rules (refined)

| Layer | Rule |
|-------|------|
| **BuildingBlocks** | Technical spine for the modular monolith. **Allowed:** platform multi-tenancy markers (`IMustHaveTenant`, `OrganizationId` stamp/filter), CQRS, outbox/inbox, generic crypto/password/token, thin object-storage port, technical dead-letter metrics. **Forbidden:** module business aggregates, private-schema product SQL that encodes one module’s domain vocabulary, brand/product HTML. |
| **SharedKernel** | **Intentional empty marker** until a true cross-module value object appears. No write-model entities. See `SharedKernel/SharedKernelMarker.cs`. |
| **Modules** | Own product orchestration, domain vocabulary, and (eventually) product metrics contributors. |
| **Host (`Lazuar.Api`)** | Composition root only. Single `PlatformDbContext` base lives in **BuildingBlocks.Infrastructure** — no host-parallel base type. |

Architecture tests (`ModuleBoundaryTests`) enforce BB ↛ `Modules.*` assembly edges. They do **not** catch conceptual leakage (schema names, LHDN SQL) inside BB — this map does.

---

## 2. Stay in BuildingBlocks (technical core)

| Concern | Home | Notes |
|---------|------|--------|
| Entity, ValueObject, IAggregateRoot, IBusinessRule, IDomainEvent | Domain | Lean structural DDD |
| `IMustHaveTenant` / OrganizationId filter | Domain + PlatformDbContext | Platform tenancy policy — **allowed** (docs/002 was overly absolute) |
| ICommand / IQuery / handlers | Application | CQRS |
| IIntegrationEvent, IEventBus, IEventBusSubscriptions, IIntegrationEventHandler | Application | Cross-module async |
| Outbox / inbox jobs, retry, TypeResolver, DatabaseJobTrigger, InMemoryEventBus | Infrastructure | Messaging spine — **do not extract** |
| `PlatformDbContext` (BB) | Infrastructure | Tenant filter + domain-event cascade + job poke |
| ISqlConnectionFactory + Npgsql factory | Application / Infrastructure | Shared SQL access |
| IExecutionContextAccessor | Application (impl in host) | Ambient tenant/user |
| IPasswordService, ISecretVault, ITokenGeneratorService | Application + Infrastructure | Generic security |
| JWT **generation** helper (`IJwtService`) | Application port + Infrastructure `JwtService` | **R30:** interface moved to Application |
| Thin R2 / object storage (`IR2StorageService` + S3 impl) | Application port + Infrastructure adapters | **R30:** interface in Application; keep shared (Billing + One) |
| Technical metrics: dead-letter counters tied to message applier | Application.Observability | Messaging-technical |
| GlobalExceptionHandler | Infrastructure or host | Host-facing; either is fine |

---

## 3. Move (or demote) — product-shaped / wrong owner

Ownership target when a dedicated PR is justified. **Not** all moved in Phase 15.

| Item | Recommended owner | Why |
|------|-------------------|-----|
| `IEmailService`, Resend/Console, ResendOptions, `EmailTemplateBuilder` | **Messaging** (+ Communications for BYOK/config) | Only Messaging sends; brand HTML + org tags are product |
| `IMessagingService`, ConsoleMessagingService | **Messaging** | Channel transport; decision 00.4 freezes multi-channel product work |
| `MarkdownParser` (Markdig) | **Communications** (or thin shared content slice later) | Avoid Markdig on every Contracts fan-out |
| `IMagicLinkTokenService` / MagicLinkTokenService | **Commerce** | **R33 done** — port on `Modules.Commerce.Contracts`; HMAC impl in Commerce.Infrastructure.Security |
| DocumentLinkSigner **payload** helpers (`FinalDocumentPayload`, `DraftDocumentPayload`) | **Billing** / **Commerce** | Generic HMAC `Sign`/`TryValidate` may stay in BB Security |
| Full LLM stack (`IChatClientFactory`, policies, title generator) | **Ops** Application.Llm + Infrastructure.Llm | **R31 done** — registered in `AddOpsModule`; OpenAI removed from BB |
| `IAgentPromptProvider`, `AgentToolAttribute` | **Ops.Contracts** / Ops.Application | Agent product feature; Billing implements via Contracts |
| `LazuarMetrics.RecordDunningCancel` | **Commerce** (or tagged generic counter) | Product metric |
| LHDN stuck SQL in `PlatformMetricsCollector` | **Lhdn** `IPlatformMetricsContributor` | Private schema + domain status vocabulary |
| Module-specific fields on `BackgroundWorkerOptions` | Per-module `IOptions<T>` | BB should not catalog every worker interval |

---

## 4. Grey area — stay shared *if* multi-module and thin

| Item | Policy |
|------|--------|
| **R2 / object storage** | Stay as **thin shared port** (Billing + One). Interface lives in Application (`IR2StorageService`); adapters in Infrastructure. No Storage module unless blob lifecycle becomes product. |
| **Email port** | Prefer Messaging-owned long-term. Thin `IEmailService` may remain in Application while product traffic goes through Messaging integration events. |
| **Platform metrics aggregator** | May stay in BB **if** pluginized (`IPlatformMetricsContributor` / schema registration). Today: hardcoded schema list + LHDN SQL — accept temporary god collector with plugin direction (comment on `PlatformMetricsCollector`). |
| **Outbox lag gauges** | Shared technical observability — stay; schema list should eventually come from registration, not a constant array. |

---

## 5. SharedKernel decision (Phase 15.1)

| Choice | Decision |
|--------|----------|
| Populate with shared domain types **now**? | **No** |
| Keep as marker? | **Yes — intentional** |
| Why | No true cross-module VO/ID yet. Every module Domain ProjectReferences SharedKernel for architecture-test anchor and future pressure valve. Filling it prematurely invites entity dumps and cycles. |
| When to populate | Only when a **real** shared VO appears (e.g. strong-typed `OrganizationId`) used by ≥2 modules without dragging write models. |

---

## 6. Explicit deferrals (Phase 15 safe subset)

Full LLM / email / messaging / metrics plugin moves are **large**. Phase 15 ships the map + hygiene only:

| Work | Status |
|------|--------|
| Ownership map (this doc) + docs/002 refine | **Done** (Phase 15) |
| SharedKernel marker documentation | **Done** |
| Delete unused host `Lazuar.Api.Infrastructure.Data.PlatformDbContext` | **Done** |
| Plugin note on `PlatformMetricsCollector` | **Done** (comment only) |
| Move LLM factory / title / policies → Ops | **Done** (R31 / 005-remaining) |
| Move email / Messaging ports | **Deferred** (decision 00.4 + composition root) |
| Port placement (`IR2StorageService`, `IJwtService` → Application) | **Done** (R30 / 005-remaining) |
| Split `BackgroundWorkerOptions` per module | **Deferred** |
| `IPlatformMetricsContributor` plugins | **Deferred** (ticket direction only) |
| BuildingBlocks project splits (Persistence / Messaging / …) | **Deferred** (plan 06 Option A/B later) |

Product-concern move criterion for Phase 15 exit: **explicitly deferred with this map** (no silent kitchen-sink growth).

---

## 7. Decision matrix (quick reference)

| Component | Stay BB | Move | Host | Notes |
|-----------|:-------:|:----:|:----:|-------|
| Entity / VO / rules / IDomainEvent | ✅ | | | |
| IMustHaveTenant | ✅ | | | Platform tenancy |
| CQRS + integration events + bus | ✅ | | | |
| Outbox / inbox | ✅ | | | Backbone |
| PlatformDbContext (BB) | ✅ | | | Single owner |
| Host parallel PlatformDbContext | | | ❌ deleted | Dead weight |
| ISqlConnectionFactory | ✅ | | | |
| IPassword / ISecretVault / ITokenGenerator | ✅ | | | |
| IJwtService port | ✅ App | | | **R30 done** — Application port |
| IR2StorageService | ✅ App thin | | | **R30 done** — Application port |
| IEmailService + Resend + templates | | ✅ Messaging | | Deferred move |
| IMessagingService | | ✅ Messaging | | Deferred; 00.4 freeze product WA |
| MarkdownParser | | ✅ Communications | | Deferred |
| MagicLinkTokenService | | ✅ Commerce | | **R33 done** — Contracts port + Infrastructure HMAC |
| Document payload helpers | | ✅ Billing/Commerce | | Generic sign stays BB |
| LLM factory / title / DI | | ✅ Ops | | **R31 done** — `Modules.Ops.Application.Llm` + `Infrastructure.Llm` |
| AgentTool / IAgentPromptProvider | | ✅ Ops | | **R32 done** — `Modules.Ops.Contracts` |
| Dead-letter metrics | ✅ | | | Technical |
| Dunning / webhook product counters | | ✅ or tagged | | Soft |
| Metrics schema list / LHDN SQL | ✅ if pluginized | Lhdn contributor | | Plugin deferred |
| BackgroundWorkerOptions (as-is) | | ✅ per module | | Deferred |
| SharedKernel | ✅ empty marker | | | Fill only with true shared VOs |

---

## 8. Related decisions (do not reopen casually)

- **00.4 Messaging / WhatsApp:** No multi-channel product in 6 months; Messaging stays thin transport; do not treat email/WhatsApp as “just another BB adapter PR” without reopen.
- **00.6 Scope freeze:** No new modules (Storage/Email/LLM modules) for purity alone.
- **Architecture:** BB must not reference `Modules.*` assemblies (enforced). Conceptual reverse knowledge (schema names) is debt tracked here.

---

## 9. How to use this map

1. **Before adding a type under BuildingBlocks:** check §2 stay list. If it names a product concept (dunning, TaxDocuments, subscription portal URL shape), stop — put it in the owning module or extend this map.  
2. **Before moving a fat stack:** one concern per PR; keep architecture tests green; prefer Options B folders before multi-csproj splits.  
3. **SharedKernel:** do not dump VOs “because the project exists.” Marker-only is correct until a real shared type is needed.
